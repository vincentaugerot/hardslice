#!/usr/bin/env bash
# The single verification for humans, agents, hooks and CI: a task is not done until this script exits 0.
# Same contract as check.ps1: build the solution (restoring if needed), run unit and architecture tests,
# then integration tests, which need a Docker-compatible container runtime.
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

echo "==> Checking the container runtime"
if ! docker info > /dev/null 2>&1; then
  echo "error: integration tests need a Docker-compatible container runtime reachable by the 'docker' CLI (Docker Desktop, Docker Engine, Podman, Colima...). Start one, check that 'docker info' succeeds, then run this script again." >&2
  exit 1
fi

step "Running integration tests" test --project tests/MyApp.IntegrationTests/MyApp.IntegrationTests.csproj --no-build

echo "==> Check passed"
