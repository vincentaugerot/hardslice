#!/usr/bin/env bash
# Packs HardSlice, installs the hardslice-api template, generates a solution in a temporary directory,
# verifies it with scripts/check.sh and check.ps1, then proves HS016 fails both and points to a rule doc that exists.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
work="$(mktemp -d "${TMPDIR:-/tmp}/hardslice-template.XXXXXX")"
trap 'rm -rf "$work"' EXIT

app_name="Demo"
app="$work/$app_name"

"$repo_root/scripts/generate-app.sh" "$work"

if ! command -v pwsh > /dev/null; then
  echo "error: pwsh is required to verify scripts/check.ps1." >&2
  exit 1
fi

# The check scripts are run from another directory: they must locate the solution themselves.
# dotnet new does not preserve the executable bit, hence the documented 'bash scripts/check.sh' form.
echo "==> Verifying the generated solution with scripts/check.sh"
bash "$app/scripts/check.sh"

echo "==> Verifying the generated solution with scripts/check.ps1"
pwsh -NoProfile -File "$app/scripts/check.ps1"

echo "==> Checking that HS016 is active, fails both check scripts and its Doc path exists"
cat > "$app/src/$app_name/Features/Bookings/CancelBooking/Hs016Probe.cs" <<EOF
namespace $app_name.Features.Bookings.CancelBooking;

internal static class Hs016Probe
{
    internal static readonly Type Target = typeof($app_name.Features.Bookings.ConfirmBooking.ConfirmBookingHandler);
}
EOF

if check_output="$(bash "$app/scripts/check.sh" 2>&1)"; then
  echo "error: scripts/check.sh passed despite a cross-slice reference: HS016 is not active." >&2
  exit 1
fi

doc_path="$(printf '%s\n' "$check_output" | sed -n "s/.*error HS016:.* Doc: '\([^']*\)'.*/\1/p" | head -n 1)"
if [[ -z "$doc_path" ]]; then
  printf '%s\n' "$check_output" >&2
  echo "error: scripts/check.sh failed without an HS016 diagnostic announcing a Doc path." >&2
  exit 1
fi

if check_output="$(pwsh -NoProfile -File "$app/scripts/check.ps1" 2>&1)"; then
  echo "error: scripts/check.ps1 passed despite a cross-slice reference." >&2
  exit 1
fi

if ! printf '%s\n' "$check_output" | grep -q "error HS016:"; then
  printf '%s\n' "$check_output" >&2
  echo "error: scripts/check.ps1 failed without an HS016 diagnostic." >&2
  exit 1
fi

if [[ ! -f "$app/$doc_path" ]]; then
  echo "error: HS016 announces '$doc_path', which does not exist from the generated solution root." >&2
  exit 1
fi

echo "==> Template OK: $app_name passes both check scripts, which fail on HS016 pointing to $doc_path"
