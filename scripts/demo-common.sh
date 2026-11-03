# Sourced by demo-agent-fix.sh (PB-049) and demo-add-feature.sh (PB-050): the validation of a candidate solution left by an agent,
# which never executes anything from it. Each verifier compares the candidate with a trusted solution it regenerated itself, rejects
# forbidden changes, then copies only the accepted changes into a clean tree before building and testing it.

# The constructs a changed C# file never contains: a preprocessor directive (#pragma warning disable, #if false...), a suppression,
# a justification of one (HS000: only a maintainer writes it), reflection or dynamic to hide a reference, a skipped or always-true test.
forbidden_source_regex='^[[:space:]]*#|SuppressMessage|hardslice-justification|System[.]Reflection|Type[.]GetType|Activator[.]|(^|[^[:alnum:]_])dynamic([^[:alnum:]_]|$)|Skip[[:space:]]*=|if[[:space:]]*[(][[:space:]]*false|Assert[.](True|False)[(][[:space:]]*(true|false)[[:space:]]*[)]'

fail() {
  echo "error: $1" >&2
  exit 1
}

# Runs <command...> in a subshell with its output in <log>, and sets verification_status to its exit code. Bash ignores set -e in
# every command an if, a while, a ! or a && or || list tests, the functions it calls and their subshells included: a verifier tested
# that way would carry on past a failed step, such as a failed trusted check, and succeed. Call it as a plain command, never as a
# condition, then test verification_status.
run_logged() {
  local log="$1"
  shift
  set +e
  (set -e; "$@") > "$log" 2>&1
  verification_status=$?
  set -e
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

# Fails when one of the changed paths is protected, as the trusted dotnet hardslice protected of <trusted_app> reads them.
fail_on_protected_paths() {
  local trusted_app="$1"
  local changed_paths="$2"
  local protected_output

  if [[ -s "$changed_paths" ]] && ! protected_output="$(cd "$trusted_app" && dotnet tool restore > /dev/null && dotnet hardslice protected < "$changed_paths" 2>&1)"; then
    printf '%s\n' "$protected_output" >&2
    fail "a protected file was changed."
  fi
}

# No forbidden construct in a changed C# file, no empty validator (HS013) and no test that asserts nothing (HS014, HS018).
# The only directives allowed are those dotnet ef writes in a migration, its designer and the model snapshot: #nullable disable,
# and the #pragma warning disable and restore of the obsolete warnings CS0612 and CS0618, never a HardSlice rule.
validate_changed_sources() {
  local candidate_app="$1"
  local changed_paths="$2"
  local path
  local matches

  while IFS= read -r path; do
    [[ -f "$candidate_app/$path" && "$path" == *.cs ]] || continue
    matches="$(grep -nE "$forbidden_source_regex" "$candidate_app/$path" || true)"
    if [[ "$path" == src/*/Shared/Persistence/Migrations/*.cs ]]; then
      matches="$(printf '%s\n' "$matches" | grep -vE '^[0-9]+:(#nullable disable|[[:space:]]*#pragma warning (disable|restore) 612, 618)[[:space:]]*$' || true)"
    fi
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

# Removes a verification folder that the calling script created under its work directory $work, and nothing else.
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
