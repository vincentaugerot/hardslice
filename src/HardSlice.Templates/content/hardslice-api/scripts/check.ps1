#!/usr/bin/env pwsh
# The single verification for humans, agents, hooks and CI: a task is not done until this script exits 0 without argument.
# A thin launcher, like check.sh: restores the tools pinned by .config/dotnet-tools.json, then runs 'dotnet hardslice check',
# which builds the solution (restoring if needed), runs unit and architecture tests, then integration tests,
# which need a Docker-compatible container runtime, and fails while a TODO(hardslice): marker is left in src/ or tests/.
# Its exit code is the one of this script.
#   --build  the verification after an edit: the incremental build alone, errors only, then the TODO(hardslice): markers.
#            Its success does not mean the work is done: only the full check decides.
# Any other argument exits with code 2. No param block: PowerShell would bind or refuse arguments itself.
$ErrorActionPreference = 'Stop'

if ($args.Count -gt 1 -or ($args.Count -eq 1 -and $args[0] -cne '--build')) {
    [Console]::Error.WriteLine("error: scripts/check.ps1 takes no argument, or --build for the incremental build alone. Unknown arguments: '$($args -join ' ')'.")
    exit 2
}
$build = $args.Count -eq 1

# global.json, which pins the SDK and the test runner, and the tool manifest are resolved from the current directory.
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    if (-not $build) {
        & dotnet tool restore
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }
        & dotnet hardslice check
        exit $LASTEXITCODE
    }

    # After an edit, the output is reduced to errors: the restore of the tools is shown only when it fails.
    $restoreOutput = & dotnet tool restore 2>&1
    if ($LASTEXITCODE -ne 0) {
        $restoreOutput | ForEach-Object { [Console]::Error.WriteLine("$_") }
        exit 1
    }
    & dotnet hardslice check --build
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
