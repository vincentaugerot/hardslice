#!/usr/bin/env bash
# Runs a command, records its full output and true exit code, then exits with that code.
# Usage: run-logged.sh <absolute-log-path> -- <command> [args...]
#
# <log> and <log>.exit are created exclusively (mode 0600 through umask 077) under a canonical parent
# directory: no symlink in any ancestor, no "." or "..". Existing paths and symlinks are refused, so a log
# or exit code from an earlier attempt is never overwritten or reused.
# The command writes through a pipe consumed by the wrapper, so inherited stdout/stderr from asynchronous
# descendants must close before the footer is written. The header records the log's device:inode; the log
# identity and the header are checked again after the command, before and after the footer, and before the
# .exit sidecar is trusted.
# Exit codes: the command's own code when everything was recorded; 64 on usage error; 70 on a preflight
# error (the command is not run) and on ANY recording or integrity failure after the command ran, whatever
# the command returned. The command's code is then still written to stderr and, when possible, to the
# footer, but it must not be used as a result.
# This guards against accidents, not against a hostile command running under the same UID (decision D-18).
set -u
umask 077

usage() {
  echo "usage: run-logged.sh <absolute-log-path> -- <command> [args...]" >&2
  exit 64
}

preflight_fail() {
  echo "run-logged.sh: preflight: $1" >&2
  exit 70
}

record_fail() {
  echo "run-logged.sh: recording failed: $1 (command exit code: $status, not usable as a result)" >&2
  exit 70
}

if stat -c '%d:%i' / > /dev/null 2>&1; then
  file_id() { stat -c '%d:%i' -- "$1" 2> /dev/null; }
else
  file_id() { stat -f '%d:%i' -- "$1" 2> /dev/null; }
fi

if [ "$#" -lt 3 ] || [ "$2" != "--" ]; then
  usage
fi
log=$1
exit_file=$log.exit
shift 2

case "$log" in
  /*) ;;
  *) usage ;;
esac
case "$log" in
  */) usage ;;
esac

parent=$(dirname -- "$log")
if [ ! -d "$parent" ]; then
  preflight_fail "parent directory does not exist: $parent"
fi
if [ "$(cd -- "$parent" 2> /dev/null && pwd -P)" != "$parent" ]; then
  preflight_fail "parent directory is not canonical (symlink, '.' or '..' in its path): $parent"
fi
for path in "$log" "$exit_file"; do
  if [ -e "$path" ] || [ -L "$path" ]; then
    preflight_fail "refusing existing path or symlink: $path"
  fi
done

# noclobber makes bash create files with O_EXCL: an existing path makes the redirection fail.
set -o noclobber
if ! exec 3>"$log"; then
  preflight_fail "cannot create log exclusively: $log"
fi
log_id=$(file_id "$log")
if [ -z "$log_id" ]; then
  exec 3>&-
  preflight_fail "cannot read log identity: $log"
fi

header="# start: $(date -u +%Y-%m-%dT%H:%M:%SZ)"$'\n'"# log-id: $log_id"$'\n'"# cmd:$(printf ' %q' "$@")"$'\n'
header_bytes=$(printf '%s' "$header" | wc -c | tr -d ' ')
header_intact() {
  [ "$(file_id "$log")" = "$log_id" ] && [ ! -L "$log" ] \
    && head -c "$header_bytes" -- "$log" | cmp -s - <(printf '%s' "$header")
}
if ! printf '%s' "$header" >&3 || ! header_intact; then
  exec 3>&-
  preflight_fail "cannot write or verify log header: $log"
fi

# The command never inherits fd 3. Its combined stdout/stderr goes through cat; the pipeline does not
# finish until every descendant that inherited the pipe has closed it. Read PIPESTATUS immediately.
"$@" 3>&- 2>&1 | /bin/cat >&3
pipeline_status=("${PIPESTATUS[@]}")
status=${pipeline_status[0]}
capture_status=${pipeline_status[1]}
if [ "$capture_status" -ne 0 ]; then
  exec 3>&-
  record_fail "cannot capture complete command output"
fi

if ! header_intact; then
  printf '\n# end: %s\n# exit: %d\n# record: FAILED (log replaced or header altered)\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$status" >&3 2> /dev/null
  exec 3>&-
  record_fail "log replaced, moved or header altered: $log"
fi
if [ -e "$exit_file" ] || [ -L "$exit_file" ]; then
  exec 3>&-
  record_fail "exit file appeared while the command ran: $exit_file"
fi

footer="# end: $(date -u +%Y-%m-%dT%H:%M:%SZ)"$'\n'"# exit: $status"$'\n'
if [ -n "$(tail -c 1 -- "$log")" ]; then
  footer=$'\n'"$footer"
fi
if ! printf '%s' "$footer" >&3; then
  exec 3>&-
  record_fail "cannot write log footer: $log"
fi
exec 3>&-
if ! header_intact || [ "$(tail -n 1 -- "$log")" != "# exit: $status" ]; then
  record_fail "log footer missing or log altered after close: $log"
fi

if ! printf '%d\n' "$status" > "$exit_file"; then
  record_fail "cannot create exit file exclusively: $exit_file"
fi
if [ -L "$exit_file" ] || [ ! -f "$exit_file" ] || [ "$(cat -- "$exit_file")" != "$status" ] || ! header_intact; then
  record_fail "exit file or log inconsistent after recording: $exit_file"
fi

exit "$status"
