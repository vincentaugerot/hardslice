#!/usr/bin/env bash
# The single verification for humans, agents, hooks and CI: a task is not done until this script exits 0 without argument.
# A thin launcher, like check.ps1: restores the tools pinned by .config/dotnet-tools.json, then runs 'dotnet hardslice check',
# which builds the solution (restoring if needed), runs unit and architecture tests, then integration tests,
# which need a Docker-compatible container runtime, and fails while a TODO(hardslice): marker is left in src/ or tests/.
# Exit codes, as those of 'dotnet hardslice check': 0 the check passed, 1 a step failed, its output left as is, 2 a wrong argument.
#   --build  the verification after an edit: the incremental build alone, errors only, then the TODO(hardslice): markers.
#            Its success does not mean the work is done: only the full check decides.
# Any other argument exits with code 2, before anything runs.
set -euo pipefail

if [[ $# -gt 1 || ( $# -eq 1 && "$1" != --build ) ]]; then
  echo "error: scripts/check.sh takes no argument, or --build for the incremental build alone. Unknown arguments: '$*'." >&2
  exit 2
fi

# global.json, which pins the SDK and the test runner, and the tool manifest are resolved from the current directory.
cd "$(dirname "${BASH_SOURCE[0]}")/.."

if [[ $# -eq 0 ]]; then
  dotnet tool restore || exit 1
  exec dotnet hardslice check
fi

# After an edit, the output is reduced to errors: the restore of the tools is shown only when it fails.
if ! restore_output="$(dotnet tool restore 2>&1)"; then
  printf '%s\n' "$restore_output" >&2
  exit 1
fi
exec dotnet hardslice check --build
