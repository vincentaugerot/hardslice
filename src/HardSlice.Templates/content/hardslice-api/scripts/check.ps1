#!/usr/bin/env pwsh
# The single verification for humans, agents, hooks and CI: a task is not done until this script exits 0.
# A thin launcher, like check.sh: restores the tools pinned by .config/dotnet-tools.json, then runs 'dotnet hardslice check',
# which builds the solution (restoring if needed), runs unit and architecture tests, then integration tests,
# which need a Docker-compatible container runtime. Its exit code is the one of this script.
$ErrorActionPreference = 'Stop'

# global.json, which pins the SDK and the test runner, and the tool manifest are resolved from the current directory.
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    & dotnet tool restore
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
    & dotnet hardslice check
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
