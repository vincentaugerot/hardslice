#!/usr/bin/env bash
# Packs HardSlice, installs the hardslice-api template, generates a solution in a temporary directory,
# runs its tests, then proves HS016 is active and points to a rule doc that exists in the generated solution.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
work="$(mktemp -d "${TMPDIR:-/tmp}/hardslice-template.XXXXXX")"
trap 'rm -rf "$work"' EXIT

feed="$work/feed"
hive="$work/template-hive"
app_name="Demo"
app="$work/$app_name"
app_project="src/$app_name/$app_name.csproj"
template_hive_args=(--debug:custom-hive "$hive")

echo "==> Packing HardSlice.Analyzers and HardSlice.Templates"
dotnet pack "$repo_root/src/HardSlice.Analyzers/HardSlice.Analyzers.csproj" --configuration Release --output "$feed"
dotnet pack "$repo_root/src/HardSlice.Templates/HardSlice.Templates.csproj" --configuration Release --output "$feed"

# A private package cache guarantees the freshly packed analyzer is restored, never a stale build of the same version.
# It is set only after packing so repository restore artifacts never point into the temporary directory.
export NUGET_PACKAGES="$work/nuget-packages"

# Parent of the generated solution: HardSlice packages come only from the local feed, everything else from nuget.org.
cat > "$work/nuget.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
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
dotnet new hardslice-api --name "$app_name" --output "$app" "${template_hive_args[@]}"

echo "==> Building and testing the generated solution"
(
  cd "$app"
  dotnet restore
  dotnet build --no-restore
  dotnet test --no-build
)

echo "==> Checking that HS016 is active and its Doc path exists"
cat > "$app/src/$app_name/Features/Bookings/CancelBooking/Hs016Probe.cs" <<EOF
namespace $app_name.Features.Bookings.CancelBooking;

internal static class Hs016Probe
{
    internal static readonly Type Target = typeof($app_name.Features.Bookings.ConfirmBooking.ConfirmBookingHandler);
}
EOF

if build_output="$(cd "$app" && dotnet build "$app_project" --no-restore 2>&1)"; then
  echo "error: the generated solution built despite a cross-slice reference: HS016 is not active." >&2
  exit 1
fi

doc_path="$(printf '%s\n' "$build_output" | sed -n "s/.*error HS016:.* Doc: '\([^']*\)'.*/\1/p" | head -n 1)"
if [[ -z "$doc_path" ]]; then
  printf '%s\n' "$build_output" >&2
  echo "error: the build failed without an HS016 diagnostic announcing a Doc path." >&2
  exit 1
fi

if [[ ! -f "$app/$doc_path" ]]; then
  echo "error: HS016 announces '$doc_path', which does not exist from the generated solution root." >&2
  exit 1
fi

echo "==> Template OK: $app_name builds, its tests pass and HS016 points to $doc_path"
