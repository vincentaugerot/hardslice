#!/usr/bin/env bash
# PB-048: the CI workflow of a generated solution, .github/workflows/hardslice.yml, outside GitHub Actions.
#   lint <solution>         validates the workflow with actionlint, shellcheck of its run steps included.
#   run <solution> <job>    runs the run steps of one job in order, in the root of the solution, with the shell that a Linux
#                           runner gives them: bash --noprofile --norc -eo pipefail with shell: bash (on the step, in the
#                           defaults of its job or of the workflow), bash -e without shell, which does not fail a pipe on
#                           its first command. The steps share the environment of this script.
# run skips the steps that use an action: the caller reproduces them, as the generated-workflow job of ci.yml does with the
# same actions at the same pins (TemplatePackageTests checks the pins). The expressions of the workflow are not evaluated:
# the caller sets the variables that a step reads in its env, and a run step with any key but name, env, shell and run, or
# with a shell other than bash, is refused.
# actionlint and yq run in containers pinned by digest: it needs a Docker-compatible container runtime.
set -euo pipefail

actionlint_image="rhysd/actionlint:1.7.12@sha256:b1934ee5f1c509618f2508e6eb47ee0d3520686341fec936f3b79331f9315667"
yq_image="mikefarah/yq:4.54.1@sha256:4b3d9475d65571d28cbb19544d3820ec2945e4c8b2f18279394282b8dc3a592e"
workflow_path=".github/workflows/hardslice.yml"

usage() {
  echo "usage: $0 lint <solution> | run <solution> <job>" >&2
  exit 2
}

[[ $# -ge 2 ]] || usage
command="$1"
solution="$(cd "$2" && pwd)"
workflow="$solution/$workflow_path"
if [[ ! -f "$workflow" ]]; then
  echo "error: $solution has no $workflow_path." >&2
  exit 1
fi

# The workflow goes through the standard input: the containers never read the files of the solution.
yq() {
  docker run --rm -i "$yq_image" "$1" < "$workflow"
}

case "$command" in
  lint)
    [[ $# -eq 2 ]] || usage
    docker run --rm -i "$actionlint_image" -stdin-filename "$workflow_path" - < "$workflow"
    echo "$workflow_path of $(basename "$solution") passes actionlint"
    ;;
  run)
    [[ $# -eq 3 ]] || usage
    job="$3"
    if [[ ! "$job" =~ ^[A-Za-z_][A-Za-z0-9_-]*$ || "$(yq ".jobs | has(\"$job\")")" != true ]]; then
      echo "error: $workflow_path has no job '$job'." >&2
      exit 1
    fi
    step_file="$(mktemp)"
    trap 'rm -f "$step_file"' EXIT
    count="$(yq ".jobs.$job.steps | length")"
    for ((index = 0; index < count; index++)); do
      step=".jobs.$job.steps[$index]"
      if [[ "$(yq "$step | has(\"uses\")")" == true ]]; then
        echo "==> $job, step $((index + 1)): $(yq "$step.uses"), reproduced by the caller"
        continue
      fi
      keys="$(yq "$step | keys | .[] | select(. != \"name\" and . != \"env\" and . != \"shell\" and . != \"run\")")"
      if [[ -n "$keys" ]]; then
        echo "error: step $((index + 1)) of the job $job has keys that this script does not reproduce: ${keys//$'\n'/, }." >&2
        exit 1
      fi
      shell="$(yq "$step.shell // .jobs.$job.defaults.run.shell // .defaults.run.shell // \"\"")"
      case "$shell" in
        bash) shell_command=(bash --noprofile --norc -eo pipefail) ;;
        "") shell_command=(bash -e) ;;
        *)
          echo "error: step $((index + 1)) of the job $job runs with the shell '$shell', which this script does not reproduce." >&2
          exit 1
          ;;
      esac
      yq "$step.run" > "$step_file"
      echo "==> $job, step $((index + 1)): $(yq "$step.name // \"run\""), with ${shell_command[*]}"
      (cd "$solution" && "${shell_command[@]}" "$step_file")
    done
    ;;
  *)
    usage
    ;;
esac
