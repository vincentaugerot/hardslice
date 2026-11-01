#!/usr/bin/env bash
# Verifies published HardSlice packages the way a user meets them: installs HardSlice.Templates@<version> from a NuGet source,
# NuGet.org by default, as step 1 of "Getting started" in README.md says, generates a solution with the Bookings sample and one
# without, then in each restores the local tools and runs dotnet hardslice check, integration tests included.
# Run by the verify job of .github/workflows/release.yml on a fresh runner, once the three packages are pushed. With --source
# <folder>, it verifies the packages of a local folder the same way, before a release.
# Everything NuGet and dotnet new keep lives in <work-dir>: the template hive, the user-level NuGet configuration, the package
# folder and the HTTP cache. The template, the analyzers and the tool can therefore come only from that source, never from a
# cache or a build of this repository, and the script checks that each is, byte for byte, the package the source serves.
# It first waits, at most --wait-minutes (60 by default), until the source lists the three packages at that version:
# NuGet.org validates and indexes a pushed package before serving it.
set -euo pipefail

usage() {
  echo "usage: $0 <version> <work-dir> [--source <nuget-v3-index-url | folder>] [--wait-minutes <n>]" >&2
  exit 2
}

[[ $# -ge 2 ]] || usage
version="$1"
work="$(cd "$2" && pwd)"
shift 2
nuget_org="https://api.nuget.org/v3/index.json"
source="$nuget_org"
wait_minutes=60
while [[ $# -gt 0 ]]; do
  case "$1" in
    --source)
      [[ $# -ge 2 ]] || usage
      source="$2"
      shift 2
      ;;
    --wait-minutes)
      [[ $# -ge 2 && "$2" =~ ^[0-9]+$ ]] || usage
      wait_minutes="$2"
      shift 2
      ;;
    *) usage ;;
  esac
done

if [[ "$source" != http://* && "$source" != https://* ]]; then
  source="$(cd "$source" && pwd)"
fi

# Dependencies first: the template pins the analyzers and the tool, so it is pointless without them.
packages=(HardSlice.Analyzers HardSlice.Tool HardSlice.Templates)

# Whether the source lists <package> at the version: in the PackageBaseAddress resource of a V3 feed, which dotnet new install,
# restore and dotnet tool restore read, or as a .nupkg in a folder.
is_listed() {
  local package="$1" id versions
  if [[ "$source" != http://* && "$source" != https://* ]]; then
    [[ -f "$source/$package.$version.nupkg" ]]
    return
  fi
  id="$(printf '%s' "$package" | tr '[:upper:]' '[:lower:]')"
  # 404 until the first version of the package is served.
  versions="$(curl --silent --fail --location "$package_base_address$id/index.json")" || return 1
  python3 -I -c 'import json, sys; sys.exit(0 if sys.argv[1] in json.load(sys.stdin)["versions"] else 1)' "${version,,}" <<< "$versions"
}

if [[ "$source" == http://* || "$source" == https://* ]]; then
  package_base_address="$(curl --silent --fail --location "$source" | python3 -I -c '
import json, sys
resources = json.load(sys.stdin)["resources"]
print(next(r["@id"] for r in resources if r["@type"] == "PackageBaseAddress/3.0.0").rstrip("/") + "/")')"
fi

echo "==> Waiting until $source lists ${packages[*]} $version (at most $wait_minutes min)"
deadline=$((SECONDS + wait_minutes * 60))
for package in "${packages[@]}"; do
  until is_listed "$package"; do
    if ((SECONDS >= deadline)); then
      echo "error: $source still does not list $package $version after $wait_minutes min." >&2
      exit 1
    fi
    sleep 30
  done
  echo "$package $version is listed."
done

# A fresh home for dotnet and NuGet: no template, configuration, package or cached response of this machine.
export DOTNET_CLI_HOME="$work/dotnet-cli"
export NUGET_PACKAGES="$work/nuget-packages"
export NUGET_HTTP_CACHE_PATH="$work/nuget-http-cache"
export DOTNET_NOLOGO=true

# On NuGet.org, the user-level configuration NuGet creates in that home, as for a new user: no nuget.config at all.
# From another source, HardSlice packages come from it only, everything else from NuGet.org.
if [[ "$source" != "$nuget_org" ]]; then
  cat > "$work/nuget.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="hardslice" value="$source" />
    <add key="nuget.org" value="$nuget_org" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="hardslice">
      <package pattern="HardSlice.*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
EOF
fi

cd "$work"

echo "==> dotnet new install HardSlice.Templates@$version"
dotnet new install "HardSlice.Templates@$version"

sha512() {
  python3 -I -c 'import base64, hashlib, sys; print(base64.b64encode(hashlib.sha512(sys.stdin.buffer.read()).digest()).decode())'
}

# The SHA-512 of <package> at the version, as the source serves it.
source_hash() {
  local package="$1" id
  if [[ "$source" != http://* && "$source" != https://* ]]; then
    sha512 < "$source/$package.$version.nupkg"
    return
  fi
  id="$(printf '%s' "$package" | tr '[:upper:]' '[:lower:]')"
  curl --silent --fail --location "$package_base_address$id/${version,,}/$id.${version,,}.nupkg" | sha512
}

# Fails unless <file> holds the very bytes of <package> that the source serves.
expect_from_source() {
  local package="$1" file="$2" expected actual
  if [[ ! -f "$file" ]]; then
    echo "error: $package $version is not at $file." >&2
    exit 1
  fi
  expected="$(source_hash "$package")"
  actual="$(sha512 < "$file")"
  if [[ "$actual" != "$expected" ]]; then
    echo "error: $file is not the $package $version that $source serves." >&2
    exit 1
  fi
  echo "$package $version comes from $source."
}

# The package NuGet restored into the package folder, which only the commands of this script fill.
restored_package() {
  local id
  id="$(printf '%s' "$1" | tr '[:upper:]' '[:lower:]')"
  printf '%s' "$NUGET_PACKAGES/$id/${version,,}/$id.${version,,}.nupkg"
}

# dotnet new lists each template package it installed with its version and the source it took it from.
installed="$(dotnet new uninstall)"
if ! grep -qx "   HardSlice.Templates" <<< "$installed" \
  || ! grep -qx "      Version: $version" <<< "$installed" \
  || ! grep -qx "         NuGetSource: $source" <<< "$installed"; then
  echo "error: dotnet new does not list HardSlice.Templates $version from $source among its installed templates:" >&2
  echo "$installed" >&2
  exit 1
fi
expect_from_source HardSlice.Templates "$DOTNET_CLI_HOME/.templateengine/packages/HardSlice.Templates.$version.nupkg"

verify_solution() {
  local name="$1"
  shift
  echo "==> dotnet new hardslice-api --name $name $*"
  dotnet new hardslice-api --name "$name" "$@"

  echo "==> $name: dotnet tool restore, then dotnet hardslice check"
  (
    cd "$name"
    if ! grep -q "<GlobalPackageReference Include=\"HardSlice.Analyzers\" Version=\"$version\" />" Directory.Packages.props; then
      echo "error: $name does not pin HardSlice.Analyzers $version in Directory.Packages.props." >&2
      exit 1
    fi
    dotnet tool restore
    dotnet hardslice check
  )
}

verify_solution Rooms
verify_solution Empty --no-sample

echo "==> Checking where the analyzers and the tool were restored from"
for package in HardSlice.Analyzers HardSlice.Tool; do
  expect_from_source "$package" "$(restored_package "$package")"
done

echo "==> HardSlice.Templates $version from $source generates solutions that pass dotnet hardslice check, with and without Bookings."
