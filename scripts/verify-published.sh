#!/usr/bin/env bash
# Verifies published HardSlice packages the way a user meets them: installs HardSlice.Templates@<version> from a NuGet source,
# NuGet.org by default, as step 1 of docs/getting-started.md says, generates a solution with the Bookings sample and one
# without, then in each restores the local tools and runs dotnet hardslice check, integration tests included, adds a module
# with dotnet new hardslice-module and a command slice with dotnet new hardslice-command, completes them as the skills of the
# solution do, and runs dotnet hardslice check again.
# Run by the pack job of .github/workflows/release.yml on the packages it built, with --source <folder>, and by its verify job
# on a fresh runner, once the three packages are pushed, with --expect <folder>: the packages NuGet.org serves must have the
# content of the packages the run built and tested, the repository signature NuGet.org adds to each one excepted.
# Everything NuGet and dotnet new keep lives in <work-dir>: the template hive, the user-level NuGet configuration, the package
# folder and the HTTP cache. The template, the analyzers and the tool can therefore come only from that source, never from a
# cache or a build of this repository, and the script checks that each is, byte for byte, the package the source serves.
# It first waits, at most --wait-minutes (60 by default), until the source lists the three packages at that version:
# NuGet.org validates and indexes a pushed package before serving it.
# <work-dir> grows to several gigabytes (packages, two built solutions): --clean removes it when the script ends.
# It needs bash 3.2 or later, curl and python3.
set -euo pipefail

usage() {
  echo "usage: $0 <version> <work-dir> [--source <nuget-v3-index-url | folder>] [--expect <folder>] [--wait-minutes <n>] [--clean]" >&2
  exit 2
}

[[ $# -ge 2 ]] || usage
version="$1"
work="$(cd "$2" && pwd)"
shift 2
nuget_org="https://api.nuget.org/v3/index.json"
source="$nuget_org"
expected_packages=""
wait_minutes=60
clean=false
while [[ $# -gt 0 ]]; do
  case "$1" in
    --source)
      [[ $# -ge 2 ]] || usage
      source="$2"
      shift 2
      ;;
    --expect)
      [[ $# -ge 2 ]] || usage
      expected_packages="$(cd "$2" && pwd)"
      shift 2
      ;;
    --wait-minutes)
      [[ $# -ge 2 && "$2" =~ ^[0-9]+$ ]] || usage
      wait_minutes="$2"
      shift 2
      ;;
    --clean)
      clean=true
      shift
      ;;
    *) usage ;;
  esac
done

if [[ "$clean" == true ]]; then
  trap 'cd / && rm -rf "${work:?}"' EXIT
fi

scripts="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# register_module, register_slice and complete_command, shared with scripts/test-template.sh.
source "$scripts/item-completion.sh"

if [[ "$source" != http://* && "$source" != https://* ]]; then
  source="$(cd "$source" && pwd)"
fi

lowercase() {
  printf '%s' "$1" | tr '[:upper:]' '[:lower:]'
}
lower_version="$(lowercase "$version")"

# Dependencies first: the template pins the analyzers and the tool, so it is pointless without them.
packages=(HardSlice.Analyzers HardSlice.Tool HardSlice.Templates)

# Whether the source lists <package> at the version, or holds it as a .nupkg in a folder. A V3 feed must list it in two resources,
# which NuGet.org updates one after the other once a package is validated: PackageBaseAddress, from which restore and dotnet
# tool restore download it, and RegistrationsBaseUrl/3.6.0, the metadata from which dotnet new install resolves it. The second
# can lag behind by minutes: dotnet new install then answers that the package does not exist.
is_listed() {
  local package="$1" id versions registration
  if [[ "$source" != http://* && "$source" != https://* ]]; then
    [[ -f "$source/$package.$version.nupkg" ]]
    return
  fi
  id="$(lowercase "$package")"
  # 404 until the first version of the package is served.
  versions="$(curl --silent --fail --location "$package_base_address$id/index.json")" || return 1
  python3 -I -c 'import json, sys; sys.exit(0 if sys.argv[1] in json.load(sys.stdin)["versions"] else 1)' "$lower_version" <<< "$versions" || return 1
  registration="$(curl --silent --fail --location --compressed "$registrations_base_url$id/index.json")" || return 1
  # A page of the index lists its versions inline, or only its address when the package has many versions.
  python3 -I -c '
import gzip, json, sys, urllib.request
def versions(page):
    if "items" not in page:
        with urllib.request.urlopen(page["@id"]) as response:
            body = response.read()
        # NuGet.org serves the pages of RegistrationsBaseUrl/3.6.0 compressed.
        page = json.loads(gzip.decompress(body) if body[:2] == b"\x1f\x8b" else body)
    return [leaf["catalogEntry"]["version"].lower() for leaf in page["items"]]
pages = json.load(sys.stdin)["items"]
sys.exit(0 if any(sys.argv[1] in versions(page) for page in pages) else 1)' "$lower_version" <<< "$registration"
}

if [[ "$source" == http://* || "$source" == https://* ]]; then
  resources="$(curl --silent --fail --location "$source")"
  resource() {
    python3 -I -c '
import json, sys
resources = json.load(sys.stdin)["resources"]
print(next(r["@id"] for r in resources if r["@type"] == sys.argv[1]).rstrip("/") + "/")' "$1" <<< "$resources"
  }
  package_base_address="$(resource PackageBaseAddress/3.0.0)"
  registrations_base_url="$(resource RegistrationsBaseUrl/3.6.0)"
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

# The packages as the source serves them, downloaded once: their bytes, their content and their metadata are checked from these files.
served="$work/served"
mkdir -p "$served"
for package in "${packages[@]}"; do
  if [[ "$source" != http://* && "$source" != https://* ]]; then
    cp "$source/$package.$version.nupkg" "$served/$package.nupkg"
  else
    id="$(lowercase "$package")"
    curl --silent --fail --location --output "$served/$package.nupkg" "$package_base_address$id/$lower_version/$id.$lower_version.nupkg"
  fi
done

echo "==> Checking the README, the icon and the release notes of each package"
for package in "${packages[@]}"; do
  python3 -I "$scripts/nupkg.py" metadata "$served/$package.nupkg" "$version"
done

if [[ -n "$expected_packages" ]]; then
  echo "==> Checking that $source serves the packages of $expected_packages"
  for package in "${packages[@]}"; do
    if [[ ! -f "$expected_packages/$package.$version.nupkg" ]]; then
      echo "error: $expected_packages holds no $package.$version.nupkg to compare with." >&2
      exit 1
    fi
    served_digest="$(python3 -I "$scripts/nupkg.py" digest "$served/$package.nupkg")"
    expected_digest="$(python3 -I "$scripts/nupkg.py" digest "$expected_packages/$package.$version.nupkg")"
    if [[ -z "$served_digest" || "$served_digest" != "$expected_digest" ]]; then
      echo "error: the $package $version that $source serves is not the one of $expected_packages." >&2
      exit 1
    fi
    echo "$package $version has the content of $expected_packages/$package.$version.nupkg."
  done
fi

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

# Fails unless <file> holds the very bytes of <package> that the source serves.
expect_from_source() {
  local package="$1" file="$2"
  if [[ ! -f "$file" ]]; then
    echo "error: $package $version is not at $file." >&2
    exit 1
  fi
  if [[ "$(sha512 < "$file")" != "$(sha512 < "$served/$package.nupkg")" ]]; then
    echo "error: $file is not the $package $version that $source serves." >&2
    exit 1
  fi
  echo "$package $version comes from $source."
}

# The package NuGet restored into the package folder, which only the commands of this script fill.
restored_package() {
  local id
  id="$(lowercase "$1")"
  printf '%s' "$NUGET_PACKAGES/$id/$lower_version/$id.$lower_version.nupkg"
}

# dotnet new lists each template package it installed with its version and the source it took it from.
installed="$(dotnet new uninstall)"
if ! grep -qxF "   HardSlice.Templates" <<< "$installed" \
  || ! grep -qxF "      Version: $version" <<< "$installed" \
  || ! grep -qxF "         NuGetSource: $source" <<< "$installed"; then
  echo "error: dotnet new does not list HardSlice.Templates $version from $source among its installed templates:" >&2
  echo "$installed" >&2
  exit 1
fi
expect_from_source HardSlice.Templates "$DOTNET_CLI_HOME/.templateengine/packages/HardSlice.Templates.$version.nupkg"

# Generates <name> with the remaining options and checks it, then adds the module Payments and its command slice IssueInvoice
# with the item templates, registered with the lines they print and completed, and checks it again.
verify_solution() {
  local name="$1"
  shift
  echo "==> dotnet new hardslice-api --name $name $*"
  dotnet new hardslice-api --name "$name" "$@"

  app="$work/$name"
  app_name="$name"
  app_src="$app/src/$name"
  if ! grep -qF "<GlobalPackageReference Include=\"HardSlice.Analyzers\" Version=\"$version\" />" "$app/Directory.HardSlice.props"; then
    echo "error: $name does not pin HardSlice.Analyzers $version in Directory.HardSlice.props." >&2
    exit 1
  fi

  echo "==> $name: dotnet tool restore, then dotnet hardslice check"
  (cd "$app" && dotnet tool restore && dotnet hardslice check)

  echo "==> $name: dotnet new hardslice-module and hardslice-command, completed, then dotnet hardslice check"
  local module_output command_output
  module_output="$(cd "$app" && dotnet new hardslice-module --project "src/$name/$name.csproj" --name Payments)"
  printf '%s\n' "$module_output"
  register_module "$module_output"
  command_output="$(cd "$app" && dotnet new hardslice-command --project "src/$name/$name.csproj" --module Payments --name IssueInvoice)"
  printf '%s\n' "$command_output"
  register_slice hardslice-command "$command_output"
  complete_command Payments IssueInvoice
  (cd "$app" && dotnet hardslice check)
}

verify_solution Rooms
verify_solution Empty --no-sample

echo "==> Checking where the analyzers and the tool were restored from"
for package in HardSlice.Analyzers HardSlice.Tool; do
  expect_from_source "$package" "$(restored_package "$package")"
done

echo "==> HardSlice.Templates $version from $source generates solutions that pass dotnet hardslice check, with and without Bookings, before and after a module and a command slice of its item templates."
