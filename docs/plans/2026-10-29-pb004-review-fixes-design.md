# PB-004 review fixes

Status: approved on 29/10/2026.

## Context

The PB-004 implementation makes HS016 actionable and its tests pass, but review found three gaps: the diagnostic's local documentation path is not meaningful for a consumer that installs only the analyzer package, the rule sheet describes suppressions more absolutely than the HS000 product contract, and the documentation test checks keywords without proving their meaning or keeping the diagnostic example synchronized.

## Selected approach

- Keep `HardSlice.Analyzers` as an analyzer-only package. Do not add a build target that writes documentation into consumer source trees.
- Define the generated `hardslice-api` template as the supported greenfield installation path for the first increment. It owns the local `docs/rules/HS016.md` referenced by the diagnostic. Direct analyzer-package installation remains outside this increment and will gain a stable public help link when the adoption path is implemented.
- Extend PB-005 acceptance so its end-to-end template test proves that the generated solution contains the documentation path advertised by HS016.
- Describe suppression mechanisms as invalid repairs, while preserving the future maintainer-approved HS000 justification path.
- Strengthen the documentation test by matching the diagnostic's exact rendered example and by checking the required prohibitions only inside the `Forbidden workarounds` section.

## Alternatives rejected

- A `buildTransitive` target that copies Markdown into every consumer project would make package restore or build mutate the source tree and could overwrite user-owned documentation.
- NuGet `contentFiles` would not guarantee that `docs/rules/HS016.md` exists physically relative to the consumer solution, so the diagnostic path would remain unreliable.
- Publishing a public help URI now would introduce a dependency on documentation hosting before that distribution path is part of the roadmap.
- Keeping absolute “never suppress” wording would conflict with the approved HS000 exception mechanism.
- Keyword-only documentation assertions would allow the rule sheet to describe a forbidden mechanism as permitted or to show a stale diagnostic example without failing tests.

## Validation

Run `dotnet build --no-restore`, `dotnet test --no-build`, the focused Bookings reference tests, and `git diff --check`. Confirm the HS016 rule sheet remains untracked only until it is included with the complete PB-004 implementation, and run the security pattern audit over the final diff.
