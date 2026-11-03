#!/usr/bin/env bash
# Pushes the three HardSlice packages of <folder> at <version> to NuGet.org, for the publish job of .github/workflows/release.yml.
# A version pushed there is never replaced, so nothing is pushed unless:
# - the tag v<version> of <remote> still points to <commit>, the commit the run built, tested and packed: a tag moved while the
#   run waited for its approval would otherwise publish the packages of a commit it no longer names;
# - every package that NuGet.org already serves at that version has the content of the one of <folder>, the repository
#   signature NuGet.org adds excepted (scripts/nupkg.py digest): a rerun after a partial push never mixes two commits.
# Then it pushes, in the order of their dependencies, the packages NuGet.org does not serve yet, with $NUGET_API_KEY. A package
# pushed but still being validated is not served yet: its push is then refused as a duplicate, and the job is rerun once
# NuGet.org serves it. --feed reads the packages served from another flat container or from a folder, and --dry-run only says
# what it would push: both serve the tests.
set -euo pipefail

usage() {
  echo "usage: $0 <version> <folder> --commit <sha> --remote <git-url> [--feed <flat-container-url | folder>] [--dry-run]" >&2
  exit 2
}

[[ $# -ge 2 ]] || usage
version="$1"
folder="$2"
shift 2
commit=""
remote=""
feed="https://api.nuget.org/v3-flatcontainer/"
dry_run=false
while [[ $# -gt 0 ]]; do
  case "$1" in
    --commit | --remote | --feed)
      [[ $# -ge 2 ]] || usage
      case "$1" in
        --commit) commit="$2" ;;
        --remote) remote="$2" ;;
        --feed) feed="$2" ;;
      esac
      shift 2
      ;;
    --dry-run)
      dry_run=true
      shift
      ;;
    *) usage ;;
  esac
done
[[ -n "$commit" && -n "$remote" ]] || usage

scripts="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
packages=(HardSlice.Analyzers HardSlice.Tool HardSlice.Templates)
tag="v$version"
lower_version="$(printf '%s' "$version" | tr '[:upper:]' '[:lower:]')"
served="$(mktemp -d)"
trap 'rm -rf "${served:?}"' EXIT

# The commit the tag names on the remote: the peeled commit of an annotated tag, or the commit of a lightweight one.
tagged="$(git ls-remote --tags "$remote" "refs/tags/$tag" "refs/tags/$tag^{}" |
  awk '$2 ~ /\^\{\}$/ { peeled = $1 } { plain = $1 } END { print (peeled != "" ? peeled : plain) }')"
if [[ "$tagged" != "$commit" ]]; then
  echo "error: the tag $tag of $remote points to ${tagged:-nothing}, not to $commit, the commit this run built and tested: nothing is pushed. Run the workflow of the tag again instead." >&2
  exit 1
fi
echo "The tag $tag still points to $commit."

# The digest of the content of <nupkg>: exits the script when it cannot be read, so that two failures never compare equal.
content_digest() {
  local digest
  if ! digest="$(python3 -I "$scripts/nupkg.py" digest "$1")" || [[ -z "$digest" ]]; then
    echo "error: cannot read the content of $1: nothing is pushed." >&2
    exit 1
  fi
  printf '%s' "$digest"
}

# Downloads <package> at the version as the feed serves it into $served, and fails if it is served without having its content.
# Returns 1 when the feed does not serve it.
served_matches() {
  local package="$1" id status
  if [[ "$feed" != http://* && "$feed" != https://* ]]; then
    [[ -f "$feed/$package.$version.nupkg" ]] || return 1
    cp "$feed/$package.$version.nupkg" "$served/$package.nupkg"
  else
    id="$(printf '%s' "$package" | tr '[:upper:]' '[:lower:]')"
    status="$(curl --silent --location --output "$served/$package.nupkg" --write-out '%{http_code}' \
      "${feed%/}/$id/$lower_version/$id.$lower_version.nupkg")"
    case "$status" in
      200) ;;
      404) return 1 ;;
      *)
        echo "error: ${feed%/} answered $status for $package $version: nothing is pushed." >&2
        exit 1
        ;;
    esac
  fi
  if [[ "$(content_digest "$served/$package.nupkg")" != "$(content_digest "$folder/$package.$version.nupkg")" ]]; then
    echo "error: NuGet.org already serves a $package $version whose content differs from $folder/$package.$version.nupkg: it was pushed from another commit. Nothing is pushed: publish the next preview instead." >&2
    exit 1
  fi
}

# Every package checked before the first push.
to_push=()
for package in "${packages[@]}"; do
  if [[ ! -f "$folder/$package.$version.nupkg" ]]; then
    echo "error: $folder holds no $package.$version.nupkg." >&2
    exit 1
  fi
  if served_matches "$package"; then
    echo "$package $version is already served with the content of this run: skipped."
  else
    to_push+=("$package")
  fi
done

# The template last: it pins the two others, which must be there when it appears.
for package in ${to_push[@]+"${to_push[@]}"}; do
  if [[ "$dry_run" == true ]]; then
    echo "Would push $package $version."
    continue
  fi
  if ! dotnet nuget push "$folder/$package.$version.nupkg" --api-key "${NUGET_API_KEY:?}" --source https://api.nuget.org/v3/index.json; then
    echo "error: the push of $package $version failed. If NuGet.org already holds it, still validating, rerun the job once it serves the package: it compares it first." >&2
    exit 1
  fi
done
