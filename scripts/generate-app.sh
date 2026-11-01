#!/usr/bin/env bash
# Packs the three HardSlice packages at the version of Directory.Build.props, installs the hardslice-api template into an isolated hive
# and generates the <app-name> solution, Demo by default, in <work-dir>/<app-name>, with the options of the template that follow it,
# such as --no-sample. Shared by test-template.sh, demo-agent-fix.sh, demo-add-feature.sh and measure-analyzers.sh so all exercise
# the same generated solution.
# Follows "Try a local build of the packages" and step 2 of "Getting started" in README.md, with the same commands and nuget.config, isolated in <work-dir>:
# the feed, the template hive and the NuGet package folder.
set -euo pipefail

if [[ $# -lt 1 ]]; then
  echo "usage: $0 <work-dir> [app-name [template-option...]]" >&2
  exit 2
fi

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
work="$(cd "$1" && pwd)"
feed="$work/feed"
hive="$work/template-hive"
app_name="${2:-Demo}"
template_options=("${@:3}")
template_hive_args=(--debug:custom-hive "$hive")

# The single version of the HardSlice packages, which the pack of the template writes where it pins the analyzers and the tool.
version="$(cd "$repo_root" && dotnet msbuild src/HardSlice.Templates/HardSlice.Templates.csproj -getProperty:Version)"

echo "==> Packing HardSlice.Analyzers, HardSlice.Tool and HardSlice.Templates $version"
dotnet pack "$repo_root/src/HardSlice.Analyzers/HardSlice.Analyzers.csproj" --configuration Release --output "$feed"
dotnet pack "$repo_root/src/HardSlice.Tool/HardSlice.Tool.csproj" --configuration Release --output "$feed"
dotnet pack "$repo_root/src/HardSlice.Templates/HardSlice.Templates.csproj" --configuration Release --output "$feed"

expected_packages="$(printf '%s\n' "HardSlice.Analyzers.$version.nupkg" "HardSlice.Templates.$version.nupkg" "HardSlice.Tool.$version.nupkg")"
packed_packages="$(cd "$feed" && ls -1 -- *.nupkg | LC_ALL=C sort)"
if [[ "$packed_packages" != "$expected_packages" ]]; then
  echo "error: $feed holds ${packed_packages//$'\n'/, } instead of the three HardSlice packages at version $version." >&2
  exit 1
fi

# Parent of the generated solution: HardSlice packages come only from the local feed, everything else from nuget.org.
# A private package folder guarantees the freshly packed analyzer and tool are restored, never a stale build of the same version,
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
dotnet new install "$feed/HardSlice.Templates.$version.nupkg" "${template_hive_args[@]}"

echo "==> Generating $app_name"
dotnet new hardslice-api --name "$app_name" --output "$work/$app_name" "${template_options[@]}" "${template_hive_args[@]}"
