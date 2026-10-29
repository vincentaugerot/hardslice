# PB-001 review fixes

Status: approved on 29/10/2026.

## Context

The PB-001 acceptance commands pass, but the review identified three maintenance and supply-chain issues: the analyzer dependency test rejects a dependency required by Roslyn's analyzer contract, the CI actions use mutable major-version tags, and the shared package metadata comment no longer describes its actual scope.

## Selected approach

- Keep PB-003 out of scope: do not add a placeholder analyzer.
- Extend the analyzer assembly allowlist only with `System.Collections.Immutable`, which is part of the Roslyn API surface used by `DiagnosticAnalyzer.SupportedDiagnostics`.
- Reword the test comment so it describes known compiler-host dependencies rather than claiming every other dependency fails to load.
- Pin the existing v5 action lines to the full commits currently referenced by their official release tags, retaining the patch versions in comments:
  - `actions/checkout` v5.1.0;
  - `actions/setup-dotnet` v5.4.0.
- Reword the central metadata comment to cover every distributable HardSlice package, including analyzers, templates, and the future adoption tool.

## Alternatives rejected

- Allowing every current Roslyn transitive dependency would weaken the test unnecessarily.
- Adding a minimal production analyzer would begin PB-003 before PB-002 and expand this corrective change beyond PB-001.
- Keeping mutable action tags would leave the reported supply-chain risk unresolved.

## Validation

Run `dotnet restore`, `dotnet build --no-restore`, and `dotnet test --no-build`. Confirm the workflow contains only full-length action SHAs, inspect the final diff, and rerun the package vulnerability audit.
