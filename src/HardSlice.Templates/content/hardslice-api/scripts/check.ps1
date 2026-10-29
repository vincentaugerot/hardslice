#!/usr/bin/env pwsh
# The single verification for humans, agents, hooks and CI: a task is not done until this script exits 0.
# Same contract as check.sh: build the solution (restoring if needed), run unit and architecture tests,
# then integration tests, which need a Docker-compatible container runtime.
$ErrorActionPreference = 'Stop'

function Invoke-Step([string] $Title, [string[]] $Arguments) {
    Write-Host "==> $Title"
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

function Test-ContainerRuntime {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        return $false
    }
    & docker info *> $null
    return $LASTEXITCODE -eq 0
}

# global.json, which pins the SDK and the test runner, is resolved from the current directory.
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    Invoke-Step 'Building MyApp.slnx' @('build', 'MyApp.slnx')
    Invoke-Step 'Running unit tests' @('test', '--project', 'tests/MyApp.Tests/MyApp.Tests.csproj', '--no-build')
    Invoke-Step 'Running architecture tests' @('test', '--project', 'tests/MyApp.ArchitectureTests/MyApp.ArchitectureTests.csproj', '--no-build')

    Write-Host '==> Checking the container runtime'
    if (-not (Test-ContainerRuntime)) {
        [Console]::Error.WriteLine("error: integration tests need a Docker-compatible container runtime reachable by the 'docker' CLI (Docker Desktop, Docker Engine, Podman, Colima...). Start one, check that 'docker info' succeeds, then run this script again.")
        exit 1
    }

    Invoke-Step 'Running integration tests' @('test', '--project', 'tests/MyApp.IntegrationTests/MyApp.IntegrationTests.csproj', '--no-build')
    Write-Host '==> Check passed'
}
finally {
    Pop-Location
}
