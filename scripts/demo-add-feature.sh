#!/usr/bin/env bash
# PB-050: from a solution generated with --no-sample, an agent delivers a business request (a module, its aggregates, a command,
# a query and a domain event with its reaction) through the skills and the item templates, without recreating their structure.
#   prepare <dir>                  generates Shop --no-sample in <dir>, installs the templates where the agent's dotnet new finds them,
#                                  proves the solution green and commits it for the agent's local diff workflow.
#   verify <dir> <trace>           validates the candidate <dir>/Shop against a freshly generated trusted Shop, copies its accepted changes
#                                  into a clean tree, checks the expected elements, no TODO(hardslice): marker and the trusted check,
#                                  then checks that the trace of the agent runs each dotnet new hardslice-* command.
#   record <dir> <trace> <run>     keeps the changes of <dir>/Shop and the trace in demos/add-feature/runs/<run>/.
#   verify-recorded <dir> [run...] generates Shop once, then verifies every recorded run, or the given ones, from its changes, with
#                                  the renames of its changes.rename, and its trace: each must reach the verdict of its verdict file.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$repo_root/scripts/dotnet-cli-home.sh"
source "$repo_root/scripts/demo-common.sh"
demo="$repo_root/demos/add-feature"
runs="$demo/runs"
app_name=Shop
trace_tool=(python3 -I "$repo_root/scripts/agent-trace.py")

# The names that prompt.md gives, and the files of each element at the paths where its template generates them.
module=Catalog
expected_files=(
  "src/$app_name/Features/$module/${module}Module.cs"
  "src/$app_name.Domain/$module/Product.cs"
  "src/$app_name.Domain/$module/ProductId.cs"
  "src/$app_name/Features/$module/ProductConfiguration.cs"
  "tests/$app_name.Tests/Domain/$module/ProductTests.cs"
  "src/$app_name.Domain/$module/Stock.cs"
  "src/$app_name.Domain/$module/StockId.cs"
  "src/$app_name/Features/$module/StockConfiguration.cs"
  "tests/$app_name.Tests/Domain/$module/StockTests.cs"
  "src/$app_name/Features/$module/RegisterProduct/RegisterProduct.cs"
  "tests/$app_name.Tests/Features/$module/RegisterProduct/RegisterProductValidatorTests.cs"
  "tests/$app_name.IntegrationTests/Features/$module/RegisterProduct/RegisterProductTests.cs"
  "src/$app_name/Features/$module/GetProduct/GetProduct.cs"
  "tests/$app_name.IntegrationTests/Features/$module/GetProduct/GetProductTests.cs"
  "src/$app_name.Domain/$module/Events/ProductRegistered.cs"
  "src/$app_name/Features/$module/OpenStockOnProductRegistered/OpenStockOnProductRegistered.cs"
  "tests/$app_name.IntegrationTests/Features/$module/OpenStockOnProductRegistered/OpenStockOnProductRegisteredTests.cs"
  "src/$app_name/Shared/Persistence/Migrations/AppDbContextModelSnapshot.cs"
)
# <file>|<extended regular expression it must match>: the registrations the templates print, the event raised by the aggregate,
# the reaction, a type of the module being written with or without the namespace it comes from.
qualified='([A-Za-z_][A-Za-z0-9_]*[.])*'
expected_contents=(
  "src/$app_name/Program.cs|Add${module}Module[(]"
  "src/$app_name/Program.cs|Map${module}Module[(]"
  "src/$app_name/Features/$module/${module}Module.cs|${qualified}RegisterProductEndpoint[.]Map[(]"
  "src/$app_name/Features/$module/${module}Module.cs|${qualified}GetProductEndpoint[.]Map[(]"
  "src/$app_name/Features/$module/${module}Module.cs|IDomainEventHandler<${qualified}ProductRegistered>"
  "src/$app_name.Domain/$module/Product.cs|new ${qualified}ProductRegistered[(]"
  "src/$app_name/Features/$module/OpenStockOnProductRegistered/OpenStockOnProductRegistered.cs|IDomainEventHandler<${qualified}ProductRegistered>"
  "src/$app_name/Shared/Persistence/Migrations/AppDbContextModelSnapshot.cs|\"$app_name[.]Domain[.]$module[.]Product\""
  "src/$app_name/Shared/Persistence/Migrations/AppDbContextModelSnapshot.cs|\"$app_name[.]Domain[.]$module[.]Stock\""
  "AGENTS.md|$module"
)
# The dotnet new commands the trace must run, as agent-trace.py expect reads them.
expected_commands=(
  "hardslice-module:--name=$module"
  "hardslice-aggregate:--name=Product"
  "hardslice-aggregate:--name=Stock"
  "hardslice-command:--name=RegisterProduct"
  "hardslice-query:--name=GetProduct"
  "hardslice-event:--name=ProductRegistered"
  "hardslice-event:--reaction=$module/OpenStockOnProductRegistered"
)

usage() {
  echo "usage: $0 prepare <dir>" >&2
  echo "       $0 verify <dir> <trace>" >&2
  echo "       $0 record <dir> <trace> <run>" >&2
  echo "       $0 verify-recorded <dir> [run...]" >&2
  exit 2
}

# Generates Shop --no-sample in <work> with its packages, keeps a copy of its sources in <work>/.generated before anything builds it,
# and installs the templates in the isolated home of the dotnet CLI, where a plain dotnet new finds them as for a user.
generate_solution() {
  local work="$1"
  isolate_dotnet_cli_home "$work"
  "$repo_root/scripts/generate-app.sh" "$work" "$app_name" --no-sample
  copy_sources "$work/$app_name" "$work/.generated/$app_name"
  echo "==> Installing the templates where the dotnet new of the agent finds them"
  dotnet new install "$work"/feed/HardSlice.Templates.*.nupkg > /dev/null
}

prepare() {
  work="$(prepare_empty_dir "$1")"
  generate_solution "$work"
  local app="$work/$app_name"
  echo "==> Verifying the generated solution before the agent starts"
  bash "$app/scripts/check.sh"
  git -C "$app" init --quiet
  printf '%s\n' bin/ obj/ TestResults/ >> "$app/.git/info/exclude"
  git -C "$app" add --all
  git -C "$app" -c user.name=HardSlice -c user.email=hardslice@localhost -c commit.gpgsign=false commit --quiet --message "PB-050: $app_name generated with --no-sample"
  echo "==> Solution ready in $app"
  echo "    Run the agent from that directory with $demo/prompt.md and DOTNET_CLI_HOME=$work/dotnet-cli, then: $0 verify $work <trace>"
}

# A change stays in the C# of src/ and of the unit and integration tests, the .gitkeep that the module template left in its
# folders before PB-073, which the recorded runs hold, and the map of AGENTS.md. No project file and no protected file, as the trusted dotnet hardslice protected reads them.
validate_changed_paths() {
  local trusted_app="$1"
  local changed_paths="$2"
  local path

  fail_on_protected_paths "$trusted_app" "$changed_paths"
  while IFS= read -r path; do
    if [[ "$path" != AGENTS.md && ! "$path" =~ ^src/$app_name([.]Domain)?/.+[.]cs$ && ! "$path" =~ ^src/$app_name([.]Domain)?/(.+/)?[.]gitkeep$ &&
      ! "$path" =~ ^tests/$app_name[.](Integration)?Tests/.+[.]cs$ && ! "$path" =~ ^tests/$app_name[.](Integration)?Tests/(.+/)?[.]gitkeep$ ]]; then
      fail "changed outside AGENTS.md and the C# sources of src/, tests/$app_name.Tests/ and tests/$app_name.IntegrationTests/: $path"
    fi
  done < "$changed_paths"
}

check_expected_elements() {
  local app="$1"
  local path
  local entry
  local markers

  for path in "${expected_files[@]}"; do
    [[ -f "$app/$path" ]] || fail "missing $path: the element is not at the path where its template generates it."
  done
  for entry in "${expected_contents[@]}"; do
    path="${entry%%|*}"
    grep -Eq -- "${entry#*|}" "$app/$path" || fail "$path does not match '${entry#*|}'."
  done
  if grep -Fq 'None yet' "$app/AGENTS.md"; then
    fail "the map of AGENTS.md still says that the solution has no module."
  fi
  if markers="$(grep -rn --include='*.cs' 'TODO(hardslice):' "$app/src" "$app/tests")"; then
    printf '%s\n' "$markers" >&2
    fail "a TODO(hardslice): marker is left."
  fi
  echo "    ${#expected_files[@]} files at their paths, registrations, event and reaction found, no TODO(hardslice): marker"
}

# Validates <candidate_app> against <generated_app>, the trusted solution as generated and never built from the candidate, then
# copies the accepted changes into <verification_root>/Shop, checks the expected elements there and runs its trusted check,
# and last checks the trace. Exits on any failure.
verify_candidate() {
  local generated_app="$1"
  local trusted_app="$2"
  local candidate_app="$3"
  local trace="$4"
  local verification_root="$5"
  local scratch="$verification_root/validation"
  local changed_paths="$scratch/changed-paths"
  local checked_app="$verification_root/$app_name"

  [[ -f "$trace" ]] || fail "no trace at $trace."
  mkdir -p "$scratch"
  collect_changed_paths "$generated_app" "$candidate_app" "$scratch" "$changed_paths"

  echo "==> Validating the candidate before executing any candidate content"
  validate_changed_paths "$trusted_app" "$changed_paths"
  validate_changed_sources "$candidate_app" "$changed_paths"

  copy_sources "$generated_app" "$checked_app"
  apply_candidate_changes "$checked_app" "$candidate_app" "$changed_paths"

  echo "==> Checking the elements of the request"
  check_expected_elements "$checked_app"

  echo "==> Running the trusted scripts/check.sh with the accepted changes"
  bash "$checked_app/scripts/check.sh"

  echo "==> Checking that the $("${trace_tool[@]}" format "$trace") trace runs the templates"
  "${trace_tool[@]}" expect "$trace" "${expected_commands[@]}"
  local loaded
  loaded="$("${trace_tool[@]}" skills "$trace" | LC_ALL=C sort -u | tr '\n' ' ')"
  echo "    skills loaded through the skill tool: ${loaded:-none}"

  echo "==> Changes accepted: $(wc -l < "$changed_paths") files"
}

verify() {
  work="$1"
  local trace="$2"
  [[ -d "$work" ]] || fail "no work directory at $work: run prepare first."
  work="$(cd "$work" && pwd)"
  trace="$(cd "$(dirname "$trace")" && pwd)/$(basename "$trace")"
  local candidate_app="$work/$app_name"
  [[ -d "$candidate_app" ]] || fail "no candidate application in $candidate_app: run prepare first."

  local verification_root
  verification_root="$(mktemp -d "$work/.verify.XXXXXX")"
  trap 'cleanup_verification_root "$verification_root"' EXIT

  echo "==> Regenerating a trusted solution for verification"
  # The trusted check runs dotnet hardslice from the trusted tree only, never from the package folder of the candidate.
  local trusted="$verification_root/trusted"
  mkdir -p "$trusted"
  generate_solution "$trusted"

  verify_candidate "$trusted/.generated/$app_name" "$trusted/$app_name" "$candidate_app" "$trace" "$trusted/check"
  echo "==> Verified: the request is delivered by the templates, the trusted check passes, no protected file was changed"

  cleanup_verification_root "$verification_root"
  trap - EXIT
}

# Keeps the changes of <dir>/Shop against its generated copy as an overlay, and the trace as agent-trace.py filter prints it:
# without the streaming events of Copilot, the inventory of the CLI and the encrypted reasoning blocks, and with the work
# directory and the home folder replaced, as the fixtures of the hooks do, in their path form and in the form of the project
# folder of Claude Code.
record() {
  work="$(cd "$1" && pwd)"
  local trace="$2"
  local run="$3"
  [[ "$run" =~ ^[a-z0-9-]+$ ]] || fail "a run is named in lowercase letters, digits and hyphens: $run"
  local generated_app="$work/.generated/$app_name"
  local candidate_app="$work/$app_name"
  [[ -d "$generated_app" && -d "$candidate_app" ]] || fail "$work was not prepared by $0 prepare."
  local run_dir="$runs/$run"
  [[ ! -e "$run_dir/changes" && ! -e "$run_dir/trace.jsonl" ]] || fail "$run_dir already holds a recorded run."
  mkdir -p "$run_dir/changes"

  local scratch
  scratch="$(mktemp -d)"
  collect_changed_paths "$generated_app" "$candidate_app" "$scratch" "$scratch/changed-paths"
  local path
  while IFS= read -r path; do
    if [[ -f "$candidate_app/$path" ]]; then
      mkdir -p "$(dirname "$run_dir/changes/$path")"
      cp "$candidate_app/$path" "$run_dir/changes/$path"
    else
      printf '%s\n' "$path" >> "$run_dir/changes.remove"
    fi
  done < "$scratch/changed-paths"
  rm -rf -- "${scratch:?}"

  "${trace_tool[@]}" filter "$trace" "$work" "$HOME" > "$run_dir/trace.jsonl"
  echo "==> Recorded $(find "$run_dir/changes" -type f | wc -l) changed files and the trace in $run_dir"
  echo "    Write its verdict (pass or fail) in $run_dir/verdict and its account in $run_dir/trace.md."
}

# The recorded changes stay as the agent wrote them, against the template of their run. <run>/changes.rename lists, one per line,
# what the template changed since then in the C# files of the changes, laid over <app>, before they are verified:
# - "<old> <new>", a pair of identifiers: a type of the generated solution that the template renamed, renamed in them;
# - "using <old> <new>", two namespaces: types that the template moved from <old> to <new>, so that each file with
#   "using <old>;" gets "using <new>;" after it.
apply_template_renames() {
  local run_dir="$1"
  local app="$2"
  local old
  local new
  local extra
  local more
  local path
  local namespace='^[A-Za-z_][A-Za-z0-9_]*([.][A-Za-z_][A-Za-z0-9_]*)*$'
  [[ -f "$run_dir/changes.rename" ]] || return 0
  while read -r old new extra more || [[ -n "$old" ]]; do
    [[ -n "$old" && "$old" != \#* ]] || continue
    if [[ "$old" == using ]]; then
      [[ "$new" =~ $namespace && "$extra" =~ $namespace && -z "$more" ]] ||
        fail "$run_dir/changes.rename: 'using $new $extra $more' is not a pair of namespaces."
      while IFS= read -r path; do
        OLD="$new" NEW="$extra" perl -0pi -e 's/^using \Q$ENV{OLD}\E;\n(?!(?s:.*)^using \Q$ENV{NEW}\E;$)/using $ENV{OLD};\nusing $ENV{NEW};\n/m' "$app/$path"
      done < <(cd "$run_dir/changes" && find . -type f -name '*.cs' | sed 's|^[.]/||' | LC_ALL=C sort)
      continue
    fi
    [[ "$old" =~ ^[A-Za-z_][A-Za-z0-9_]*$ && "$new" =~ ^[A-Za-z_][A-Za-z0-9_]*$ && -z "$extra" ]] ||
      fail "$run_dir/changes.rename: '$old $new $extra' is not a pair of identifiers."
    while IFS= read -r path; do
      OLD="$old" NEW="$new" perl -pi -e 's/\b\Q$ENV{OLD}\E\b/$ENV{NEW}/g' "$app/$path"
    done < <(cd "$run_dir/changes" && find . -type f -name '*.cs' | sed 's|^[.]/||' | LC_ALL=C sort)
  done < "$run_dir/changes.rename"
}

recorded_runs() {
  local run
  if [[ $# -gt 0 ]]; then
    for run in "$@"; do
      [[ -f "$runs/$run/trace.jsonl" && -f "$runs/$run/verdict" ]] || fail "no recorded run with a trace and a verdict in $runs/$run."
      printf '%s\n' "$run"
    done
    return
  fi
  find "$runs" -mindepth 2 -maxdepth 2 -name trace.jsonl -exec dirname {} \; | xargs -n 1 basename | LC_ALL=C sort
}

verify_recorded() {
  work="$(prepare_empty_dir "$1")"
  local selected
  selected="$(recorded_runs "${@:2}")"
  generate_solution "$work"
  local generated_app="$work/.generated/$app_name"
  local run
  local expected
  local actual
  local log

  for run in $selected; do
    expected="$(tr -d '[:space:]' < "$runs/$run/verdict")"
    [[ "$expected" == pass || "$expected" == fail ]] || fail "$runs/$run/verdict holds '$expected' instead of pass or fail."
    echo "==> $run: laying the recorded changes over the generated $app_name"
    copy_sources "$generated_app" "$work/$run/$app_name"
    apply_overlay "$runs/$run/changes" "$work/$run/$app_name"
    apply_template_renames "$runs/$run" "$work/$run/$app_name"
    log="$work/$run/verify.log"
    run_logged "$log" verify_candidate "$generated_app" "$work/$app_name" "$work/$run/$app_name" "$runs/$run/trace.jsonl" "$work/$run/check"
    if [[ "$verification_status" -eq 0 ]]; then
      actual=pass
    else
      actual=fail
    fi
    if [[ "$actual" != "$expected" ]]; then
      cat "$log" >&2
      fail "$run: verify reached '$actual' instead of its recorded verdict '$expected'."
    fi
    if [[ "$actual" == pass ]]; then
      grep -E '^    ' "$log" || true
      echo "==> $run: verified"
    else
      echo "==> $run: fails as recorded: $(grep '^error: ' "$log" | tail -n 1 || echo 'the trusted check failed')"
    fi
  done
  echo "==> Every recorded run reaches its verdict: ${selected//$'\n'/ }"
}

work=""
case "${1:-}" in
  prepare)
    [[ $# -eq 2 ]] || usage
    prepare "$2"
    ;;
  verify)
    [[ $# -eq 3 ]] || usage
    verify "$2" "$3"
    ;;
  record)
    [[ $# -eq 4 ]] || usage
    record "$2" "$3" "$4"
    ;;
  verify-recorded)
    [[ $# -ge 2 ]] || usage
    verify_recorded "${@:2}"
    ;;
  *)
    usage
    ;;
esac
