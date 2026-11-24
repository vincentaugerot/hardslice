#!/usr/bin/env bash
# Packs the three HardSlice packages of this repository for a local trial, at <version>.dev.<n>, where <version> is the
# HardSliceVersion of Directory.Build.props and <n> the same number for the three packs: the template writes at its pack the
# version of the analyzers and of the tool that it pins. <n> is the Unix time in seconds, or one more than the number of the last
# pack of this clone when that one is not older, so two packs in a row never share a version: neither the NuGet cache nor an
# installed template serve an earlier build in place of the new one. NuGet orders <version> < <version>.dev.<n> < <version>.dev.<n+1>
# and every later release. Only .github/workflows/release.yml packs the bare version.
# Writes the packages in <output-dir>, artifacts/packages by default, prints the output of the packs on stderr and the version alone
# on stdout, such as: version="$(scripts/pack-local.sh)".
set -euo pipefail

usage() {
  echo "usage: $0 [--output <output-dir>]" >&2
  exit 2
}

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
output="$repo_root/artifacts/packages"
while [[ $# -gt 0 ]]; do
  case "$1" in
    --output)
      [[ $# -ge 2 ]] || usage
      output="$2"
      shift 2
      ;;
    *) usage ;;
  esac
done
mkdir -p "$output"
output="$(cd "$output" && pwd)"

# The number of the last pack of this clone, ignored by git with the rest of artifacts/.
last_file="$repo_root/artifacts/.last-local-pack"
mkdir -p "$repo_root/artifacts"
build="$(date +%s)"
last="$(cat "$last_file" 2> /dev/null || echo 0)"
if [[ "$last" =~ ^[0-9]+$ ]] && ((build <= last)); then
  build=$((last + 1))
fi
printf '%s\n' "$build" > "$last_file"

version="$(cd "$repo_root" && dotnet msbuild src/HardSlice.Templates/HardSlice.Templates.csproj -getProperty:Version "-p:HardSliceDevBuild=$build")"
if [[ "$version" != *".dev.$build" ]]; then
  echo "error: Directory.Build.props gives the version '$version' to a local pack, which should end with .dev.$build." >&2
  exit 1
fi

echo "==> Packing HardSlice.Analyzers, HardSlice.Tool and HardSlice.Templates $version into $output" >&2
for package in HardSlice.Analyzers HardSlice.Tool HardSlice.Templates; do
  dotnet pack "$repo_root/src/$package/$package.csproj" --configuration Release --output "$output" "-p:HardSliceDevBuild=$build" >&2
done

for package in HardSlice.Analyzers HardSlice.Tool HardSlice.Templates; do
  if [[ ! -f "$output/$package.$version.nupkg" ]]; then
    echo "error: $output holds no $package.$version.nupkg after its pack." >&2
    exit 1
  fi
done
printf '%s\n' "$version"
