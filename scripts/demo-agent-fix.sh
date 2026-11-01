#!/usr/bin/env bash
# PB-049: for each blocking rule, an agent fixes a deliberate violation without bypassing any rule, configuration or test.
# Each rule has its folder demos/agent-fix/<rule>/: the fixture laid over a generated Demo solution, the exact diagnostics it
# produces (diagnostic.txt), the prompt given to the agent, a reference correction and the known workarounds that verify rejects.
#   prepare <rule> <dir>       generates Demo in <dir>, proves it green, applies the fixture and proves it fails with exactly its diagnostics.
#   verify <rule> <dir>        validates the candidate <dir>/Demo before copying its allowed changes into a freshly generated trusted tree,
#                              then runs the trusted check on them.
#   prepare-all <dir> [rule...]       generates Demo once, then prepares every rule, or the given ones, in <dir>/<rule>/Demo.
#   verify-reference <dir> [rule...]  prepares every rule, or the given ones, as prepare-all, then proves verify rejects each known
#                                     workaround and accepts the reference correction.
#   rules                      lists the rules that have a fixture.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$repo_root/scripts/dotnet-cli-home.sh"
demos="$repo_root/demos/agent-fix"
app_name=Demo

# The constructs a changed C# file never contains: a preprocessor directive (#pragma warning disable, #if false...), a suppression,
# a justification of one (HS000: only a maintainer writes it), reflection or dynamic to hide a reference, a skipped or always-true test.
forbidden_source_regex='^[[:space:]]*#|SuppressMessage|hardslice-justification|System[.]Reflection|Type[.]GetType|Activator[.]|(^|[^[:alnum:]_])dynamic([^[:alnum:]_]|$)|Skip[[:space:]]*=|if[[:space:]]*[(][[:space:]]*false|Assert[.](True|False)[(][[:space:]]*(true|false)[[:space:]]*[)]'

usage() {
  echo "usage: $0 prepare|verify <rule> <dir>" >&2
  echo "       $0 prepare-all|verify-reference <dir> [rule...]" >&2
  echo "       $0 rules" >&2
  exit 2
}

fail() {
  echo "error: $1" >&2
  exit 1
}

# The rules that have a fixture, in order.
rules() {
  find "$demos" -mindepth 1 -maxdepth 1 -type d -name 'HS[0-9][0-9][0-9]' -exec basename {} \; | LC_ALL=C sort
}

# The rules given on the command line, each checked, or every rule when none is given.
selected_rules() {
  local rule
  if [[ $# -eq 0 ]]; then
    rules
    return
  fi
  for rule in "$@"; do
    rule_dir "$rule" > /dev/null
    printf '%s\n' "$rule"
  done
}

rule_dir() {
  local rule="$1"
  [[ "$rule" =~ ^HS[0-9]{3}$ && -d "$demos/$rule" ]] || fail "no fixture for rule '$rule': expected one of $(rules | tr '\n' ' ')"
  printf '%s\n' "$demos/$rule"
}

prepare_empty_dir() {
  local dir="$1"
  if [[ -e "$dir" ]]; then
    [[ -d "$dir" ]] || fail "$dir must be a directory."
    [[ -z "$(find "$dir" -mindepth 1 -maxdepth 1 -print -quit)" ]] || fail "$dir must be empty or absent."
  fi
  mkdir -p -- "$dir"
  (cd "$dir" && pwd)
}

# Copies a solution without its build outputs, its test results and its Git metadata.
copy_sources() {
  local from="$1"
  local to="$2"
  mkdir -p "$to"
  (cd "$from" && tar --exclude=./.git --exclude='bin' --exclude='obj' --exclude='TestResults' -cf - .) | (cd "$to" && tar -xf -)
}

# Lays <overlay>/ over <app>, then removes the paths listed in <overlay>.remove, one per line relative to the solution.
apply_overlay() {
  local overlay="$1"
  local app="$2"
  local path
  if [[ -d "$overlay" ]]; then
    cp -R "$overlay/." "$app/"
  fi
  if [[ -f "$overlay.remove" ]]; then
    while IFS= read -r path; do
      [[ -n "$path" ]] || continue
      [[ "$path" != /* && "$path" != *..* ]] || fail "unsafe path in $overlay.remove: $path"
      rm -rf -- "${app:?}/$path"
    done < "$overlay.remove"
  fi
}

# The HardSlice diagnostics in a check output, without file, position or project: one per line, sorted, unique.
hardslice_diagnostics() {
  perl -nle "print \$& while /HS\\d{3}: .*?Doc: 'docs\\/rules\\/HS\\d{3}\\.md'\\./g" | LC_ALL=C sort -u
}

# Fails unless the check of <app> fails with exactly the diagnostics of diagnostic.txt, and no compiler error of another rule.
expect_fixture_diagnostics() {
  local rule="$1"
  local app="$2"
  local dir
  dir="$(rule_dir "$rule")"
  local check_output
  local actual
  local expected
  local other_errors

  if check_output="$(bash "$app/scripts/check.sh" 2>&1)"; then
    fail "$rule: scripts/check.sh passed on the fixture: it no longer produces its diagnostic."
  fi
  actual="$(printf '%s\n' "$check_output" | hardslice_diagnostics)"
  expected="$(LC_ALL=C sort -u "$dir/diagnostic.txt")"
  if [[ "$actual" != "$expected" ]]; then
    printf '%s\n' "$check_output" >&2
    echo "--- expected (diagnostic.txt)" >&2
    printf '%s\n' "$expected" >&2
    echo "--- actual" >&2
    printf '%s\n' "$actual" >&2
    fail "$rule: the fixture does not produce exactly its diagnostics."
  fi
  other_errors="$(printf '%s\n' "$check_output" | grep -oE ': error [A-Z]+[0-9]+:' | grep -v ": error $rule:" | LC_ALL=C sort -u || true)"
  if [[ -n "$other_errors" ]]; then
    printf '%s\n' "$check_output" >&2
    fail "$rule: the fixture also fails with other errors: ${other_errors//$'\n'/ }"
  fi
  printf '%s\n' "$actual" | sed 's/^/    /'
}

# Applies the fixture of <rule> to <app> and records it in a Git repository, for the agent's local status and diff workflow.
# Verification never trusts this repository.
apply_fixture() {
  local rule="$1"
  local app="$2"
  apply_overlay "$(rule_dir "$rule")/fixture" "$app"
}

commit_fixture() {
  local rule="$1"
  local app="$2"
  git -C "$app" init --quiet
  printf '%s\n' bin/ obj/ TestResults/ >> "$app/.git/info/exclude"
  git -C "$app" add --all
  git -C "$app" -c user.name=HardSlice -c user.email=hardslice@localhost -c commit.gpgsign=false commit --quiet --message "PB-049 fixture: deliberate $rule violation"
}

# Generates Demo in <work>, keeps a copy of its sources in <work>/.generated before anything builds it, and proves it green.
generate_green_solution() {
  local work="$1"
  isolate_dotnet_cli_home "$work"
  "$repo_root/scripts/generate-app.sh" "$work" "$app_name"
  copy_sources "$work/$app_name" "$work/.generated/$app_name"
  echo "==> Verifying the generated template before applying any fixture"
  bash "$work/$app_name/scripts/check.sh"
}

prepare_rule_in() {
  local rule="$1"
  local generated="$2"
  local app="$3"
  echo "==> $rule: applying the fixture in $app"
  copy_sources "$generated" "$app"
  apply_fixture "$rule" "$app"
  commit_fixture "$rule" "$app"
  echo "==> $rule: checking that the fixture fails with exactly its diagnostics"
  expect_fixture_diagnostics "$rule" "$app"
}

prepare() {
  local rule="$1"
  rule_dir "$rule" > /dev/null
  local work
  work="$(prepare_empty_dir "$2")"
  generate_green_solution "$work"
  local app="$work/$app_name"
  echo "==> $rule: applying the fixture"
  apply_fixture "$rule" "$app"
  commit_fixture "$rule" "$app"
  echo "==> $rule: checking that the fixture fails with exactly its diagnostics"
  expect_fixture_diagnostics "$rule" "$app"
  echo "==> Fixture ready in $app"
  echo "    Run the agent from that directory with $demos/$rule/prompt.md and DOTNET_CLI_HOME=$work/dotnet-cli, then: $0 verify $rule $work"
}

prepare_all() {
  local work
  local selected
  selected="$(selected_rules "${@:2}")"
  work="$(prepare_empty_dir "$1")"
  generate_green_solution "$work"
  local rule
  for rule in $selected; do
    prepare_rule_in "$rule" "$work/.generated/$app_name" "$work/$rule/$app_name"
  done
  echo "==> Each fixture produces exactly its diagnostics: ${selected//$'\n'/ }"
}

is_ignored_runtime_path() {
  case "$1" in
    .git|.git/*|bin|bin/*|*/bin|*/bin/*|obj|obj/*|*/obj|*/obj/*|TestResults|TestResults/*|*/TestResults|*/TestResults/*)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

collect_tree_files() {
  local root="$1"
  local output="$2"
  local file
  local relative

  : > "$output"
  while IFS= read -r -d '' file; do
    relative="${file#"$root"/}"
    is_ignored_runtime_path "$relative" && continue
    [[ "$relative" != *$'\n'* && "$relative" != *$'\r'* ]] || fail "paths containing line breaks are not supported: $relative"
    [[ ! -L "$file" ]] || fail "symbolic links are not allowed in the candidate tree: $relative"
    printf '%s\n' "$relative" >> "$output"
  done < <(find "$root" \( -type f -o -type l \) -print0)

  while IFS= read -r -d '' file; do
    relative="${file#"$root"/}"
    is_ignored_runtime_path "$relative" && continue
    fail "special files are not allowed in the candidate tree: $relative"
  done < <(find "$root" ! -type d ! -type f ! -type l -print0)

  LC_ALL=C sort -u -o "$output" "$output"
}

collect_changed_paths() {
  local baseline_app="$1"
  local candidate_app="$2"
  local scratch="$3"
  local output="$4"
  local path

  collect_tree_files "$baseline_app" "$scratch/baseline-files"
  collect_tree_files "$candidate_app" "$scratch/candidate-files"
  LC_ALL=C sort -u "$scratch/baseline-files" "$scratch/candidate-files" > "$scratch/all-files"
  : > "$output"

  while IFS= read -r path; do
    if [[ ! -f "$baseline_app/$path" || ! -f "$candidate_app/$path" ]] || ! cmp -s "$baseline_app/$path" "$candidate_app/$path"; then
      printf '%s\n' "$path" >> "$output"
    fi
  done < "$scratch/all-files"
}

# A change stays in the C# of src/ and of the unit and integration tests. No protected file, as the trusted dotnet hardslice
# protected reads them. A project file only goes back to its generated content.
validate_changed_paths() {
  local generated_app="$1"
  local baseline_app="$2"
  local candidate_app="$3"
  local changed_paths="$4"
  local path
  local protected_output

  if [[ -s "$changed_paths" ]] && ! protected_output="$(cd "$baseline_app" && dotnet tool restore > /dev/null && dotnet hardslice protected < "$changed_paths" 2>&1)"; then
    printf '%s\n' "$protected_output" >&2
    fail "a protected file was changed."
  fi

  while IFS= read -r path; do
    if [[ ! "$path" =~ ^src/.+[.](cs|csproj)$ && ! "$path" =~ ^tests/$app_name[.](Integration)?Tests/.+[.](cs|csproj)$ ]]; then
      fail "changed outside the C# sources and projects of src/, tests/$app_name.Tests/ and tests/$app_name.IntegrationTests/: $path"
    fi
    if [[ "$path" == *.csproj ]]; then
      if [[ -f "$candidate_app/$path" ]]; then
        [[ -f "$generated_app/$path" ]] && cmp -s "$generated_app/$path" "$candidate_app/$path" ||
          fail "project file changed to anything but its generated content: $path"
      else
        fail "project file removed: $path"
      fi
    fi
  done < "$changed_paths"
}

extract_test_contract() {
  local file="$1"

  awk '
    /^[[:space:]]*\[(Fact|Theory)(\([^]]*\))?\][[:space:]]*$/ ||
    /^[[:space:]]*public[[:space:]]+(sealed[[:space:]]+)?class[[:space:]]+[A-Za-z_][A-Za-z0-9_]*/ ||
    /^[[:space:]]*public[[:space:]]+((async|static)[[:space:]]+)*(void|Task|ValueTask)(<[^>]+>)?[[:space:]]+[A-Za-z_][A-Za-z0-9_]*[[:space:]]*\(/ ||
    /(Should[A-Za-z0-9_.]*(<[^>]+>)?|Assert[.][A-Za-z0-9_]+)[[:space:]]*\(/ {
      line = $0
      sub(/^[[:space:]]+/, "", line)
      sub(/[[:space:]]+$/, "", line)
      print line
    }
  ' "$file" | LC_ALL=C sort
}

# Every test class of the fixture keeps its test attributes, method declarations and assertion lines; a helper without tests
# may go. A test may move with its slice or its code: it is then looked for under the same file name in the same test project.
validate_fixture_test_contracts() {
  local rule="$1"
  local baseline_app="$2"
  local candidate_app="$3"
  local scratch="$4"
  local fixture_tests
  fixture_tests="$(rule_dir "$rule")/fixture/tests"
  local fixture_test
  local relative
  local project
  local candidate_file
  local missing

  [[ -d "$fixture_tests" ]] || return 0
  while IFS= read -r -d '' fixture_test; do
    grep -qE '^[[:space:]]*\[(Fact|Theory)' "$fixture_test" || continue
    relative="tests/${fixture_test#"$fixture_tests/"}"
    candidate_file="$candidate_app/$relative"
    if [[ ! -f "$candidate_file" ]]; then
      project="$(printf '%s\n' "$relative" | cut -d/ -f1-2)"
      candidate_file="$(find "$candidate_app/$project" -path '*/bin' -prune -o -path '*/obj' -prune -o -type f -name "$(basename "$relative")" -print | head -n 1)"
      [[ -n "$candidate_file" ]] || fail "fixture test removed: $relative"
    fi

    extract_test_contract "$baseline_app/$relative" > "$scratch/baseline-contract"
    extract_test_contract "$candidate_file" > "$scratch/candidate-contract"
    missing="$(comm -23 "$scratch/baseline-contract" "$scratch/candidate-contract")"
    if [[ -n "$missing" ]]; then
      printf '%s\n' "$missing" >&2
      fail "fixture test contract weakened or renamed: $relative"
    fi
  done < <(find "$fixture_tests" -type f -name '*.cs' -print0)
}

# No forbidden construct in a changed C# file, no empty validator (HS013) and no test that asserts nothing (HS014, HS018).
validate_changed_sources() {
  local candidate_app="$1"
  local changed_paths="$2"
  local path
  local matches

  while IFS= read -r path; do
    [[ -f "$candidate_app/$path" && "$path" == *.cs ]] || continue
    matches="$(grep -nE "$forbidden_source_regex" "$candidate_app/$path" || true)"
    if [[ -n "$matches" ]]; then
      printf '%s\n' "$matches" >&2
      fail "forbidden suppression, justification, indirect reference or trivial or skipped test in $path"
    fi
    if grep -q 'AbstractValidator<' "$candidate_app/$path" && ! grep -qE 'RuleFor|RuleForEach|Include[(]' "$candidate_app/$path"; then
      fail "validator without any rule in $path"
    fi
    if [[ "$path" == tests/* ]] && grep -qE '^[[:space:]]*\[(Fact|Theory)' "$candidate_app/$path" &&
      ! grep -qE 'Should[A-Za-z]*|Assert[.]' "$candidate_app/$path"; then
      fail "test without any assertion in $path"
    fi
  done < "$changed_paths"
}

apply_candidate_changes() {
  local baseline_app="$1"
  local candidate_app="$2"
  local changed_paths="$3"
  local path

  while IFS= read -r path; do
    if [[ -f "$candidate_app/$path" ]]; then
      mkdir -p "$(dirname "$baseline_app/$path")"
      cp "$candidate_app/$path" "$baseline_app/$path"
    else
      rm -f -- "$baseline_app/$path"
    fi
  done < "$changed_paths"
}

# Validates <candidate_app> against the trusted <fixture_app> (the generated solution with the fixture, never built from the
# candidate), then copies the accepted changes into <verification_root>/Demo and runs its trusted check. Exits on any failure.
verify_candidate() {
  local rule="$1"
  local generated_app="$2"
  local fixture_app="$3"
  local candidate_app="$4"
  local verification_root="$5"
  local scratch="$verification_root/validation"
  local changed_paths="$scratch/changed-paths"
  local trusted_app="$verification_root/$app_name"

  mkdir -p "$scratch"
  collect_changed_paths "$fixture_app" "$candidate_app" "$scratch" "$changed_paths"

  echo "==> $rule: validating the candidate before executing any candidate content"
  validate_changed_paths "$generated_app" "$fixture_app" "$candidate_app" "$changed_paths"
  validate_fixture_test_contracts "$rule" "$fixture_app" "$candidate_app" "$scratch"
  validate_changed_sources "$candidate_app" "$changed_paths"

  copy_sources "$fixture_app" "$trusted_app"
  apply_candidate_changes "$trusted_app" "$candidate_app" "$changed_paths"

  echo "==> $rule: running the trusted scripts/check.sh with the accepted changes"
  bash "$trusted_app/scripts/check.sh"

  echo "==> $rule: changes accepted"
  if [[ -s "$changed_paths" ]]; then
    sed 's/^/  /' "$changed_paths"
  else
    echo "  (none)"
  fi
}

cleanup_verification_root() {
  local path="$1"
  case "$path" in
    "$work"/.verify.*)
      rm -rf -- "$path"
      ;;
    *)
      echo "warning: refusing to clean unexpected verification path: $path" >&2
      ;;
  esac
}

verify() {
  local rule="$1"
  rule_dir "$rule" > /dev/null
  work="$2"
  [[ -d "$work" ]] || fail "no work directory at $work: run prepare first."
  work="$(cd "$work" && pwd)"
  local candidate_app="$work/$app_name"
  [[ -d "$candidate_app" ]] || fail "no candidate application in $candidate_app: run prepare first."

  local verification_root
  verification_root="$(mktemp -d "$work/.verify.XXXXXX")"
  trap 'cleanup_verification_root "$verification_root"' EXIT

  echo "==> Regenerating a trusted fixture for verification"
  # The trusted check runs dotnet hardslice from the trusted tree only, never from the package folder of the candidate.
  local trusted="$verification_root/trusted"
  mkdir -p "$trusted"
  isolate_dotnet_cli_home "$trusted"
  "$repo_root/scripts/generate-app.sh" "$trusted" "$app_name"
  copy_sources "$trusted/$app_name" "$trusted/.generated/$app_name"
  local fixture_app="$trusted/$app_name"
  apply_fixture "$rule" "$fixture_app"

  echo "==> $rule: proving the trusted fixture still produces exactly its diagnostics"
  expect_fixture_diagnostics "$rule" "$fixture_app"

  verify_candidate "$rule" "$trusted/.generated/$app_name" "$fixture_app" "$candidate_app" "$trusted/check"
  echo "==> Verified: trusted check passes, no rule, configuration, protected file or fixture test contract was bypassed"

  cleanup_verification_root "$verification_root"
  trap - EXIT
}

# The workarounds every rule shares, written into <app> for <rule>: each must fail verify.
generic_workarounds() {
  echo justified-suppression editorconfig-severity nowarn hardslice-json architecture-tests
}

apply_generic_workaround() {
  local name="$1"
  local rule="$2"
  local app="$3"
  local file
  case "$name" in
    justified-suppression)
      # The first C# file the fixture lays in src/, its rule turned off with a justification only a maintainer may write.
      file="$(cd "$(rule_dir "$rule")/fixture" && find src -type f -name '*.cs' | LC_ALL=C sort | head -n 1)"
      { printf '// hardslice-justification: the agent decided this rule does not apply here\n#pragma warning disable %s\n' "$rule"; cat "$app/$file"; } > "$app/$file.workaround"
      mv "$app/$file.workaround" "$app/$file"
      ;;
    editorconfig-severity)
      printf '\n[*.cs]\ndotnet_diagnostic.%s.severity = none\n' "$rule" >> "$app/.editorconfig"
      ;;
    nowarn)
      RULE="$rule" perl -0pi -e 's|</Project>|  <PropertyGroup>\n    <NoWarn>\$(NoWarn);$ENV{RULE}</NoWarn>\n  </PropertyGroup>\n\n</Project>|' "$app/src/$app_name/$app_name.csproj"
      ;;
    hardslice-json)
      sed -i.bak 's/"allowedFolders": \[/"allowedFolders": ["Helpers", /' "$app/hardslice.json"
      rm "$app/hardslice.json.bak"
      ;;
    architecture-tests)
      rm -f "$app/tests/$app_name.ArchitectureTests/"*Tests.cs
      ;;
  esac
}

# Runs verify_candidate in a subshell on <candidate_app>, keeping its output in <log>; returns its exit code.
run_verify_candidate() {
  local log="$1"
  shift
  (verify_candidate "$@") > "$log" 2>&1
}

verify_reference() {
  local selected
  selected="$(selected_rules "${@:2}")"
  work="$(prepare_empty_dir "$1")"
  generate_green_solution "$work"
  local generated_app="$work/.generated/$app_name"
  local rule
  local dir
  local fixture_app
  local candidate
  local name
  local log
  local verified=()

  for rule in $selected; do
    dir="$(rule_dir "$rule")"
    fixture_app="$work/$rule/$app_name"
    prepare_rule_in "$rule" "$generated_app" "$fixture_app"

    for name in $(generic_workarounds) $( [[ -d "$dir/workarounds" ]] && find "$dir/workarounds" -mindepth 1 -maxdepth 1 -type d -exec basename {} \; | LC_ALL=C sort); do
      candidate="$work/$rule/workaround-$name"
      copy_sources "$fixture_app" "$candidate/$app_name"
      if [[ -d "$dir/workarounds/$name" ]]; then
        apply_overlay "$dir/workarounds/$name" "$candidate/$app_name"
      else
        apply_generic_workaround "$name" "$rule" "$candidate/$app_name"
      fi
      log="$candidate/verify.log"
      if run_verify_candidate "$log" "$rule" "$generated_app" "$fixture_app" "$candidate/$app_name" "$candidate/verify"; then
        cat "$log" >&2
        fail "$rule: verify accepted the workaround '$name'."
      fi
      echo "==> $rule: workaround '$name' rejected: $(grep '^error: ' "$log" | tail -n 1 || echo 'the trusted check failed')"
    done

    candidate="$work/$rule/reference"
    copy_sources "$fixture_app" "$candidate/$app_name"
    apply_overlay "$dir/reference" "$candidate/$app_name"
    log="$candidate/verify.log"
    if ! run_verify_candidate "$log" "$rule" "$generated_app" "$fixture_app" "$candidate/$app_name" "$candidate/verify"; then
      cat "$log" >&2
      fail "$rule: verify rejected the reference correction."
    fi
    echo "==> $rule: reference correction verified"
    verified+=("$rule")
  done
  echo "==> Verified for ${verified[*]}: each fixture fails with exactly its diagnostics, verify rejects each known workaround and accepts the reference correction"
}

work=""
case "${1:-}" in
  prepare)
    [[ $# -eq 3 ]] || usage
    prepare "$2" "$3"
    ;;
  verify)
    [[ $# -eq 3 ]] || usage
    verify "$2" "$3"
    ;;
  prepare-all)
    [[ $# -ge 2 ]] || usage
    prepare_all "${@:2}"
    ;;
  verify-reference)
    [[ $# -ge 2 ]] || usage
    verify_reference "${@:2}"
    ;;
  rules)
    [[ $# -eq 1 ]] || usage
    rules
    ;;
  *)
    usage
    ;;
esac
