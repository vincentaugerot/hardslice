#!/usr/bin/env bash
# Automated tests for run-logged.sh. Works only inside a private temporary directory removed on exit.
# Exit code: 0 if every check passes, 1 otherwise.
set -u

here=$(cd "$(dirname -- "$0")" && pwd)
wrapper=$here/run-logged.sh
tmp=$(mktemp -d) || exit 1
trap 'rm -rf "$tmp"' EXIT

passed=0
failed=0

check() {
  if eval "$2"; then
    echo "PASS $1"
    passed=$((passed + 1))
  else
    echo "FAIL $1"
    failed=$((failed + 1))
  fi
}

# Runs the wrapper under a permissive umask to prove that it enforces 0600 itself.
wrap() {
  (umask 022; "$wrapper" "$@") > /dev/null 2>&1
  echo $?
}

mode() {
  stat -f '%Lp' "$1" 2> /dev/null || stat -c '%a' "$1"
}

marker=$tmp/command-ran

rc=$(wrap "$tmp/ok.log" -- true)
check "success: exit 0" '[ "$rc" -eq 0 ]'
check "success: .exit contains 0" '[ "$(cat "$tmp/ok.log.exit")" = 0 ]'
check "success: header lines" '[ "$(sed -n 1p "$tmp/ok.log" | cut -c1-9)" = "# start: " ] && [ "$(sed -n 2p "$tmp/ok.log" | cut -c1-6)" = "# cmd:" ]'
check "success: footer exit line" '[ "$(tail -n 1 "$tmp/ok.log")" = "# exit: 0" ]'
check "permissions: log is 0600" '[ "$(mode "$tmp/ok.log")" = 600 ]'
check "permissions: .exit is 0600" '[ "$(mode "$tmp/ok.log.exit")" = 600 ]'

rc=$(wrap "$tmp/ko.log" -- false)
check "failure: exit 1" '[ "$rc" -eq 1 ]'
check "failure: .exit contains 1" '[ "$(cat "$tmp/ko.log.exit")" = 1 ]'

rc=$(wrap "$tmp/streams.log" -- bash -c 'echo to-stdout; echo to-stderr >&2; exit 3')
check "stdout+stderr: exit 3" '[ "$rc" -eq 3 ]'
check "stdout+stderr: stdout captured" 'grep -qx "to-stdout" "$tmp/streams.log"'
check "stdout+stderr: stderr captured" 'grep -qx "to-stderr" "$tmp/streams.log"'
check "stdout+stderr: .exit contains 3" '[ "$(cat "$tmp/streams.log.exit")" = 3 ]'

rc=$(wrap "$tmp/partial.log" -- printf 'no-newline')
check "output without final newline: footer on its own line" '[ "$(tail -n 1 "$tmp/partial.log")" = "# exit: 0" ] && grep -qx "no-newline" "$tmp/partial.log"'

rc=$(wrap "$tmp/missing.log" -- "$tmp/does-not-exist")
check "missing command: exit 127" '[ "$rc" -eq 127 ]'
check "missing command: .exit contains 127" '[ "$(cat "$tmp/missing.log.exit")" = 127 ]'

rc=$(wrap)
check "usage: no argument gives 64" '[ "$rc" -eq 64 ]'
rc=$(wrap "$tmp/u1.log" touch "$marker")
check "usage: missing -- gives 64" '[ "$rc" -eq 64 ]'
rc=$(wrap "$tmp/u2.log" --)
check "usage: missing command gives 64" '[ "$rc" -eq 64 ]'
check "usage: command not run, nothing created" '[ ! -e "$marker" ] && [ ! -e "$tmp/u1.log" ] && [ ! -e "$tmp/u2.log" ]'

rc=$(wrap "$tmp/no-such-dir/x.log" -- touch "$marker")
check "absent parent: exit 70" '[ "$rc" -eq 70 ]'
check "absent parent: command not run" '[ ! -e "$marker" ]'

printf 'previous\n' > "$tmp/existing.log"
rc=$(wrap "$tmp/existing.log" -- touch "$marker")
check "existing log: exit 70" '[ "$rc" -eq 70 ]'
check "existing log: command not run" '[ ! -e "$marker" ]'
check "existing log: content untouched, no .exit" '[ "$(cat "$tmp/existing.log")" = previous ] && [ ! -e "$tmp/existing.log.exit" ]'

printf '0\n' > "$tmp/stale.log.exit"
rc=$(wrap "$tmp/stale.log" -- touch "$marker")
check "existing .exit: exit 70" '[ "$rc" -eq 70 ]'
check "existing .exit: command not run" '[ ! -e "$marker" ]'
check "existing .exit: no log created, .exit untouched" '[ ! -e "$tmp/stale.log" ] && [ "$(cat "$tmp/stale.log.exit")" = 0 ]'

ln -s "$tmp/dangling-target" "$tmp/link.log"
rc=$(wrap "$tmp/link.log" -- touch "$marker")
check "symlink log (dangling): exit 70" '[ "$rc" -eq 70 ]'
check "symlink log (dangling): command not run, target not created" '[ ! -e "$marker" ] && [ ! -e "$tmp/dangling-target" ]'

printf 'kept\n' > "$tmp/real-target"
ln -s "$tmp/real-target" "$tmp/link2.log"
rc=$(wrap "$tmp/link2.log" -- touch "$marker")
check "symlink log (existing target): exit 70, target untouched" '[ "$rc" -eq 70 ] && [ ! -e "$marker" ] && [ "$(cat "$tmp/real-target")" = kept ]'

ln -s "$tmp/exit-target" "$tmp/linkexit.log.exit"
rc=$(wrap "$tmp/linkexit.log" -- touch "$marker")
check "symlink .exit: exit 70" '[ "$rc" -eq 70 ]'
check "symlink .exit: command not run, no log created" '[ ! -e "$marker" ] && [ ! -e "$tmp/linkexit.log" ] && [ ! -e "$tmp/exit-target" ]'

mkdir "$tmp/real-dir"
ln -s "$tmp/real-dir" "$tmp/link-dir"
rc=$(wrap "$tmp/link-dir/x.log" -- touch "$marker")
check "symlink parent directory: exit 70" '[ "$rc" -eq 70 ]'
check "symlink parent directory: command not run, nothing created" '[ ! -e "$marker" ] && [ -z "$(ls -A "$tmp/real-dir")" ]'

echo "passed=$passed failed=$failed"
[ "$failed" -eq 0 ]
