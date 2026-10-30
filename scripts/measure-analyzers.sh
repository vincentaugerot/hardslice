#!/usr/bin/env bash
# Measures what HardSlice.Analyzers add to real builds of the PerfApp fixture and checks the thresholds of docs/performance/analyzers.md.
# Generates PerfApp from the freshly packed template (generate-app.sh) with its Bookings sample and the 20 synthetic modules
# of generate-perf-modules.sh, then copies it into a variant whose Directory.Build.targets removes the Analyzer items of the
# HardSlice.Analyzers package. Both variants are measured on the same scenarios, alternately, with the compiler server running:
#   A: incremental build after a change to a method body of a slice;
#   B: incremental build after a change to the public API of a Domain aggregate, which recompiles the projects that depend on it;
#   C: full build with --no-incremental, as a reference.
# Each scenario has one warm-up build per variant, then five measured ones; the median is kept. A build without any change
# is never measured: it may analyze nothing. A last full build with -p:ReportAnalyzer=true explains the cost of each analyzer.
#
# Writes the measurement into the report (docs/performance/analyzers.md unless --report is given): replaces its latest measurement
# and adds a line to its history. On GitHub Actions, also writes the measurement to the job summary and the verdict to the step outputs.
# Exit code: 0 when the thresholds hold, or when only the build without HardSlice exceeds a budget (arbitrage, not a failure);
# 3 when a threshold is exceeded by an unstable series (no conclusion: measure again on another runner);
# 4 when a stable series exceeds a threshold because of the analyzers.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
report="$repo_root/docs/performance/analyzers.md"
work=""
keep_work=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --report)
      report="$(cd "$(dirname "$2")" && pwd)/$(basename "$2")"
      shift 2
      ;;
    --work-dir)
      mkdir -p "$2"
      work="$(cd "$2" && pwd)"
      keep_work=true
      shift 2
      ;;
    *)
      echo "usage: $0 [--report <file>] [--work-dir <empty-dir>]" >&2
      exit 2
      ;;
  esac
done

for marker in "<!-- measure-analyzers:latest:begin -->" "<!-- measure-analyzers:latest:end -->" "<!-- measure-analyzers:history -->"; do
  if ! grep -qxF "$marker" "$report"; then
    echo "error: $report has no line '$marker' to write the measurement at." >&2
    exit 1
  fi
done

if [[ -z "$work" ]]; then
  work="$(mktemp -d "${TMPDIR:-/tmp}/hardslice-perf.XXXXXX")"
elif [[ -n "$(ls -A "$work")" ]]; then
  echo "error: $work is not empty." >&2
  exit 1
fi

cleanup() {
  dotnet build-server shutdown > /dev/null 2>&1 || true
  if [[ "$keep_work" == false ]]; then
    rm -rf "$work"
  fi
}
trap cleanup EXIT

app_name="PerfApp"
runs=5
# Thresholds of docs/performance/analyzers.md, in milliseconds or percent. Only a decision of the maintainer recorded there changes them.
budget_a=10000
overhead_a=1000
budget_b=20000
overhead_b=2000
overhead_c_percent=15
# Beyond this spread of a series (maximum minus minimum, over the median), its exceedances are not qualified.
max_spread_percent=20

with="$work/with-analyzers/$app_name"
without="$work/without-analyzers/$app_name"
slice_file="src/$app_name/Features/Module10/ListItems/ListItems.cs"
aggregate_file="src/$app_name.Domain/Module10/Item.cs"

echo "==> Generating the $app_name fixture"
"$repo_root/scripts/generate-app.sh" "$work" "$app_name"
"$repo_root/scripts/generate-perf-modules.sh" "$work/$app_name" "$app_name"
mkdir -p "$(dirname "$with")" "$(dirname "$without")"
mv "$work/$app_name" "$with"
cp -R "$with" "$without"

# The variant without analyzers keeps the package reference and its build files: only the compiler stops loading its analyzers.
without_targets="$without/Directory.Build.targets"
if ! grep -qxF "</Project>" "$without_targets"; then
  echo "error: $without_targets has no closing </Project> line." >&2
  exit 1
fi
removal='  <!-- Added by measure-analyzers.sh: the variant without HardSlice analyzers. -->
  <Target Name="RemoveHardSliceAnalyzers" AfterTargets="ResolveLockFileAnalyzers">
    <ItemGroup>
      <Analyzer Remove="@(Analyzer)" Condition="'"'"'%(Analyzer.NuGetPackageId)'"'"' == '"'"'HardSlice.Analyzers'"'"'" />
    </ItemGroup>
  </Target>
</Project>'
targets_text="$(cat "$without_targets")"
printf '%s\n' "${targets_text%</Project>}$removal" > "$without_targets"

# Restores and builds both variants once, unmeasured: the scenarios start from built solutions.
build_log="$work/build.log"
build() {
  local dir="$1"
  shift
  if ! (cd "$dir" && dotnet build "$app_name.slnx" -nologo -v:q "$@") > "$build_log" 2>&1; then
    cat "$build_log" >&2
    echo "error: the build of $dir failed." >&2
    exit 1
  fi
}

echo "==> Restoring and building both variants"
build "$with"
build "$without"

has_hardslice_analyzer() {
  (cd "$1" && dotnet msbuild "src/$app_name/$app_name.csproj" -nologo -t:ResolveLockFileAnalyzers -getItem:Analyzer) \
    | grep -q '"NuGetPackageId": "HardSlice.Analyzers"'
}
if ! has_hardslice_analyzer "$with"; then
  echo "error: the variant with analyzers does not load HardSlice.Analyzers." >&2
  exit 1
fi
if has_hardslice_analyzer "$without"; then
  echo "error: the variant without analyzers still loads HardSlice.Analyzers." >&2
  exit 1
fi

# Assemblies each scenario must compile again: A only the application, B the Domain and the projects that depend on it, C all of them.
declare -A recompiled=(
  [A]="src/$app_name/obj/Debug/net10.0/$app_name.dll"
  [B]="src/$app_name.Domain/obj/Debug/net10.0/$app_name.Domain.dll src/$app_name/obj/Debug/net10.0/$app_name.dll tests/$app_name.Tests/obj/Debug/net10.0/$app_name.Tests.dll"
  [C]="src/$app_name.Domain/obj/Debug/net10.0/$app_name.Domain.dll src/$app_name/obj/Debug/net10.0/$app_name.dll tests/$app_name.Tests/obj/Debug/net10.0/$app_name.Tests.dll"
)

# Each edit writes a value never written before in that variant, so that every measured build has a real change to compile.
declare -A edits=([with]=0 [without]=0)
edit() {
  local variant="$1"
  local scenario="$2"
  local dir="$with"
  [[ "$variant" == with ]] || dir="$without"
  local n=$(( ${edits[$variant]} + 1 ))
  edits[$variant]=$n
  case "$scenario" in
    A)
      sed -i -E "s|const int pageSize = [0-9]+; // measure-analyzers: method body|const int pageSize = $(( 50 + n )); // measure-analyzers: method body|" "$dir/$slice_file"
      grep -qF "pageSize = $(( 50 + n ));" "$dir/$slice_file"
      ;;
    B)
      sed -i -E "s|public int Revision[0-9]*\(\) => [0-9]+; // measure-analyzers: public API|public int Revision$n() => $n; // measure-analyzers: public API|" "$dir/$aggregate_file"
      grep -qF "public int Revision$n() => $n;" "$dir/$aggregate_file"
      ;;
  esac
}

# Sets duration to the length of one build of the scenario, in milliseconds. Never call it in a subshell: its edits count must persist.
measure() {
  local variant="$1"
  local scenario="$2"
  local dir="$with"
  [[ "$variant" == with ]] || dir="$without"
  local options=(--no-restore)
  [[ "$scenario" != C ]] || options+=(--no-incremental)
  [[ "$scenario" == C ]] || edit "$variant" "$scenario"
  local start end
  touch "$work/before-build"
  start="$(date +%s%N)"
  build "$dir" "${options[@]}"
  end="$(date +%s%N)"
  duration=$(( (end - start) / 1000000 ))

  # A build that compiles nothing is not measured: each scenario must recompile the projects it names.
  local project
  for project in ${recompiled[$scenario]}; do
    if [[ -z "$(find "$dir/$project" -newer "$work/before-build")" ]]; then
      echo "error: scenario $scenario did not recompile $project in $dir." >&2
      exit 1
    fi
  done
}

declare -A durations
for scenario in A B C; do
  echo "==> Scenario $scenario: one warm-up build per variant, then $runs measured builds, alternately"
  measure with "$scenario"
  measure without "$scenario"
  for run in $(seq "$runs"); do
    for variant in with without; do
      measure "$variant" "$scenario"
      durations[$scenario/$variant]+="$duration "
      echo "    $scenario $variant analyzers, run $run: $duration ms"
    done
  done
done

echo "==> Full build with -p:ReportAnalyzer=true"
analyzer_log="$work/report-analyzer.log"
# One node, so that the compiler output of each project follows its CoreCompile target in the log.
(cd "$with" && dotnet build "$app_name.slnx" -nologo --no-restore --no-incremental -m:1 -v:d -p:ReportAnalyzer=true) > "$analyzer_log" 2>&1 || {
  tail -n 50 "$analyzer_log" >&2
  echo "error: the build with -p:ReportAnalyzer=true failed." >&2
  exit 1
}
# project<TAB>seconds<TAB>analyzer, for HardSlice analyzers, then project<TAB>seconds<TAB>(all analyzers).
analyzer_times="$(awk '
  /Target "CoreCompile" in file .* from project "/ { project = $0; sub(/.* from project "/, "", project); sub(/\.csproj".*/, "", project); sub(/.*\//, "", project) }
  /Total analyzer execution time: / { total = $0; sub(/.*time: /, "", total); sub(/ seconds\..*/, "", total); printf "%s\t%s\t(all analyzers)\n", project, total }
  $3 ~ /^HardSlice\.Analyzers\./ { name = $0; sub(/^ *[^ ]+ +[^ ]+ +/, "", name); printf "%s\t%s\t%s\n", project, $1, name }
' "$analyzer_log")"
if ! grep -q $'\tHardSlice\.Analyzers\.' <<< "$analyzer_times"; then
  echo "error: -p:ReportAnalyzer=true reported no HardSlice analyzer." >&2
  exit 1
fi

# min median max of a series of milliseconds.
stats() {
  tr ' ' '\n' <<< "$1" | grep . | sort -n | awk '{ v[NR] = $1 } END { print v[1], v[int((NR + 1) / 2)], v[NR] }'
}
seconds() {
  awk -v ms="$1" 'BEGIN { printf "%.2f", ms / 1000 }'
}
percent() {
  awk -v part="$1" -v whole="$2" 'BEGIN { printf "%.1f", part * 100 / whole }'
}
above() {
  awk -v value="$1" -v limit="$2" 'BEGIN { exit !(value > limit) }'
}

declare -A min median max spread
for key in "${!durations[@]}"; do
  read -r min[$key] median[$key] max[$key] <<< "$(stats "${durations[$key]}")"
  spread[$key]="$(percent $(( ${max[$key]} - ${min[$key]} )) "${median[$key]}")"
done

verdict=pass
raise_verdict() {
  declare -A order=([pass]=0 [arbitrage]=1 [unstable]=2 [overhead]=3)
  (( ${order[$1]} <= ${order[$verdict]} )) || verdict="$1"
}

# Qualifies an exceeded threshold as docs/performance/analyzers.md describes it, into result.
# Arguments: scenario, then the total duration budget for a budget threshold, nothing for an overhead threshold.
qualify() {
  local scenario="$1"
  local budget="${2:-}"
  if above "${spread[$scenario/with]}" "$max_spread_percent" || above "${spread[$scenario/without]}" "$max_spread_percent"; then
    raise_verdict unstable
    result="dépassé, série instable : à mesurer de nouveau sur un autre runner, sans conclusion"
  elif [[ -n "$budget" ]] && above "${median[$scenario/without]}" "$budget"; then
    raise_verdict arbitrage
    result="dépassé, coût du build sans HardSlice : arbitrage sur la fixture, le runner ou le budget"
  else
    raise_verdict overhead
    result="dépassé, surcoût des analyseurs"
  fi
}

threshold_rows=""
check_scenario() {
  local scenario="$1"
  local budget="$2"
  local overhead_limit="$3"
  local with_median="${median[$scenario/with]}"
  local overhead=$(( with_median - ${median[$scenario/without]} ))

  result="respecté"
  if above "$with_median" "$budget"; then qualify "$scenario" "$budget"; fi
  threshold_rows+="| $scenario | Durée totale avec analyseurs | $(seconds "$with_median") s | ≤ $(seconds "$budget") s | $result |"$'\n'

  result="respecté"
  if above "$overhead" "$overhead_limit"; then qualify "$scenario"; fi
  threshold_rows+="| $scenario | Surcoût des analyseurs | $(seconds "$overhead") s | ≤ $(seconds "$overhead_limit") s | $result |"$'\n'
}
check_scenario A "$budget_a" "$overhead_a"
check_scenario B "$budget_b" "$overhead_b"

overhead_c="$(percent $(( ${median[C/with]} - ${median[C/without]} )) "${median[C/without]}")"
result="respecté"
if above "$overhead_c" "$overhead_c_percent"; then qualify C; fi
threshold_rows+="| C | Surcoût des analyseurs | $overhead_c % | ≤ $overhead_c_percent % | $result |"$'\n'

case "$verdict" in
  pass) verdict_text="seuils respectés" ;;
  arbitrage) verdict_text="seuils respectés par les analyseurs ; le build sans HardSlice dépasse un budget, arbitrage à ouvrir" ;;
  unstable) verdict_text="seuil dépassé par une série instable : sans conclusion, à mesurer de nouveau sur un autre runner" ;;
  overhead) verdict_text="seuil dépassé par les analyseurs sur une série stable : une seconde exécution stable consécutive au-dessus du seuil confirme la régression" ;;
esac

duration_rows=""
for scenario in A B C; do
  for variant in with without; do
    key="$scenario/$variant"
    label="avec"
    [[ "$variant" == with ]] || label="sans"
    duration_rows+="| $scenario | $label | $(seconds "${min[$key]}") | $(seconds "${median[$key]}") | $(seconds "${max[$key]}") | ${spread[$key]} % |"$'\n'
  done
done

analyzer_rows=""
while IFS=$'\t' read -r project time analyzer; do
  [[ "$analyzer" != "(all analyzers)" ]] || continue
  total="$(awk -F '\t' -v p="$project" '$1 == p && $3 == "(all analyzers)" { print $2 }' <<< "$analyzer_times")"
  analyzer_rows+="| $project | ${analyzer#HardSlice.Analyzers.} | $time s | $total s |"$'\n'
done <<< "$analyzer_times"

rules="$(awk -F '|' '/^HS[0-9]+ *\|/ { gsub(/ /, "", $1); print $1 }' "$repo_root/src/HardSlice.Analyzers/AnalyzerReleases.Shipped.md" "$repo_root/src/HardSlice.Analyzers/AnalyzerReleases.Unshipped.md" | sort -u | paste -sd ',' - | sed 's/,/, /g')"
commit="$(git -C "$repo_root" rev-parse --short HEAD)"
[[ -z "$(git -C "$repo_root" status --porcelain -- src scripts)" ]] || commit="$commit, avec des modifications non commitées"
sdk="$(cd "$with" && dotnet --version)"
cores="$(nproc)"
memory="$(awk '/^MemTotal:/ { printf "%.1f Gio", $2 / 1048576 }' /proc/meminfo)"
cpu="$(awk -F ': ' '/^model name/ { print $2; exit }' /proc/cpuinfo)"
if [[ "${GITHUB_ACTIONS:-}" == true ]]; then
  runner="GitHub Actions ubuntu-latest, image ${ImageOS:-inconnue} ${ImageVersion:-}"
else
  runner="machine locale, $(uname -sr)"
fi
cs_files="$(find "$with/src" -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' | wc -l)"
cs_lines="$(find "$with/src" -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' -exec cat {} + | wc -l)"
date="$(date -u +%Y-%m-%d)"

measurement="### Mesure du $date

- Commit : \`$commit\`
- Règles HardSlice : $rules
- SDK effectif : $sdk
- Runner : $runner
- Processeur : ${cpu:-inconnu}, $cores cœurs
- Mémoire : $memory
- Fixture : $app_name, Bookings et 20 modules synthétiques de 10 slices, $cs_files fichiers C# et $cs_lines lignes sous \`src/\`
- Verdict : $verdict_text

Durées en secondes, une chauffe puis $runs builds mesurés par variante, en alternance :

| Scénario | Analyseurs | Minimum | Médiane | Maximum | Dispersion |
|---|---|---:|---:|---:|---:|
${duration_rows}
Seuils, sur la médiane :

| Scénario | Mesure | Valeur | Seuil | Résultat |
|---|---|---:|---:|---|
${threshold_rows}
\`-p:ReportAnalyzer=true\`, build complet avec analyseurs : temps cumulé de chaque analyseur HardSlice, puis de tous les analyseurs du projet.
Ces temps se chevauchent (exécution concurrente) : ils expliquent un surcoût, seuls les seuils sur la durée totale font foi.

| Projet | Analyseur HardSlice | Temps | Tous les analyseurs |
|---|---|---:|---:|
${analyzer_rows}"

history_row="| $date | \`${commit%%,*}\` | $rules | $runner | $(seconds "${median[A/with]}") / $(seconds "${median[A/without]}") | $(seconds "${median[B/with]}") / $(seconds "${median[B/without]}") | $(seconds "${median[C/with]}") / $(seconds "${median[C/without]}") | $verdict |"

# Replaces the latest measurement and adds the history line, leaving the rest of the report untouched.
awk -v measurement="$measurement" -v history="$history_row" '
  $0 == "<!-- measure-analyzers:latest:begin -->" { print; print ""; print measurement; skipping = 1; next }
  $0 == "<!-- measure-analyzers:latest:end -->" { skipping = 0 }
  skipping { next }
  $0 == "<!-- measure-analyzers:history -->" { print history }
  { print }
' "$report" > "$work/report.md"
cp "$work/report.md" "$report"

echo
printf '%s\n' "$measurement"
echo "==> Report written to $report"

if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
  printf '## Coût des analyseurs HardSlice\n\n%s\n' "$measurement" >> "$GITHUB_STEP_SUMMARY"
fi
if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
  echo "verdict=$verdict" >> "$GITHUB_OUTPUT"
fi

case "$verdict" in
  pass)
    echo "==> Thresholds respected"
    ;;
  arbitrage)
    echo "::warning::The build without HardSlice already exceeds a budget of docs/performance/analyzers.md: open an arbitrage on the fixture, the runner or the budget."
    ;;
  unstable)
    echo "::error::A threshold is exceeded by an unstable series: no conclusion, measure again on another runner."
    exit 3
    ;;
  overhead)
    echo "::error::HardSlice analyzers exceed a threshold of docs/performance/analyzers.md on stable series. -p:ReportAnalyzer=true above names the analyzer."
    exit 4
    ;;
esac
