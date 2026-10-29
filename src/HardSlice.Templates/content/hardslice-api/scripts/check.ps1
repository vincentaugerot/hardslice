#!/usr/bin/env pwsh
# The single verification for humans, agents, hooks and CI: a task is not done until this script exits 0.
# Same contract as check.sh: build the solution (restoring if needed), then run unit and architecture tests.
$ErrorActionPreference = 'Stop'

function Invoke-Step([string] $Title, [string[]] $Arguments) {
    Write-Host "==> $Title"
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

# global.json, which pins the SDK and the test runner, is resolved from the current directory.
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    Invoke-Step 'Building MyApp.slnx' @('build', 'MyApp.slnx')
    Invoke-Step 'Running unit tests' @('test', '--project', 'tests/MyApp.Tests/MyApp.Tests.csproj', '--no-build')
    Invoke-Step 'Running architecture tests' @('test', '--project', 'tests/MyApp.ArchitectureTests/MyApp.ArchitectureTests.csproj', '--no-build')
    Write-Host '==> Check passed'
}
finally {
    Pop-Location
}
