# AGENTS.md

MyApp is an ASP.NET Core API organized in vertical slices around a rich domain. HardSlice enforces this architecture at build time: a violation fails `dotnet build` or `dotnet test`, whoever wrote the code.

## Project map

| Path | Contains |
|---|---|
| `src/MyApp.Domain/<Module>/` | Aggregates, value objects and business rules. Depends on the base class library only. |
| `src/MyApp/Features/<Module>/<Slice>/` | One use case: its command or query, handler and endpoint. |
| `src/MyApp/Features/<Module>/` | Module registration (`<Module>Module.cs`), EF Core mapping of its aggregates (`<Aggregate>Configuration.cs`) and the public contract shared by its slices. |
| `src/MyApp/Shared/` | Cross-cutting code only, never a use case. |
| `src/MyApp/Shared/Persistence/` | `AppDbContext`, its registration and the migrations. It declares no module type. |
| `tests/MyApp.Tests/` | Unit tests of the domain, at the same path as the code they test. |
| `tests/MyApp.IntegrationTests/` | Tests on a real PostgreSQL (Testcontainers), at the same path as the slice they test. |
| `tests/MyApp.ArchitectureTests/` | Architecture tests. |
| `docs/rules/` | One sheet per HardSlice rule: what it forbids and how to fix it. |

Modules and slices:

- `Bookings`: `ConfirmBooking`, `CancelBooking`. Mapping: `BookingConfiguration`. Public contract: none yet.

## Golden rules

1. For a use case, read its slice and the aggregate it uses, nothing else.
2. A slice never uses a type from another slice (HS016). Share through the domain, a domain event or the module public contract.
3. Business rules live in the domain aggregates, not in handlers or endpoints.
4. The domain project references nothing but the base class library.
5. `Shared` holds cross-cutting code only and never depends on `Features`.
6. Every behavior change comes with a test at the same path as the code it covers.
7. Never weaken a rule to make the build pass: see [Forbidden](#forbidden).

## Adding a slice

1. Check whether the aggregate already owns the business rule. If not, add it to the aggregate with its unit test.
2. Create `src/MyApp/Features/<Module>/<Slice>/<Slice>.cs` with namespace `MyApp.Features.<Module>.<Slice>`: the command or query, an `internal` handler and an `internal` endpoint. The handler reaches aggregates through `AppDbContext.Set<T>()`. Follow `ConfirmBooking` as the example.
3. Register the handler and map the endpoint in `<Module>Module.cs`.
4. Add its tests in `tests/MyApp.IntegrationTests/Features/<Module>/<Slice>/`.
5. Add the slice to the map above, then verify.

## Changing the database

1. Map a new aggregate in `src/MyApp/Features/<Module>/<Aggregate>Configuration.cs`: an `internal` `IEntityTypeConfiguration<T>` in fluent API. Never add EF Core attributes or references to the domain, and never declare module types in `AppDbContext`.
2. After any mapping change, add a migration. Never edit a migration that may already be applied.

   ```text
   dotnet tool restore
   dotnet ef migrations add <Name> --project src/MyApp --output-dir Shared/Persistence/Migrations
   ```

3. The application never migrates its database at startup. Apply migrations with `dotnet ef database update --project src/MyApp`, which reads `ConnectionStrings:Database`. Integration tests apply them to their own container and fail if the model has changes missing from the migrations.

## Verify

```text
bash scripts/check.sh     # macOS, Linux, Git Bash
pwsh scripts/check.ps1    # Windows, PowerShell
```

Both scripts build the solution (restoring if needed), then run the unit tests, the architecture tests and the integration tests. Integration tests need a Docker-compatible container runtime: without one, the scripts stop before them and say so. They exit with a non-zero code on any failure.

**You are not done until the check script exits 0.** Do not hand back, report success or commit while it fails. If it cannot pass without breaking a rule, stop and explain the conflict instead.

## Fixing HS016

HS016 means a type in one slice uses a type from another slice.

1. Read the diagnostic: it names the offending type, its slice, and the type from the other slice it must stop using.
2. Open `docs/rules/HS016.md` and apply the first fix that matches why the other slice's type was needed: move the shared rule into the aggregate, raise a domain event, or use the module public contract.
3. Run the check script again.

## Forbidden

Fix the code, never the rule. Never make a HardSlice diagnostic or an architecture test pass by:

- suppressing it: `#pragma warning disable`, `[SuppressMessage]`, `<NoWarn>`, `<WarningsNotAsErrors>`;
- lowering its severity: `dotnet_diagnostic.HS016.severity` in `.editorconfig` or `.globalconfig`, or turning off `TreatWarningsAsErrors`;
- turning analyzers off: `<RunAnalyzers>false</RunAnalyzers>` or `<RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild>`;
- removing, editing or downgrading `HardSlice.Analyzers` or its reference in `Directory.Packages.props`;
- deleting, skipping or editing tests in `tests/MyApp.ArchitectureTests/`, or editing `scripts/check.sh` and `scripts/check.ps1`;
- any other workaround listed in `docs/rules/HS016.md`, such as reflection or moving a slice's type into the module namespace.

An exception requires an explicit decision from a maintainer.
