#!/usr/bin/env bash
# Automated tests for run-logged.sh. Works only inside a private temporary directory, restores the
# permissions it changes and removes the directory at the end (checked as the last test).
# Exit code: 0 if every check passes, 1 otherwise.
set -u

here=$(cd "$(dirname -- "$0")" && pwd -P)
wrapper=$here/run-logged.sh
# The wrapper requires a canonical parent: resolve symlinks such as /var -> /private/var on macOS.
tmp=$(cd "$(mktemp -d)" && pwd -P) || exit 1

cleanup() {
  if [ -e "$tmp" ]; then
    chmod -R u+rwx "$tmp" 2> /dev/null
    rm -rf "$tmp"
  fi
}
trap cleanup EXIT

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
# The wrapper's stderr goes to $tmp/stderr (overwritten on each call).
wrap() {
  (umask 022; "$wrapper" "$@") > /dev/null 2> "$tmp/stderr"
  echo $?
}

if stat -c '%a' / > /dev/null 2>&1; then
  mode() { stat -c '%a' -- "$1"; }
  file_id() { stat -c '%d:%i' -- "$1"; }
else
  mode() { stat -f '%Lp' -- "$1"; }
  file_id() { stat -f '%d:%i' -- "$1"; }
fi

marker=$tmp/command-ran

# --- Normal behaviour --------------------------------------------------------------------------
rc=$(wrap "$tmp/ok.log" -- true)
check "success: exit 0" '[ "$rc" -eq 0 ]'
check "success: .exit contains 0" '[ "$(cat "$tmp/ok.log.exit")" = 0 ]'
check "success: header lines" '[ "$(sed -n 1p "$tmp/ok.log" | cut -c1-9)" = "# start: " ] && [ "$(sed -n 3p "$tmp/ok.log" | cut -c1-6)" = "# cmd:" ]'
check "success: header device:inode matches the log file" '[ "$(sed -n 2p "$tmp/ok.log")" = "# log-id: $(file_id "$tmp/ok.log")" ]'
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

rc=$(wrap "$tmp/async.log" -- bash -c '(sleep 0.2; echo late-output) &')
check "async descendant: exit 0" '[ "$rc" -eq 0 ]'
# On a broken wrapper the call returns before late-output, which is then appended after the footer.
sleep 0.4
check "async descendant: inherited output is complete before footer" 'grep -qx "late-output" "$tmp/async.log" && [ "$(tail -n 1 "$tmp/async.log")" = "# exit: 0" ]'

rc=$(wrap "$tmp/partial.log" -- printf 'no-newline')
check "output without final newline: footer on its own line" '[ "$(tail -n 1 "$tmp/partial.log")" = "# exit: 0" ] && grep -qx "no-newline" "$tmp/partial.log"'

rc=$(wrap "$tmp/missing.log" -- "$tmp/does-not-exist")
check "missing command: exit 127" '[ "$rc" -eq 127 ]'
check "missing command: .exit contains 127" '[ "$(cat "$tmp/missing.log.exit")" = 127 ]'

# --- Usage errors ------------------------------------------------------------------------------
rc=$(wrap)
check "usage: no argument gives 64" '[ "$rc" -eq 64 ]'
rc=$(wrap "$tmp/u1.log" touch "$marker")
check "usage: missing -- gives 64" '[ "$rc" -eq 64 ]'
rc=$(wrap "$tmp/u2.log" --)
check "usage: missing command gives 64" '[ "$rc" -eq 64 ]'
rc=$(cd "$tmp" && wrap relative.log -- touch "$marker")
check "usage: relative log path gives 64" '[ "$rc" -eq 64 ]'
check "usage: command not run, nothing created" '[ ! -e "$marker" ] && [ ! -e "$tmp/u1.log" ] && [ ! -e "$tmp/u2.log" ] && [ ! -e "$tmp/relative.log" ]'

# --- Preflight errors: the command must never run ---------------------------------------------
rc=$(wrap "$tmp/no-such-dir/x.log" -- touch "$marker")
check "absent parent: exit 70, command not run" '[ "$rc" -eq 70 ] && [ ! -e "$marker" ]'

mkdir "$tmp/sub"
rc=$(wrap "$tmp/sub/../dotdot.log" -- touch "$marker")
check "non-canonical parent (..): exit 70, command not run" '[ "$rc" -eq 70 ] && [ ! -e "$marker" ] && [ ! -e "$tmp/dotdot.log" ]'

printf 'previous\n' > "$tmp/existing.log"
rc=$(wrap "$tmp/existing.log" -- touch "$marker")
check "existing log: exit 70, command not run" '[ "$rc" -eq 70 ] && [ ! -e "$marker" ]'
check "existing log: content untouched, no .exit" '[ "$(cat "$tmp/existing.log")" = previous ] && [ ! -e "$tmp/existing.log.exit" ]'

printf '0\n' > "$tmp/stale.log.exit"
rc=$(wrap "$tmp/stale.log" -- touch "$marker")
check "existing .exit: exit 70, command not run" '[ "$rc" -eq 70 ] && [ ! -e "$marker" ]'
check "existing .exit: no log created, .exit untouched" '[ ! -e "$tmp/stale.log" ] && [ "$(cat "$tmp/stale.log.exit")" = 0 ]'

ln -s "$tmp/dangling-target" "$tmp/link.log"
rc=$(wrap "$tmp/link.log" -- touch "$marker")
check "symlink log (dangling): exit 70, command not run, target not created" '[ "$rc" -eq 70 ] && [ ! -e "$marker" ] && [ ! -e "$tmp/dangling-target" ]'

printf 'kept\n' > "$tmp/real-target"
ln -s "$tmp/real-target" "$tmp/link2.log"
rc=$(wrap "$tmp/link2.log" -- touch "$marker")
check "symlink log (existing target): exit 70, target untouched" '[ "$rc" -eq 70 ] && [ ! -e "$marker" ] && [ "$(cat "$tmp/real-target")" = kept ]'

ln -s "$tmp/exit-target" "$tmp/linkexit.log.exit"
rc=$(wrap "$tmp/linkexit.log" -- touch "$marker")
check "symlink .exit: exit 70, command not run, no log created" '[ "$rc" -eq 70 ] && [ ! -e "$marker" ] && [ ! -e "$tmp/linkexit.log" ] && [ ! -e "$tmp/exit-target" ]'

mkdir "$tmp/real-dir"
ln -s "$tmp/real-dir" "$tmp/link-dir"
rc=$(wrap "$tmp/link-dir/x.log" -- touch "$marker")
check "symlink parent directory: exit 70, command not run, nothing created" '[ "$rc" -eq 70 ] && [ ! -e "$marker" ] && [ -z "$(ls -A "$tmp/real-dir")" ]'

mkdir -p "$tmp/real-anc/inner"
ln -s "$tmp/real-anc" "$tmp/link-anc"
rc=$(wrap "$tmp/link-anc/inner/x.log" -- touch "$marker")
check "symlink in an ancestor (not the parent): exit 70, command not run, nothing created" '[ "$rc" -eq 70 ] && [ ! -e "$marker" ] && [ -z "$(ls -A "$tmp/real-anc/inner")" ]'

# --- Recording failures after the command ran: always 70 ---------------------------------------
mkdir "$tmp/locked"
rc=$(wrap "$tmp/locked/run.log" -- bash -c 'chmod 500 "$1"; exit 1' _ "$tmp/locked")
check "command returns 1 then blocks .exit creation: exit 70 (not 1)" '[ "$rc" -eq 70 ]'
check "command returns 1 then blocks .exit creation: true code reported on stderr" 'grep -q "command exit code: 1" "$tmp/stderr"'
check "command returns 1 then blocks .exit creation: no .exit, footer keeps true code" '[ ! -e "$tmp/locked/run.log.exit" ] && [ "$(tail -n 1 "$tmp/locked/run.log")" = "# exit: 1" ]'
chmod 700 "$tmp/locked"
check "permissions restored on the locked directory" '[ "$(mode "$tmp/locked")" = 700 ]'

rc=$(wrap "$tmp/fake.log" -- bash -c 'printf "0\n" > "$1.exit"; exit 1' _ "$tmp/fake.log")
check "command writes a fake .exit then returns 1: exit 70" '[ "$rc" -eq 70 ]'
check "command writes a fake .exit: fake content not overwritten nor trusted" '[ "$(cat "$tmp/fake.log.exit")" = 0 ] && grep -q "command exit code: 1" "$tmp/stderr"'

rc=$(wrap "$tmp/moved.log" -- bash -c 'mv "$1" "$1.elsewhere"' _ "$tmp/moved.log")
check "command moves the log away: exit 70, no .exit" '[ "$rc" -eq 70 ] && [ ! -e "$tmp/moved.log.exit" ]'

rc=$(wrap "$tmp/replaced.log" -- bash -c 'mv "$1" "$1.orig"; printf "forged\n" > "$1"' _ "$tmp/replaced.log")
check "command replaces the log with a new file (other inode): exit 70, no .exit" '[ "$rc" -eq 70 ] && [ ! -e "$tmp/replaced.log.exit" ]'

rc=$(wrap "$tmp/relinked.log" -- bash -c 'mv "$1" "$1.real"; ln -s "$1.real" "$1"' _ "$tmp/relinked.log")
check "command replaces the log with a symlink to it: exit 70, no .exit" '[ "$rc" -eq 70 ] && [ ! -e "$tmp/relinked.log.exit" ]'

rc=$(wrap "$tmp/tampered.log" -- bash -c 'printf X | dd of="$1" bs=1 seek=2 conv=notrunc 2> /dev/null' _ "$tmp/tampered.log")
check "command alters the header in place (same inode): exit 70, no .exit" '[ "$rc" -eq 70 ] && [ ! -e "$tmp/tampered.log.exit" ]'

# --- Cleanup -----------------------------------------------------------------------------------
cleanup
check "temporary directory removed" '[ ! -e "$tmp" ]'

echo "passed=$passed failed=$failed"
[ "$failed" -eq 0 ]
