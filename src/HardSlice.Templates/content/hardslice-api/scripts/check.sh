#!/usr/bin/env bash
# The single verification for humans, agents, hooks and CI: a task is not done until this script exits 0.
# Same contract as check.ps1: build the solution (restoring if needed), then run unit and architecture tests.
set -euo pipefail

# global.json, which pins the SDK and the test runner, is resolved from the current directory.
cd "$(dirname "${BASH_SOURCE[0]}")/.."

step() {
  echo "==> $1"
  shift
  dotnet "$@"
}

step "Building MyApp.slnx" build MyApp.slnx
step "Running unit tests" test --project tests/MyApp.Tests/MyApp.Tests.csproj --no-build
step "Running architecture tests" test --project tests/MyApp.ArchitectureTests/MyApp.ArchitectureTests.csproj --no-build

echo "==> Check passed"
