# PB-003 review fixes

Status: approved on 29/10/2026.

## Context

The PB-003 implementation covers the required HS016 scenarios and its nominal analyzer tests pass, but review found three product gaps: the NuGet package exposes the analyzer as a runtime library instead of a compiler analyzer, aliases can hide composed types from another slice, and implicit operations can consume another slice's types without naming them.

## Selected approach

- Pack `HardSlice.Analyzers.dll` only under `analyzers/dotnet/cs`, suppress package dependencies, and remove compile and runtime assets.
- Add a cross-platform integration test that packs the analyzer, restores a temporary consumer from a local feed, proves HS016 fails a violating build, and proves a conforming publish has no HardSlice runtime dependency.
- Keep the existing syntax analysis for explicit names so diagnostics retain their precise locations and the covered PB-003 matrix remains stable.
- Resolve aliases explicitly at their use sites and recursively inspect composed types, including generic arguments, arrays, tuples, nullable types, nested types, pointers, and function-pointer signatures.
- Add targeted operation analysis only for implicit boundaries: target-typed construction, values exposed by module-contract members, user-defined operators and conversions, and unnamed indexers.
- Route syntax and operation findings through one concurrent reporter keyed by syntax location and target type so the hybrid analysis produces no duplicate diagnostics.
- Leave the SDK, xUnit, and test-runner configuration unchanged. The observed zero-test result came from passing the unsupported `--nologo` option to Microsoft.Testing.Platform; the repository's documented command already passes all 41 tests.

## Alternatives rejected

- Extending syntax analysis alone would require an increasingly fragile list of C# syntax kinds and would still struggle with types propagated implicitly through allowed APIs.
- Replacing the analyzer with operation-only analysis would be a larger rewrite, lose precise type-token locations, and risk regressions across declarations, attributes, constraints, and inheritance.
- A custom `.nuspec` would duplicate package metadata already maintained by MSBuild.
- Registering a DLL from `tools` through a `buildTransitive` target would be less conventional, more fragile in IDEs, and easier to leak into runtime assets than the standard analyzer package path.
- Pinning or downgrading the SDK or xUnit would not address the unsupported command-line option and would introduce unrelated toolchain churn.

## Validation

Run `dotnet restore`, `dotnet build --no-restore`, and `dotnet test --no-build`. Run the analyzer package integration test in Release, inspect the produced package for analyzer-only assets, verify the conforming consumer's publish output and `.deps.json`, run the package vulnerability audit, and confirm `git diff --check` is clean.
