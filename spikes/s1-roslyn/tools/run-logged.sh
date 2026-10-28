#!/usr/bin/env bash
# Runs a command, records its full output and true exit code, then exits with that code.
# Usage: run-logged.sh <log-file> -- <command> [args...]
#
# <log-file> and <log-file>.exit are created exclusively (mode 0600 through umask 077). Existing paths
# and symlinks are refused, so a log or exit code from an earlier attempt is never overwritten or reused.
# Exit codes: the command's own code; 64 on usage error; 70 on a preflight error (the command is not
# run); after the command ran, a recording failure keeps the command's code, or 70 if that code was 0.
# The wrapper never judges the code: comparing it with the expected outcome is the series script's job.
set -u
umask 077

usage() {
  echo "usage: run-logged.sh <log-file> -- <command> [args...]" >&2
  exit 64
}

preflight_fail() {
  echo "run-logged.sh: $1" >&2
  exit 70
}

record_fail() {
  echo "run-logged.sh: $1 (command exit code: $status)" >&2
  if [ "$status" -ne 0 ]; then
    exit "$status"
  fi
  exit 70
}

if [ "$#" -lt 3 ] || [ "$2" != "--" ]; then
  usage
fi
log=$1
exit_file=$log.exit
shift 2

parent=$(dirname -- "$log")
if [ -L "$parent" ]; then
  preflight_fail "parent directory is a symlink: $parent"
fi
if [ ! -d "$parent" ]; then
  preflight_fail "parent directory does not exist: $parent"
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

header="# start: $(date -u +%Y-%m-%dT%H:%M:%SZ)"$'\n'"# cmd:$(printf ' %q' "$@")"$'\n'
if ! printf '%s' "$header" >&3 || ! printf '%s' "$header" | cmp -s - "$log"; then
  exec 3>&-
  preflight_fail "cannot write log header: $log"
fi

"$@" >&3 2>&3 3>&-
status=$?

footer="# end: $(date -u +%Y-%m-%dT%H:%M:%SZ)"$'\n'"# exit: $status"$'\n'
if [ -n "$(tail -c 1 "$log")" ]; then
  footer=$'\n'"$footer"
fi
if ! printf '%s' "$footer" >&3; then
  exec 3>&-
  record_fail "cannot write log footer: $log"
fi
exec 3>&-
if [ "$(tail -n 1 "$log")" != "# exit: $status" ]; then
  record_fail "log footer not found after close: $log"
fi

if ! printf '%d\n' "$status" > "$exit_file"; then
  record_fail "cannot create exit file exclusively: $exit_file"
fi
if [ "$(cat "$exit_file")" != "$status" ]; then
  record_fail "exit file content mismatch: $exit_file"
fi

exit "$status"
