# PB-008 review fixes

Status: approved on 29/10/2026.

## Context

The PB-008 demonstration follows the expected HS016 correction loop, but its verifier trusts mutable state inside the agent workspace. It runs the workspace's `scripts/check.sh` before validating changes, uses a movable Git tag as its baseline, recognizes tests only by method name, and does not prove that the generated template is green before applying the violating fixture.

## Selected approach

- Verify the freshly generated template before applying the fixture, then require the exact `CancelBookingHandler` to `BookingSchedule` HS016 diagnostic after the overlay.
- Keep the fixture commit only as a convenience for the development agent. Do not use its Git tag, history, index, configuration, or hooks as verification inputs.
- During `verify`, regenerate the template and fixture in a separate temporary directory owned by the harness. Compare the candidate filesystem with this clean baseline before executing any candidate content.
- Accept changes only to regular C# files below `src/` and `tests/Demo.Tests/`. Reject changes to scripts, projects, configuration, architecture tests, documentation, symlinks, and special files.
- Copy the accepted C# changes into the clean verification tree and run the check script from that tree. Its packages, NuGet configuration, scripts, and project files therefore come from the trusted repository state.
- Protect each fixture test by its file path and by the exact attributes, method declarations, and assertion lines present in the baseline. Additions remain possible, but removing, renaming, skipping, or weakening the recorded contracts fails before the build.
- Replace the test-name parser with path-specific contract comparison. Pass application paths explicitly to every helper, and keep the forbidden-source pattern in one canonical script constant.
- Describe the verifier's guarantees in the README without duplicating its implementation regex.

## Alternatives rejected

- Reordering the existing checks would still rely on mutable Git state and would eventually execute inside the agent-controlled tree.
- Making every fixture test byte-for-byte immutable would reject legitimate mechanical updates when the architectural fix changes a domain API.
- Parsing test names more carefully would still miss removed assertions and weakened test bodies.
- Treating a read-only file beside the agent workspace as an immutable baseline would not create a real trust boundary for processes running as the same user.

## Implementation plan

1. Refactor `demo-hs016-agent-fix.sh` into explicit generation, diagnostic, filesystem-comparison, test-contract, source-policy, and verification steps.
2. Update the README and trace so their claims match the hardened verifier.
3. Run syntax and text-hygiene checks.
4. Exercise the nominal prepare and verify paths with the documented architectural correction.
5. Exercise negative cases for a modified check script, a moved Git reference, a removed assertion, and a renamed test. Confirm candidate code is never executed before validation.
6. Run the template integration test and inspect the final diff for security regressions and unrelated changes.

## Validation

Run `bash -n` on all changed scripts, `scripts/test-template.sh`, and a complete PB-008 prepare/fix/verify cycle. Confirm the verifier rejects every negative case before running the clean check, and run `git diff --check` plus the security pattern audit over the final changes.
