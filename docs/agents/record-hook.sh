#!/usr/bin/env bash
# Hook recorder of the PB-034 trials (docs/agents/support.md, "Protocole d'essai"). Not shipped to applications.
#   record-hook.sh <cli> <event> [pass|post-exit2|post-context|stop-exit2|stop-json]
# Saves the payload read on stdin, and the names of the environment variables, to <work>/payloads/<cli>/<event>-NN.json(.env),
# where <work> is the parent of the folder holding this script. Then answers by mode:
#   post-exit2    first edit of a .cs file: message on stderr, exit code 2 (Claude Code, Codex)
#   post-context  first edit of a .cs file: {"additionalContext": "..."} on stdout, exit code 0 (Copilot CLI)
#   stop-exit2    first end of turn: message on stderr, exit code 2 (Claude Code, Codex)
#   stop-json     first end of turn: {"decision": "block", "reason": "..."} on stdout, exit code 0 (Copilot CLI)
# The messages ask for a visible fix (a marker comment, a file), which proves that the agent received them.
set -u
work="$(cd "$(dirname "$0")/.." && pwd)"
cli="$1"; event="$2"; mode="${3:-pass}"
mkdir -p "$work/payloads/$cli" "$work/state"
n=$(ls "$work/payloads/$cli" | grep -c "^$event-.*\.json$" || true)
file="$work/payloads/$cli/$event-$(printf %02d "$n").json"
cat > "$file"
env | cut -d= -f1 | sort > "$file.env"
echo "$(date -u +%FT%TZ) $cli $event $mode -> $file" >> "$work/payloads/calls.log"

post_msg='HARDSLICE-SPIKE error HS999: the post-edit check failed. Fix: add the line // hook-feedback-received as the first line of the file you just edited, then stop.'
stop_msg='HARDSLICE-SPIKE: the verification failed. Fix: create the file HOOK_STOP_SEEN.txt at the solution root containing the word ok, then finish.'
edit_tool='"(tool_name|toolName)": *"(edit|create|Edit|Write|apply_patch|write_file|replace)"'

case "$mode" in
  post-exit2|post-context)
    if grep -q '\.cs' "$file" && grep -Eq "$edit_tool" "$file" && [ ! -e "$work/state/$cli-post" ]; then
      touch "$work/state/$cli-post"
      if [ "$mode" = post-exit2 ]; then echo "$post_msg" >&2; exit 2; fi
      printf '{"additionalContext":"%s"}\n' "$post_msg"
    fi ;;
  stop-exit2|stop-json)
    if [ ! -e "$work/state/$cli-stop" ]; then
      touch "$work/state/$cli-stop"
      if [ "$mode" = stop-exit2 ]; then echo "$stop_msg" >&2; exit 2; fi
      printf '{"decision":"block","reason":"%s"}\n' "$stop_msg"
    fi ;;
esac
exit 0
