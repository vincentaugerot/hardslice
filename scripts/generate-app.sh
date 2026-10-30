#!/usr/bin/env bash
# Packs HardSlice, installs the hardslice-api template into an isolated hive and generates the Demo solution in <work-dir>/Demo.
# Shared by test-template.sh and demo-hs016-agent-fix.sh so both exercise the same generated solution.
# Follows steps 1 and 2 of "Getting started" in README.md, with the same commands and nuget.config, isolated in <work-dir>:
# the feed, the template hive and the NuGet package folder.
set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "usage: $0 <work-dir>" >&2
  exit 2
fi

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
work="$(cd "$1" && pwd)"
feed="$work/feed"
hive="$work/template-hive"
app_name="Demo"
template_hive_args=(--debug:custom-hive "$hive")

echo "==> Packing HardSlice.Analyzers and HardSlice.Templates"
dotnet pack "$repo_root/src/HardSlice.Analyzers/HardSlice.Analyzers.csproj" --configuration Release --output "$feed"
dotnet pack "$repo_root/src/HardSlice.Templates/HardSlice.Templates.csproj" --configuration Release --output "$feed"

# Parent of the generated solution: HardSlice packages come only from the local feed, everything else from nuget.org.
# A private package folder guarantees the freshly packed analyzer is restored, never a stale build of the same version,
# including when an agent later runs the check scripts from its own shell.
cat > "$work/nuget.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <config>
    <add key="globalPackagesFolder" value="nuget-packages" />
  </config>
  <packageSources>
    <clear />
    <add key="hardslice-local" value="feed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="hardslice-local">
      <package pattern="HardSlice.*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
EOF

echo "==> Installing the template into an isolated hive"
dotnet new install "$feed"/HardSlice.Templates.*.nupkg "${template_hive_args[@]}"

echo "==> Generating $app_name"
dotnet new hardslice-api --name "$app_name" --output "$work/$app_name" "${template_hive_args[@]}"
