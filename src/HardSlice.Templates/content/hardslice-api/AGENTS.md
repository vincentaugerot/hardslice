# AGENTS.md

MyApp is an ASP.NET Core API organized in vertical slices around a rich domain. HardSlice enforces this architecture at build time: a violation fails `dotnet build` or `dotnet test`, whoever wrote the code.

## Project map

| Path | Contains |
|---|---|
| `src/MyApp.Domain/` | The contracts that classify domain types: `IEntity`, `IAggregateRoot` (an entity) and `IDomainEvent`. |
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
| `tests/MyApp.ArchitectureTests/` | Architecture tests of HS001, HS012 to HS014 and HS016 to HS018, on the compiled assemblies and the test sources. |
| `docs/rules/` | One sheet per HardSlice rule: what it forbids and how to fix it. |
| `hardslice.json` | HardSlice configuration: the `slices` style, the `contracts` that classify entities, events, commands, queries and handlers, and the folders allowed in `Shared`. |
| `.editorconfig` | Severity of the HardSlice analyzer rules. |
| `Directory.Build.props` | Build settings of every project, including `HardSliceAnalysisMode`, set to `All`: every HardSlice analyzer rule is on. |
| `Directory.Build.targets` | Supplies each test project's compilation symbols and compiled source paths to HS014. |
| `.config/dotnet-tools.json` | Local tools: `dotnet-ef` at the version of EF Core, and `dotnet hardslice` at the version of `HardSlice.Analyzers`. |

Modules and slices:

<!--#if (!NoSample) -->
- `Bookings`: `ConfirmBooking`, `CancelBooking` (commands), `GetBooking` (query). Mapping: `BookingConfiguration`. Public contract: `BookingsModule`, which registers and maps the module; no type for other modules yet.
<!--#else -->
- None yet: start with [Adding a first module](#adding-a-first-module).
<!--#endif -->

## Golden rules

1. For a use case, read its slice and the aggregate it uses, nothing else.
2. A slice never uses a type from another slice, and its handler, validator and endpoint stay `internal` (HS016). Share through the domain, a domain event or the module public contract.
3. A module uses another module only through its public contract: the types directly in `MyApp.Features.<Module>` (HS012).
4. Business rules live in the domain aggregates, not in handlers or endpoints. An endpoint never reaches the database: it sends a command or a query through `IMediator` (HS003).
5. The domain project references nothing but the base class library (HS001). Every aggregate root implements `IAggregateRoot`, every child entity `IEntity` (HS902), and a type has one role (HS901).
6. `Shared` holds cross-cutting code only, in the folders `hardslice.json` allows, and never depends on `Features` (HS017).
7. Every behavior change comes with a test at the same path as the code it covers. Every slice has tests at its path under `tests/` (HS018), at least one for a command handler (HS014), and every command with parameters has a validator (HS013).
8. Never weaken a rule to make the build pass: see [Forbidden](#forbidden).
<!--#if (NoSample) -->

## Adding a first module

1. Create the aggregate in `src/MyApp.Domain/<Module>/`, namespace `MyApp.Domain.<Module>`: a `sealed` class implementing `IAggregateRoot` whose state changes only through its methods, created by a static factory method that checks its rules, with a private parameterless constructor for persistence. Unit test it in `tests/MyApp.Tests/Domain/<Module>/`.
2. Create `src/MyApp/Features/<Module>/<Module>Module.cs`, namespace `MyApp.Features.<Module>`: a public static class with `Add<Module>Module(this IServiceCollection services)`, which registers the slices, and `Map<Module>Module(this IEndpointRouteBuilder endpoints)`, which maps them under one `MapGroup`. Call both from `Program.cs`.
3. Map the aggregate and add the first migration: see [Changing the database](#changing-the-database).
4. Add its slices as below, then add the module to the map above.
<!--#endif -->

## Adding a slice

1. Check whether the aggregate already owns the business rule. If not, add it to the aggregate with its unit test.
<!--#if (!NoSample) -->
   A broken rule throws `DomainException`, answered with 400. A rule broken by the current state, such as an overlapping booking, throws a `ConflictException`, answered with 409.
   When the use case produces a fact other code may react to, the aggregate records a domain event: a `record` named in the past tense, implementing `IDomainEvent`, in `src/MyApp.Domain/<Module>/Events/`. Follow `BookingConfirmed`, recorded by `Booking.Confirm` and exposed through `Booking.DomainEvents`. Events are not stored: the EF Core mapping ignores `DomainEvents`. They are not published to handlers yet.
2. Create `src/MyApp/Features/<Module>/<Slice>/<Slice>.cs` with namespace `MyApp.Features.<Module>.<Slice>`. Follow `ConfirmBooking` as the example:
<!--#else -->
   A broken rule throws `DomainException`, answered with 400. A rule broken by the current state, such as a duplicate, throws a `ConflictException`, answered with 409.
   When the use case produces a fact other code may react to, the aggregate records a domain event: a `record` named in the past tense, implementing `IDomainEvent`, in `src/MyApp.Domain/<Module>/Events/`, kept in a private list and exposed through a read-only `DomainEvents` property. Events are not stored: the EF Core mapping ignores `DomainEvents`. They are not published to handlers yet.
2. Create `src/MyApp/Features/<Module>/<Slice>/<Slice>.cs` with namespace `MyApp.Features.<Module>.<Slice>`, containing:
<!--#endif -->
   - a public request: an `ICommand<TResponse>` when it changes state, an `IQuery<TResponse>` when it only reads. The response type is exact (invariant), matching its handler and pipeline;
   - an `internal` validator, an `AbstractValidator<TRequest>`, required for a command with parameters, that checks the shape of the input (required fields, formats, lengths). A failure is answered with 400 naming the fields, before the handler runs. Business rules stay in the aggregate;
<!--#if (!NoSample) -->
   - an `internal` handler, an `IRequestHandler<TRequest, TResponse>` that reaches aggregates through `AppDbContext.Set<T>()`. It never calls `SaveChangesAsync`: each command runs in one transaction that the pipeline saves and commits, and any exception rolls it back. A failed transaction also detaches tracked entities: later commands in the same scope must reload them. Save early only to translate a database error, as `ConfirmBooking` does;
   - for a query, follow `GetBooking` instead: a public response DTO, and a handler that reads with `AsNoTracking()` and projects into that DTO with `Select`. It never loads an aggregate, never saves, and runs outside any transaction. An unknown id is a `null` response, answered with 404;
<!--#else -->
   - an `internal` handler, an `IRequestHandler<TRequest, TResponse>` that reaches aggregates through `AppDbContext.Set<T>()`. It never calls `SaveChangesAsync`: each command runs in one transaction that the pipeline saves and commits, and any exception rolls it back. A failed transaction also detaches tracked entities: later commands in the same scope must reload them. Save early only to translate a database error, such as a violated constraint, into a domain exception;
   - for a query, instead of that handler: a public response DTO, and a handler that reads with `AsNoTracking()` and projects into that DTO with `Select`. It never loads an aggregate, never saves, and runs outside any transaction. An unknown id is a `null` response, answered with 404;
<!--#endif -->
   - an `internal` endpoint that sends the request through `IMediator` and returns `TypedResults`. It never calls a handler and never uses `AppDbContext` (HS003).
3. In `<Module>Module.cs`, register the handler and the validator by their contracts (`IRequestHandler<,>`, `IValidator<>`) and map the endpoint.
4. Add the validator tests in `tests/MyApp.Tests/Features/<Module>/<Slice>/` and the integration tests, an invalid request included, in `tests/MyApp.IntegrationTests/Features/<Module>/<Slice>/`. Renaming or moving a slice moves these folders too.
5. Add the slice to the map above, then verify.

## Changing the database

1. Map a new aggregate in `src/MyApp/Features/<Module>/<Aggregate>Configuration.cs`: an `internal` `IEntityTypeConfiguration<T>` in fluent API. The aggregate implements `IAggregateRoot` (HS902). Never add EF Core attributes or references to the domain, and never declare module types in `AppDbContext`.
<!--#if (!NoSample) -->
2. After any mapping change, add a migration. A constraint EF Core cannot model, such as the exclusion constraint that keeps confirmed bookings of a room from overlapping, goes in a migration through `migrationBuilder.Sql`. Never edit a migration that may already be applied.
<!--#else -->
2. After any mapping change, add a migration. A constraint EF Core cannot model, such as an exclusion constraint, goes in a migration through `migrationBuilder.Sql`. Never edit a migration that may already be applied.
<!--#endif -->

   ```text
   dotnet tool restore
   dotnet ef migrations add <Name> --project src/MyApp --output-dir Shared/Persistence/Migrations
   ```

3. The application never migrates its database at startup. Apply migrations with `dotnet ef database update --project src/MyApp`, which reads `ConnectionStrings:Database`. Integration tests apply them to their own container and fail if the model has changes missing from the migrations.

## Running the API

`src/MyApp/appsettings.Development.json` points to a local PostgreSQL. After a first build or check:

```text
docker run --detach --name myapp-postgres --env POSTGRES_PASSWORD=postgres --publish 127.0.0.1:5432:5432 postgres:18-alpine
docker exec myapp-postgres sh -c 'for i in $(seq 60); do pg_isready --quiet --host 127.0.0.1 --username postgres && exit 0; sleep 1; done; echo "PostgreSQL not ready after 60 s" >&2; exit 1'
dotnet tool restore
dotnet ef database update --project src/MyApp
dotnet run --project src/MyApp -- --environment Development --urls http://localhost:5080
```

The second command waits, for at most 60 seconds, until PostgreSQL accepts TCP connections. Override the connection string with the `ConnectionStrings__Database` environment variable. Running the API is never a substitute for the check script.

## Verify

```text
bash scripts/check.sh     # macOS, Linux, Git Bash
pwsh scripts/check.ps1    # Windows, PowerShell
```

Both scripts restore the local tools, then run `dotnet hardslice check`: it builds the solution (restoring if needed), then runs the unit tests, the architecture tests and the integration tests. Integration tests need a Docker-compatible container runtime: without one, the check stops before them and says so. It exits with a non-zero code on any failure, the code of the first step that fails.

**You are not done until the check script exits 0.** Do not hand back, report success or commit while it fails. If it cannot pass without breaking a rule, stop and explain the conflict instead.

## Fixing a HardSlice failure

A HardSlice failure starts with its rule: `HS003`, `HS016` and `HS900` to `HS902` from the analyzers at build time, or `HS001`, `HS012` to `HS014`, `HS016` to `HS018` from the architecture tests.

1. Read the diagnostic: it names the offending symbol, the rule, the expected fix and its sheet after `Doc:`.
2. Open the sheet, or print it with `dotnet hardslice explain <rule>`, and apply the first fix that matches: `docs/rules/HS001.md` (domain dependencies), `docs/rules/HS003.md` (endpoints), `docs/rules/HS012.md` (modules), `docs/rules/HS013.md` (validators), `docs/rules/HS014.md` (command handler tests), `docs/rules/HS016.md` (slices), `docs/rules/HS017.md` (Shared), `docs/rules/HS018.md` (test folders), `docs/rules/HS900.md` (contracts of `hardslice.json`), `docs/rules/HS901.md` (one role per type), `docs/rules/HS902.md` (mapped entities).
3. Run the check script again.

`HS900`, or a message starting with `hardslice.json:`, means the configuration or one of its contracts is missing or invalid: restore them, never adapt them to the code.

## Forbidden

Fix the code, never the rule. Never make a HardSlice diagnostic or an architecture test pass by:

- suppressing it: `#pragma warning disable`, `[SuppressMessage]`, `<NoWarn>`, `<WarningsNotAsErrors>`;
- lowering its severity: `dotnet_diagnostic.HS016.severity` in `.editorconfig` or `.globalconfig`, or turning off `TreatWarningsAsErrors`;
- editing `.editorconfig` or `hardslice.json`, for example to allow a new folder in `Shared`;
- turning analyzers off: `<RunAnalyzers>false</RunAnalyzers>`, `<RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild>` or a `HardSliceAnalysisMode` other than `All`;
- removing, editing or downgrading `HardSlice.Analyzers` or its reference in `Directory.Packages.props`, or the `hardslice.tool` entry of `.config/dotnet-tools.json`;
- deleting, skipping or editing tests in `tests/MyApp.ArchitectureTests/`, or editing `scripts/check.sh` and `scripts/check.ps1`;
- changing `HardSliceGetTestInputs` in `Directory.Build.targets` to hide tests behind different compilation symbols or source paths;
- an empty validator, an empty test or a placeholder file written only to satisfy HS013, HS014 or HS018;
- any other workaround listed in the rule sheets of `docs/rules/`, such as reflection, a public handler or moving a slice's type into the module namespace.

An exception requires an explicit decision from a maintainer.
