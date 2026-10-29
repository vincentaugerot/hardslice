# AGENTS.md

MyApp is an ASP.NET Core API organized in vertical slices around a rich domain. HardSlice enforces this architecture at build time: a violation fails `dotnet build` or `dotnet test`, whoever wrote the code.

## Project map

| Path | Contains |
|---|---|
| `src/MyApp.Domain/<Module>/` | Aggregates, value objects and business rules. Depends on the base class library only. |
| `src/MyApp.Domain/<Module>/Events/` | Domain events raised by the module aggregates. |
| `src/MyApp/Features/<Module>/<Slice>/` | One use case: its command or query, validator, handler and endpoint. |
| `src/MyApp/Features/<Module>/` | Module registration (`<Module>Module.cs`), EF Core mapping of its aggregates (`<Aggregate>Configuration.cs`) and the public contract shared by its slices. |
| `src/MyApp/Shared/` | Cross-cutting code only, never a use case. |
| `src/MyApp/Shared/Messaging/` | The in-house mediator: `IMediator`, `ICommand<T>`, `IQuery<T>`, `IRequestHandler<,>`, `IPipelineBehavior<,>`. |
| `src/MyApp/Shared/Behaviors/` | The pipeline every request goes through, in order: logs, validation, then one transaction per command. Also the HTTP answers to validation and domain errors. |
| `src/MyApp/Shared/Persistence/` | `AppDbContext`, its registration and the migrations. It declares no module type. |
| `tests/MyApp.Tests/` | Unit tests of the domain, the validators and `Shared`, at the same path as the code they test. |
| `tests/MyApp.IntegrationTests/` | Tests on a real PostgreSQL (Testcontainers), at the same path as the slice they test. |
| `tests/MyApp.ArchitectureTests/` | Architecture tests of HS001, HS012, HS016 and HS017, on the compiled assemblies. |
| `docs/rules/` | One sheet per HardSlice rule: what it forbids and how to fix it. |
| `hardslice.json` | HardSlice configuration read by the architecture tests: the `slices` style and the folders allowed in `Shared`. |
| `.editorconfig` | Severity of the HardSlice analyzer rules. |

Modules and slices:

- `Bookings`: `ConfirmBooking`, `CancelBooking` (commands), `GetBooking` (query). Mapping: `BookingConfiguration`. Public contract: none yet.

## Golden rules

1. For a use case, read its slice and the aggregate it uses, nothing else.
2. A slice never uses a type from another slice, and its handler, validator and endpoint stay `internal` (HS016). Share through the domain, a domain event or the module public contract.
3. A module uses another module only through its public contract: the types directly in `MyApp.Features.<Module>` (HS012).
4. Business rules live in the domain aggregates, not in handlers or endpoints.
5. The domain project references nothing but the base class library (HS001).
6. `Shared` holds cross-cutting code only, in the folders `hardslice.json` allows, and never depends on `Features` (HS017).
7. Every behavior change comes with a test at the same path as the code it covers.
8. Never weaken a rule to make the build pass: see [Forbidden](#forbidden).

## Adding a slice

1. Check whether the aggregate already owns the business rule. If not, add it to the aggregate with its unit test.
   A broken rule throws `DomainException`, answered with 400. A rule broken by the current state, such as an overlapping booking, throws a `ConflictException`, answered with 409.
   When the use case produces a fact other code may react to, the aggregate records a domain event: a `record` named in the past tense, implementing `IDomainEvent`, in `src/MyApp.Domain/<Module>/Events/`. Follow `BookingConfirmed`, recorded by `Booking.Confirm` and exposed through `Booking.DomainEvents`. Events are not stored: the EF Core mapping ignores `DomainEvents`. They are not published to handlers yet.
2. Create `src/MyApp/Features/<Module>/<Slice>/<Slice>.cs` with namespace `MyApp.Features.<Module>.<Slice>`. Follow `ConfirmBooking` as the example:
   - a public request: an `ICommand<TResponse>` when it changes state, an `IQuery<TResponse>` when it only reads. The response type is exact (invariant), matching its handler and pipeline;
   - an `internal` validator, an `AbstractValidator<TRequest>` that checks the shape of the input (required fields, formats, lengths). A failure is answered with 400 naming the fields, before the handler runs. Business rules stay in the aggregate;
   - an `internal` handler, an `IRequestHandler<TRequest, TResponse>` that reaches aggregates through `AppDbContext.Set<T>()`. It never calls `SaveChangesAsync`: each command runs in one transaction that the pipeline saves and commits, and any exception rolls it back. A failed transaction also detaches tracked entities: later commands in the same scope must reload them. Save early only to translate a database error, as `ConfirmBooking` does;
   - for a query, follow `GetBooking` instead: a public response DTO, and a handler that reads with `AsNoTracking()` and projects into that DTO with `Select`. It never loads an aggregate, never saves, and runs outside any transaction. An unknown id is a `null` response, answered with 404;
   - an `internal` endpoint that sends the request through `IMediator` and returns `TypedResults`. It never calls a handler.
3. In `<Module>Module.cs`, register the handler and the validator by their contracts (`IRequestHandler<,>`, `IValidator<>`) and map the endpoint.
4. Add the validator tests in `tests/MyApp.Tests/Features/<Module>/<Slice>/` and the integration tests, an invalid request included, in `tests/MyApp.IntegrationTests/Features/<Module>/<Slice>/`.
5. Add the slice to the map above, then verify.

## Changing the database

1. Map a new aggregate in `src/MyApp/Features/<Module>/<Aggregate>Configuration.cs`: an `internal` `IEntityTypeConfiguration<T>` in fluent API. Never add EF Core attributes or references to the domain, and never declare module types in `AppDbContext`.
2. After any mapping change, add a migration. A constraint EF Core cannot model, such as the exclusion constraint that keeps confirmed bookings of a room from overlapping, goes in a migration through `migrationBuilder.Sql`. Never edit a migration that may already be applied.

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

## Fixing a HardSlice failure

A HardSlice failure starts with its rule: `HS016` from the analyzer at build time, or `HS001`, `HS012`, `HS016`, `HS017` from the architecture tests.

1. Read the diagnostic: it names the offending symbol, the rule, the expected fix and its sheet after `Doc:`.
2. Open the sheet and apply the first fix that matches: `docs/rules/HS001.md` (domain dependencies), `docs/rules/HS012.md` (modules), `docs/rules/HS016.md` (slices), `docs/rules/HS017.md` (Shared).
3. Run the check script again.

A message starting with `hardslice.json:` means the configuration is missing or invalid: restore it, never adapt it to the code.

## Forbidden

Fix the code, never the rule. Never make a HardSlice diagnostic or an architecture test pass by:

- suppressing it: `#pragma warning disable`, `[SuppressMessage]`, `<NoWarn>`, `<WarningsNotAsErrors>`;
- lowering its severity: `dotnet_diagnostic.HS016.severity` in `.editorconfig` or `.globalconfig`, or turning off `TreatWarningsAsErrors`;
- editing `.editorconfig` or `hardslice.json`, for example to allow a new folder in `Shared`;
- turning analyzers off: `<RunAnalyzers>false</RunAnalyzers>` or `<RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild>`;
- removing, editing or downgrading `HardSlice.Analyzers` or its reference in `Directory.Packages.props`;
- deleting, skipping or editing tests in `tests/MyApp.ArchitectureTests/`, or editing `scripts/check.sh` and `scripts/check.ps1`;
- any other workaround listed in the rule sheets of `docs/rules/`, such as reflection, a public handler or moving a slice's type into the module namespace.

An exception requires an explicit decision from a maintainer.
