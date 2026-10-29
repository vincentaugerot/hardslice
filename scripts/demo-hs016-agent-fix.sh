#!/usr/bin/env bash
# PB-008: an agent fixes a deliberate HS016 violation without bypassing any rule, configuration or test.
#   prepare <dir>  proves the generated template is green, applies the fixture and proves the expected HS016 is active.
#   verify <dir>   validates the candidate before copying its allowed C# changes into a freshly generated trusted tree.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
demo="$repo_root/demos/hs016-agent-fix"
expected_hs016="error HS016: 'CancelBookingHandler' in slice 'Bookings/CancelBooking' uses 'BookingSchedule' from slice 'Bookings/ConfirmBooking'. Slices never reference each other. Fix: move the shared rule into a domain aggregate, raise a domain event, or use the public contract of module 'Bookings'. Doc: 'docs/rules/HS016.md'."
forbidden_source_regex='^[[:space:]]*#|SuppressMessage|System[.]Reflection|Type[.]GetType|Activator[.]|(^|[^[:alnum:]_])dynamic([^[:alnum:]_]|$)|Skip[[:space:]]*=|if[[:space:]]*[(][[:space:]]*false'

usage() {
  echo "usage: $0 prepare|verify <dir>" >&2
  exit 2
}

fail() {
  echo "error: $1" >&2
  exit 1
}

apply_fixture() {
  local app="$1"
  cp -R "$demo/fixture/." "$app/"
}

expect_fixture_hs016() {
  local app="$1"
  local check_output

  if check_output="$(bash "$app/scripts/check.sh" 2>&1)"; then
    fail "scripts/check.sh passed on the fixture: it no longer contains the expected HS016 violation."
  fi
  if ! printf '%s\n' "$check_output" | grep -Fq "$expected_hs016"; then
    printf '%s\n' "$check_output" >&2
    fail "scripts/check.sh failed without the expected CancelBookingHandler to BookingSchedule HS016 diagnostic."
  fi

  printf '%s\n' "$check_output" |
    grep -F "$expected_hs016" |
    sed 's/ \[.*\]$//' |
    sort -u
}

prepare() {
  if [[ -e "$work" ]]; then
    [[ -d "$work" ]] || fail "$work must be a directory."
    [[ -z "$(find "$work" -mindepth 1 -maxdepth 1 -print -quit)" ]] || fail "$work must be empty or absent."
  fi
  mkdir -p -- "$work"
  work="$(cd "$work" && pwd)"

  "$repo_root/scripts/generate-app.sh" "$work"
  local app="$work/Demo"

  echo "==> Verifying the generated template before applying the fixture"
  bash "$app/scripts/check.sh"

  echo "==> Applying the fixture"
  apply_fixture "$app"

  # Git is provided only for the agent's local status and diff workflow. Verification never trusts this repository.
  git -C "$app" init --quiet
  printf '%s\n' bin/ obj/ TestResults/ >> "$app/.git/info/exclude"
  git -C "$app" add --all
  git -C "$app" -c user.name=HardSlice -c user.email=hardslice@localhost commit --quiet --message "PB-008 fixture: deliberate HS016 violation"

  echo "==> Checking that the fixture fails on the expected HS016"
  expect_fixture_hs016 "$app"

  echo "==> Fixture ready in $app"
  echo "    Run the agent from that directory with $demo/prompt.md, then: $0 verify $work"
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

validate_changed_paths() {
  local changed_paths="$1"
  local path

  while IFS= read -r path; do
    if [[ ! "$path" =~ ^src/.+[.]cs$ && ! "$path" =~ ^tests/Demo[.](Integration)?Tests/.+[.]cs$ ]]; then
      fail "changed outside C# sources of src/, tests/Demo.Tests/ and tests/Demo.IntegrationTests/: $path"
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

validate_fixture_test_contracts() {
  local baseline_app="$1"
  local candidate_app="$2"
  local scratch="$3"
  local fixture_test
  local relative
  local missing

  while IFS= read -r -d '' fixture_test; do
    relative="${fixture_test#"$demo/fixture/"}"
    [[ -f "$candidate_app/$relative" ]] || fail "fixture test removed: $relative"

    extract_test_contract "$baseline_app/$relative" > "$scratch/baseline-contract"
    extract_test_contract "$candidate_app/$relative" > "$scratch/candidate-contract"
    missing="$(comm -23 "$scratch/baseline-contract" "$scratch/candidate-contract")"
    if [[ -n "$missing" ]]; then
      printf '%s\n' "$missing" >&2
      fail "fixture test contract weakened or renamed: $relative"
    fi
  done < <(find "$demo/fixture/tests" -type f -name '*.cs' -print0)
}

validate_changed_sources() {
  local candidate_app="$1"
  local changed_paths="$2"
  local path
  local matches

  while IFS= read -r path; do
    [[ -f "$candidate_app/$path" ]] || continue
    matches="$(grep -nE "$forbidden_source_regex" "$candidate_app/$path" || true)"
    if [[ -n "$matches" ]]; then
      printf '%s\n' "$matches" >&2
      fail "forbidden suppression, indirect reference or skipped test in $path"
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

cleanup_verification_root() {
  local path="$1"

  case "$path" in
    "$work"/.hs016-verify.*)
      rm -rf -- "$path"
      ;;
    *)
      echo "warning: refusing to clean unexpected verification path: $path" >&2
      ;;
  esac
}

verify() {
  [[ -d "$work" ]] || fail "no work directory at $work: run prepare first."
  work="$(cd "$work" && pwd)"
  local candidate_app="$work/Demo"
  [[ -d "$candidate_app" ]] || fail "no candidate application in $candidate_app: run prepare first."

  local verification_root
  verification_root="$(mktemp -d "$work/.hs016-verify.XXXXXX")"
  trap 'cleanup_verification_root "$verification_root"' EXIT

  echo "==> Regenerating a trusted fixture for verification"
  "$repo_root/scripts/generate-app.sh" "$verification_root"
  local baseline_app="$verification_root/Demo"
  apply_fixture "$baseline_app"

  echo "==> Proving the trusted fixture still produces the expected HS016"
  expect_fixture_hs016 "$baseline_app"

  local scratch="$verification_root/validation"
  local changed_paths="$scratch/changed-paths"
  mkdir -p "$scratch"
  collect_changed_paths "$baseline_app" "$candidate_app" "$scratch" "$changed_paths"

  echo "==> Validating the candidate before executing any candidate content"
  validate_changed_paths "$changed_paths"
  validate_fixture_test_contracts "$baseline_app" "$candidate_app" "$scratch"
  validate_changed_sources "$candidate_app" "$changed_paths"
  apply_candidate_changes "$baseline_app" "$candidate_app" "$changed_paths"

  echo "==> Running the trusted scripts/check.sh with the accepted C# changes"
  bash "$baseline_app/scripts/check.sh"

  echo "==> Changes accepted from the agent"
  if [[ -s "$changed_paths" ]]; then
    sed 's/^/  /' "$changed_paths"
  else
    echo "  (none)"
  fi
  echo "==> Verified: trusted check passes, no rule, configuration or fixture test contract was bypassed"

  cleanup_verification_root "$verification_root"
  trap - EXIT
}

[[ $# -eq 2 ]] || usage
work="$2"
case "$1" in
  prepare) prepare ;;
  verify) verify ;;
  *) usage ;;
esac
