# HardSlice

Vertical slices, kept clean by the build.

HardSlice is a .NET kit that makes AI coding agents (and humans) respect a vertical slice architecture with a rich domain. The rules are not advice in a prompt: a violation fails `dotnet build` or `dotnet test`, with a one-line diagnostic that names the offending symbol, the expected fix and a rule sheet. An agent reads it and repairs the architecture instead of working around it.

HardSlice adds nothing to the runtime of your application: its analyzer runs at build time only, and its architecture tests live in your test projects.

## Status

**0.2.0-preview.1, local preview.** The packages are not published on NuGet.org yet: you build them from this repository, at the version of its `Directory.Build.props`, the only one the three packages share. This preview ships:

- the `hardslice-api` solution template: an ASP.NET Core API in vertical slices around a rich domain, with a `Bookings` sample module, or empty with `--no-sample`;
- the `hardslice-module` item template, which adds a module to such a solution, with the folders of its domain and of its tests;
- the `hardslice-aggregate` item template, which adds an aggregate to a module of such a solution, with its identifier, its mapping and its unit tests;
- the `hardslice-command` item template, which adds a command slice to a module of such a solution, its tests included;
- the `hardslice-query` item template, which adds a query slice that reads an aggregate of the module, its integration tests included;
- the `hardslice-event` item template, which adds a domain event to a module of such a solution and, on request, the reaction slice that handles it, with its integration tests;
- the `HardSlice.Analyzers` package, which reports HS003 to HS011, HS015 and HS016 at build time, suggests HS019 in the `All` analysis mode, and reports HS900 to HS902 when the contracts that classify domain and messaging types are unusable, ambiguous or missing on a mapped entity;
- architecture tests for HS001, HS012 to HS014 and HS016 to HS018 in every generated solution;
- the `HardSlice.Tool` local tool, pinned by every generated solution to the version of `HardSlice.Analyzers`: `dotnet hardslice check` runs the whole verification, `dotnet hardslice explain <rule>` prints the sheet of a rule;
- `AGENTS.md`, `scripts/check.sh` and `scripts/check.ps1` in every generated solution.

`scripts/test-template.sh` runs the commands of [Getting started](#getting-started) in CI, from a fresh runner, on every push to `main` and every pull request: in temporary folders, on free ports, with and without the sample.

## Prerequisites

- .NET SDK 10.0.100 or a later 10.0 feature band (pinned by `global.json`).
- A Docker-compatible container runtime (Docker Desktop, Docker Engine, Podman, Colima...): the integration tests and the local database run in containers.
- Bash for `scripts/check.sh` (Git Bash on Windows), or PowerShell 7 for `scripts/check.ps1`.
- `curl` to try the API.

## Getting started

### 1. Build and install the local packages

From a clone of this repository:

```text
dotnet pack src/HardSlice.Analyzers/HardSlice.Analyzers.csproj --configuration Release --output artifacts/packages
dotnet pack src/HardSlice.Tool/HardSlice.Tool.csproj --configuration Release --output artifacts/packages
dotnet pack src/HardSlice.Templates/HardSlice.Templates.csproj --configuration Release --output artifacts/packages
dotnet new install artifacts/packages/HardSlice.Templates.0.2.0-preview.1.nupkg
```

`artifacts/packages` then holds `HardSlice.Analyzers`, `HardSlice.Tool` and `HardSlice.Templates` in `0.2.0-preview.1`. The template pins the first two at that version: `HardSlice.Analyzers` in `Directory.Packages.props`, `HardSlice.Tool` in `.config/dotnet-tools.json`. The generated solutions restore them from that folder. Create a folder for them, outside this repository, with this `nuget.config`, replacing the `value` of `hardslice-local` with the absolute path of `artifacts/packages`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="hardslice-local" value="/path/to/hardslice/artifacts/packages" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="hardslice-local">
      <package pattern="HardSlice.*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
```

The mapping makes NuGet take `HardSlice.*` packages from your local folder only, never from nuget.org.

### 2. Generate an application

In that folder:

```text
dotnet new hardslice-api --name Rooms
```

`Rooms` holds the `Bookings` sample: a `Booking` aggregate whose bookings of a room never overlap, guaranteed by PostgreSQL, and three slices, `ConfirmBooking`, `CancelBooking` and `GetBooking`, with their unit, integration and architecture tests. Its `AGENTS.md` maps the solution for an agent.

Booking dates accept timezone offsets and are normalized to UTC. Fractions smaller than a microsecond are truncated before validating the period and checking overlaps, matching PostgreSQL precision. A period whose end is no longer after its start returns 400 and is not stored.

To start from the same foundation without the sample:

```text
dotnet new hardslice-api --name Rooms --no-sample
```

### 3. Verify it

```text
cd Rooms
bash scripts/check.sh     # macOS, Linux, Git Bash
pwsh scripts/check.ps1    # Windows, PowerShell
```

Both scripts restore the local tools of `.config/dotnet-tools.json`, then run `dotnet hardslice check`. It builds the solution, then runs the unit tests, the architecture tests and the integration tests, which start PostgreSQL with Testcontainers. Without a reachable container runtime, it stops before the integration tests and says so. It exits with the code of the first step that fails: this is the command an agent must bring back to green before it is done. The diagnostics are those of `dotnet build` and `dotnet test`, as they print them.

The tool has the version of the analyzers: `check` fails before building if `Directory.Packages.props` references another version of `HardSlice.Analyzers`. It has no option to skip a step.

To read the sheet of a rule without leaving the terminal:

```text
dotnet hardslice explain HS016
```

An unknown rule exits with code 2 and lists the rules that have a sheet.

### 4. Run the API

Start PostgreSQL with the credentials of `src/Rooms/appsettings.Development.json`, reachable from this machine only, wait until it accepts connections, apply the migrations, then start the API in Development:

```text
docker run --detach --name rooms-postgres --env POSTGRES_PASSWORD=postgres --publish 127.0.0.1:5432:5432 postgres:18-alpine
docker exec rooms-postgres sh -c 'for i in $(seq 60); do pg_isready --quiet --host 127.0.0.1 --username postgres && exit 0; sleep 1; done; echo "PostgreSQL not ready after 60 s" >&2; exit 1'
dotnet tool restore
dotnet ef database update --project src/Rooms
dotnet run --project src/Rooms -- --environment Development --urls http://localhost:5080
```

The application never migrates its database at startup: `dotnet ef database update`, with the `dotnet-ef` version pinned in `.config/dotnet-tools.json`, creates the database and applies the migrations. The wait runs `pg_isready` over TCP inside the container for at most 60 seconds: the image first initializes the database with a server that accepts no TCP connection, then restarts it. If port 5432 is taken, publish another one and set the connection string for both `dotnet ef` and `dotnet run`, for example `ConnectionStrings__Database="Host=localhost;Port=5433;Database=rooms;Username=postgres;Password=postgres"`.

When upgrading an existing Bookings database, `AddBookingPositivePeriodConstraint` rejects legacy rows with `"End" <= "Start"` through `CK_Bookings_Period_Positive`. Inspect those rows with `SELECT "Id", "Start", "End" FROM "Bookings" WHERE "End" <= "Start";`, restore their intended periods explicitly, then retry the migration. It never deletes or changes reservations automatically.

From another terminal, confirm a booking, then read it at the address of the `Location` header:

```text
curl -i -X POST http://localhost:5080/bookings -H "Content-Type: application/json" -d '{"roomId":"3f2b8c1e-0d4a-4b6e-9a7f-1c2d3e4f5a6b","start":"2026-12-01T09:00:00Z","end":"2026-12-01T10:00:00Z"}'
curl -i http://localhost:5080/bookings/<id>
```

The first request answers `201 Created`, the second `200 OK` with the booking. The same booking posted again answers `409 Conflict`: the room is already booked. `POST /bookings/<id>/cancel` cancels it.

With `--no-sample`, the same commands create an empty database and start an API without endpoints yet.

When you are done, stop the API with Ctrl+C and remove the database with `docker rm --force rooms-postgres`.

### 5. Change it

Ask your agent to read `AGENTS.md`: it says where a use case goes, how to add a slice, a module or a migration, and that the work is not done until the check script exits 0. When a rule fails, its diagnostic points to its sheet in `docs/rules/`.

A module is generated from the root of the solution, once it is restored (after a first build or check):

```text
dotnet new hardslice-module --project src/Rooms/Rooms.csproj --name Payments
```

It creates `src/Rooms/Features/Payments/PaymentsModule.cs`, the public contract of the module: `AddPaymentsModule`, which registers its slices, and `MapPaymentsModule`, which maps them on the group `/payments`, the name of the module in kebab-case (`--route` sets another one). It also creates the folder of its domain, `src/Rooms.Domain/Payments/`, and its test folders, `tests/Rooms.Tests/Domain/Payments/`, `tests/Rooms.Tests/Features/Payments/` and `tests/Rooms.IntegrationTests/Features/Payments/`, each holding a `.gitkeep`. Then it prints the using and the two lines to add to `Program.cs`. The empty module builds and passes the check: no rule asks a module for a slice or a test. Without `--project`, before the first restore, with a name that is not PascalCase or ends with `Module`, or with a route that is not made of lowercase words, it exits with an error and creates nothing. It refuses to overwrite an existing module without `--force`.

An aggregate of a module is generated the same way:

```text
dotnet new hardslice-aggregate --project src/Rooms/Rooms.csproj --module Payments --name Invoice
```

It creates the aggregate root `Invoice` and its identifier `InvoiceId`, a `readonly record struct` that wraps a `Guid Value`, in `src/Rooms.Domain/Payments/`, its EF Core mapping `InvoiceConfiguration` in `src/Rooms/Features/Payments/`, and its unit tests in `tests/Rooms.Tests/Domain/Payments/`. The root is `sealed` and implements `IAggregateRoot`: a private constructor for persistence, a static factory method `Create` to rename after the business fact and give the rules of the aggregate, no public setter, and a private list of events that `DequeueDomainEvents()` returns and empties. The mapping uses the table `Invoices`, the name followed by `s` (`--table` sets another one), converts the identifier and ignores the events. Then it prints the migration to add: there is nothing to register, `AppDbContext` applies the mapping by itself.

The aggregate builds without any HardSlice diagnostic. Its unit test of the state ends with `Assert.Fail("TODO(hardslice): ...")`, and the integration tests fail until the migration is added: `MigrationsTests` finds changes missing from the migrations, and once the solution has a migration, EF Core refuses to migrate the database of the others. Without `--project`, before the first restore, with a name or a module that is not PascalCase, for a module without `Features/<Module>/<Module>Module.cs`, or with a table that is not made of letters, digits and underscores, it exits with an error and creates nothing. It refuses to overwrite an existing aggregate without `--force`.

A command slice is generated the same way:

```text
dotnet new hardslice-command --project src/Rooms/Rooms.csproj --module Bookings --name ExtendBooking
```

It creates `src/Rooms/Features/Bookings/ExtendBooking/ExtendBooking.cs`, with the command `ExtendBookingCommand`, its validator, its handler and its endpoint `POST /bookings/extend-booking`, the validator tests in `tests/Rooms.Tests/Features/Bookings/ExtendBooking/` and the integration tests in `tests/Rooms.IntegrationTests/Features/Bookings/ExtendBooking/`. Then it prints the lines to add to `BookingsModule.cs`: the usings, the registrations of the handler and the validator, and the call of `Map`. It never edits an existing file. `--response` sets what the command returns: `id`, a `Guid`, by default; `none`, a `bool` whose `false` is answered with 404; or the name of a DTO of values, which the slice declares.

`--project` names the web project: the template reads the name of the application, its `RootNamespace`, and its modules from it. Without it, before the first restore, with a name that is not PascalCase or ends with `Command`, or for a module without `Features/<Module>/<Module>Module.cs`, it exits with an error that says what to do and creates nothing. It refuses to overwrite an existing slice without `--force`.

The generated code builds without any HardSlice diagnostic, and the validator tests pass. The handler throws `NotImplementedException` and the integration test ends with `Assert.Fail("TODO(hardslice): ...")`: the check fails until the use case and its assertion are written. A slice its module does not register fails the check too: a test of the host resolves every request handler and validator of the application from the container, and names the ones that are missing.

A query slice is generated the same way, from the aggregate it reads:

```text
dotnet new hardslice-query --project src/Rooms/Rooms.csproj --module Bookings --name GetBookingSummary --aggregate Booking
```

It creates `src/Rooms/Features/Bookings/GetBookingSummary/GetBookingSummary.cs`, with the query `GetBookingSummaryQuery`, its DTO `GetBookingSummaryDto`, its handler and its endpoint `GET /bookings/get-booking-summary/{id}`, and the integration tests in `tests/Rooms.IntegrationTests/Features/Bookings/GetBookingSummary/`, then prints the lines to add to `BookingsModule.cs`. The handler reads `Set<Booking>()` with `AsNoTracking()` and projects it into the DTO with `Select`, so HS006 has nothing to report. By default it reads one DTO by its id, an unknown id being answered with 404; `--result many` reads the list instead. `--aggregate` is required: it names a type of the namespace `Rooms.Domain.Bookings` whose `Id` is an `<Aggregate>Id` that wraps a `Guid Value`, as `Booking` and `BookingId` do. An aggregate that does not exist fails the build with a compiler error that names it.

The handler works as generated: with one DTO by its id, the test of an unknown id passes. The test that reads or lists a stored aggregate fails with `TODO(hardslice):` until it creates the aggregate through its factory method and asserts the values the DTO reads.

A domain event is generated the same way, with the reaction that handles it if you name one:

```text
dotnet new hardslice-event --project src/Rooms/Rooms.csproj --module Bookings --name BookingExtended --reaction Payments/ChargeOnBookingExtended
```

It creates the event `BookingExtended`, a `sealed record` that implements `IDomainEvent` with a `Guid Id` to replace by the values of the fact, in `src/Rooms.Domain/Bookings/Events/`. With `--reaction <Module>/<Slice>`, it also creates the reaction slice `src/Rooms/Features/Payments/ChargeOnBookingExtended/`, whose only type is the `internal` handler `ChargeOnBookingExtendedHandler`, an `IDomainEventHandler<BookingExtended>`, and its integration tests in `tests/Rooms.IntegrationTests/Features/Payments/ChargeOnBookingExtended/`, then prints the usings and the registration to add to `PaymentsModule.cs`. The reaction may live in another module than the event: it depends on the domain only. Without `--reaction`, it prints how to raise the event from the aggregate instead. It never edits the aggregate: the method where the fact happens adds the event to its list.

The event and its reaction build without any HardSlice diagnostic. The handler throws `NotImplementedException`, and its test, which calls it with an event as the pipeline does, then saves, ends with `Assert.Fail("TODO(hardslice): ...")`: the check fails until the reaction and its assertion are written. A reaction its module does not register fails the check twice: its test, and the test of the host, which also resolves every domain event handler. A name that HS019 does not read as a past fact, such as `ExtendBooking`, is created with a warning. Without `--project`, before the first restore, with a name or a module that is not PascalCase, for a module without `Features/<Module>/<Module>Module.cs`, the module of the event or of the reaction, or with a reaction that is not `<Module>/<Slice>` in PascalCase, ends with `Handler` or is named as the event, it exits with an error and creates nothing. It refuses to overwrite an existing event without `--force`.

## Rules

| Rule | Checks | Reported by |
|---|---|---|
| [HS001](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS001.md) | The domain project depends on the base class library only: no EF Core, ASP.NET Core or dependency injection. | Architecture tests |
| [HS003](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS003.md) | An endpoint never reaches the database: it sends a command or a query through `IMediator`. | Analyzer at build time |
| [HS004](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS004.md) | A slice `Features/<Module>/<Slice>/` holds one request named after it (`<Slice>Command` or `<Slice>Query`), its handler `<Slice>Handler`, its static endpoint `<Slice>Endpoint` and, if any, its validator `<Slice>Validator`, or, for a reaction slice, one domain event handler `<Slice>Handler` without request nor endpoint; no request, handler or validator lives outside a slice, and the folder and the namespace of a slice hold the same types. | Analyzer at build time |
| [HS005](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS005.md) | A command handler returns an identifier, a DTO of values or nothing: never an entity, directly, in a collection, an array, a tuple, a generic envelope such as `Result<T>` or a DTO. | Analyzer at build time |
| [HS006](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS006.md) | A query handler never writes through EF Core, never turns tracking back on nor reconfigures it, never calls `Find`, and starts each query that materializes an entity with `AsNoTracking()` in the handler itself. | Analyzer at build time |
| [HS007](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS007.md) | An entity, root or child, has no `set` or `init` accessor reachable from outside its assembly, not even through a public interface, and no public field that is not `readonly`: its state changes through its methods. | Analyzer at build time |
| [HS008](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS008.md) | An aggregate references another aggregate by its identifier only: neither its root nor its child entities hold another root, directly, in a collection, an array or a generic type, not even another instance of the same type. A child entity may reference the roots that reach it and any child entity. | Analyzer at build time |
| [HS009](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS009.md) | A domain event, a type that implements `IDomainEvent` in any project, is a `record` or a `record struct`. | Analyzer at build time |
| [HS010](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS010.md) | The domain and the slices never read the system clock (`DateTime.Now`, `DateTime.UtcNow`, `DateTime.Today`, `DateTimeOffset.Now`, `DateTimeOffset.UtcNow`): handlers inject `TimeProvider` and pass the instant to the domain. | Analyzer at build time |
| [HS011](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS011.md) | Code never blocks on asynchronous work (`.Result`, `.Wait()`, `GetAwaiter().GetResult()`) and never declares `async void`, lambdas converted to `void` delegates included. | Analyzer at build time |
| [HS012](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS012.md) | A module uses another module only through its public contract. | Architecture tests |
| [HS013](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS013.md) | Every command with parameters has a validator. | Architecture tests |
| [HS014](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS014.md) | Every command handler and every domain event handler has a test at the path of its slice. | Architecture tests |
| [HS015](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS015.md) | The endpoint of a slice is mapped in its slice, on the group of its module, by a method its module calls, and returns `TypedResults`. | Analyzer at build time |
| [HS016](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS016.md) | A slice never uses a type of another slice, and its handlers, validator and endpoint stay `internal`. | Analyzer at build time, and architecture tests |
| [HS017](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS017.md) | `Shared` stays cross-cutting: it never depends on `Features`, and holds only the folders `hardslice.json` allows. | Architecture tests |
| [HS018](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS018.md) | The tests of a slice are at the same path under `tests/`, and every slice has some. | Architecture tests |
| [HS019](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS019.md) | A domain event is named as a past fact: the last word of its name ends with `ed` or is an irregular past participle of the sheet's list (`BookingPaid`). A naming convention, never blocking. | Analyzer suggestion in the `All` mode |
| [HS900](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS900.md) | `hardslice.json` is usable: it declares the contracts `IEntity`, `IAggregateRoot`, `IDomainEvent`, `ICommand<T>`, `IQuery<T>` and `IRequestHandler<,>`, and each one exists in its declaring assembly. | Analyzer at build time |
| [HS901](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS901.md) | A type has one role: never both an entity and a domain event, a command and a query, or an entity and a request. | Analyzer at build time |
| [HS902](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS902.md) | Every domain type EF Core maps as an entity implements `IEntity` or `IAggregateRoot`. | Analyzer at build time |

Each generated solution carries these sheets in `docs/rules/`, at the path its diagnostics announce, and `dotnet hardslice explain` prints them. A sheet says what the rule forbids, how to fix a violation, and which workarounds are forbidden.

### Analysis modes

The MSBuild property `HardSliceAnalysisMode` chooses which analyzer rules are on: `Minimum`, `Recommended` (when it is not set) or `All`. A generated solution sets `All` in its `Directory.Build.props`. A mode turns a rule on or off, never changes its severity, and a `dotnet_diagnostic.<rule>.severity` line of `.editorconfig` takes precedence over it. Any other value fails the build with the list of modes.

| Rule | `Minimum` | `Recommended` | `All` |
|---|---|---|---|
| HS003 | error | error | error |
| HS004 | – | error | error |
| HS005 | – | error | error |
| HS006 | – | error | error |
| HS007 | error | error | error |
| HS008 | – | error | error |
| HS009 | – | error | error |
| HS010 | error | error | error |
| HS011 | error | error | error |
| HS015 | – | error | error |
| HS016 | error | error | error |
| HS019 | – | – | suggestion |
| HS900 to HS902 | error | error | error |

A dash means the mode turns the rule off: `Minimum` keeps the rules that assume neither the shape of a slice, the messaging of the template, its aggregates referenced by identifier nor its domain events declared as records, plus HS016. HS019, a heuristic, is a suggestion of the `All` mode only: it never blocks the build, even with `TreatWarningsAsErrors`. HS900 to HS902 are reported only when an active rule depends on the classification of types. The architecture tests do not depend on the mode.

## Known limitations

- The packages are built locally with the fixed version 0.2.0-preview.1: a rebuild does not change it. After pulling a new version of this repository, repack the three packages, reinstall the template with `dotnet new install artifacts/packages/HardSlice.Templates.0.2.0-preview.1.nupkg --force`, and clear the packages NuGet keeps in its global packages folder with `dotnet nuget locals global-packages --clear`: otherwise solutions keep restoring the old builds of `HardSlice.Analyzers` and `HardSlice.Tool` 0.2.0-preview.1. A solution generated before keeps the template it was generated from.
- `HardSlice.Analyzers` is supported only in a solution generated by `hardslice-api`, whose `docs/rules/` holds the sheets its diagnostics point to.
- Only HS003 to HS011, HS015, HS016 and HS900 to HS902 are reported at build time and in the IDE, and HS019 as a suggestion, which the build output does not show: the IDE and the compiler log (`ErrorLog`) do. The other rules are architecture tests: they fail `dotnet test` and the check scripts, not `dotnet build`. HS900, and HS015 for an endpoint its module never maps, are reported at the end of the analysis of a project: the IDE shows them with full solution analysis only.
- HS016 is analyzed within one assembly: slices and modules are folders of the web project.
- HS003 reads the body of the endpoint only: the methods it calls are not followed, and a delegate stored in a variable or declared in another assembly is not analyzed.
- HS004 checks the slices of the project that declares them, by the namespaces `<Assembly>.Features.<Module>.<Slice>` and the folders below the project directory: the name and the number of the files of a slice are free, and generated code is not checked. A reaction slice is recognized by its domain event handler: whether the handler stays inside the transaction, without external effect, is a review matter.
- HS005 follows the declared types of the response: an entity typed `object` or `dynamic`, held by a non-public member or reached through the members of a library type is not detected, and a generic handler whose response is an unconstrained type parameter is not analyzed.
- HS006 analyzes the code of the query handler only, without control flow: a query obtained from a field, a parameter or a helper, a local reassigned other than as `query = query.<operators>`, a compiled query and a write made in a helper or through another library are not analyzed. An `AsNoTracking()` added by a conditional reassignment guarantees nothing, and the global `UseQueryTrackingBehavior` is not read.
- HS007 checks each entity in the project that declares it, by the accessibility of its accessors and fields: a write by reflection, through a public mutable collection the entity exposes, or by a public method that only assigns its parameter is not detected.
- HS008 checks each entity in the project that declares it, by the declared types of its properties and fields: a root held through `object`, an unclassified interface, a type parameter or the members of another type is not detected. The owners of a child entity are the roots of the same project that reach its type: a child that no root reaches, or a reference from a child to a root of another project, is not reported, and the rule reads types, not instances.
- HS009 checks that each domain event is a record, in the project that declares it, not that it is immutable: a record with a public setter is accepted.
- HS019 reads the form of the name only: `RoomSeed` passes although it is no past fact, an irregular past participle missing from the list of the sheet is reported, and no exception can be configured. Abstract events and event interfaces are not checked.
- HS010 checks the domain project, recognized by its entity contract in `hardslice.json`, and the slices only: the module, `Shared` and `Program.cs` are not checked, and the methods they declare that a slice calls are not followed. `TimeProvider.System` is not reported.
- HS011 recognizes the blocking members of tasks and their awaiters only: `Thread.Sleep`, `SemaphoreSlim.Wait`, synchronous APIs and the methods the code calls in other assemblies are not followed, and a task never awaited is not reported.
- HS015 checks the endpoints mapped under `Features` only, and accepts the result types of `Microsoft.AspNetCore.Http.HttpResults` only: a custom `IResult` is reported. The module must call the method of the slice that maps the endpoint, not a helper of it.
- PostgreSQL is the only database. SQL Server and Aspire orchestration are planned for v1.0.
- Domain events are published to their handlers inside the transaction of the command, before saving: no outbox, no publication after commit, no bus and no asynchronous processing. A handler that sends a request through `IMediator` fails at runtime; an external effect in a handler is not detected.
- `AGENTS.md` is the only agent guidance. `CLAUDE.md`, `GEMINI.md`, `.github/copilot-instructions.md`, skills and hooks are planned for v0.3, the adoption of existing solutions (`dotnet hardslice init`, `map`, `baseline`, `report` and `verify`) for v0.5.
- `dotnet new hardslice-module`, `hardslice-aggregate`, `hardslice-command`, `hardslice-query` and `hardslice-event` read the application and its modules through `--project` only, once the web project is restored: `dotnet new` evaluates no project from the root of a solution, and cannot read a file before it creates the others. A refusal prints `The template ... was created successfully.` before `Post action failed.` and its error, and exits with code 105: nothing is created. They only print the registrations of the module, of the slice or of the reaction, which you or the agent add to `Program.cs` or to the module, `hardslice-aggregate` the migration to add, and `hardslice-event` how to raise the event. `hardslice-module` puts a `.gitkeep` in the folders it creates without file, as `dotnet new` creates no empty folder; with `--force`, it overwrites the contract of an existing module, its registrations included. The tests that `hardslice-command` and `hardslice-query` generate call the route of the group in kebab-case: after `--route`, change theirs. `hardslice-query` cannot check the aggregate: the compiler does. Its list result is `--result many`, not `list`, which `dotnet new` would read as its own `list` command. `hardslice-event` always creates its event: a reaction is generated with it, or with `--force` on an event left unchanged, and a reaction to an event already completed is written by hand, in the shape of the generated one.
- The generated application has a Development configuration only: no production settings, authentication or deployment.
- CI runs on Linux: `scripts/check.ps1` is verified with PowerShell 7 on Linux, not on Windows.

## Working on HardSlice

```text
dotnet restore
dotnet build --no-restore
dotnet test --no-build
scripts/test-template.sh
scripts/demo-hs016-agent-fix.sh prepare "$(mktemp -d)"
scripts/measure-analyzers.sh
```

- `dotnet test` runs the analyzer tests and the tests of the template content, integration tests included: it needs a container runtime.
- `scripts/test-template.sh` packs the three packages, generates an application with and without the sample, runs the getting started path above on them, verifies them with both check scripts, then seeds one violation per rule and expects both scripts to fail with its diagnostic and its sheet. In both, it also generates a module with `hardslice-module`, registers it in `Program.cs` with the lines it prints and checks that the empty module passes the check, and, with the sample, that HS012 sees it reach into a slice of `Bookings`. It generates an aggregate with `hardslice-aggregate` through each of its refusals, checks that it builds without HardSlice diagnostic, that the check fails on its `TODO(hardslice):` test, then on the missing migration, and passes once the migration is added. It generates command slices with `hardslice-command` and query slices with `hardslice-query`, through each of their refusals, registers them with the lines they print, and checks that only the `TODO(hardslice):` tests fail until a reference completion makes the check pass, and that a query on an unknown aggregate fails the build with a compiler error alone. It generates events with `hardslice-event` the same way, with a reaction in the module of the event and one in a second module, and checks that the test of the host names a reaction left unregistered. It needs PowerShell 7.
- `scripts/demo-hs016-agent-fix.sh` prepares the [HS016 agent fix demonstration](demos/hs016-agent-fix/README.md).
- `scripts/measure-analyzers.sh` measures the build time of a large generated application with and without the HardSlice analyzers, checks the thresholds and records the measurement in [the performance report](docs/performance/analyzers.md), in French.

CI runs three jobs on every push to `main` and every pull request, each on a fresh runner from a fresh checkout, without cache: build and tests, template, and demonstration. A fourth one, performance, measures the analyzers on every push to `main` and on the pull requests that change the analyzers, the template or the measurement.

| Path | Contains |
|---|---|
| `src/HardSlice.Analyzers/` | The Roslyn analyzer package. |
| `src/HardSlice.Templates/content/hardslice-api/` | The solution template, `MyApp` being replaced by the name you choose. |
| `src/HardSlice.Templates/content/hardslice-module/` | The item template of a module, `MyApp` and `ModuleName` being replaced by the application and the module. |
| `src/HardSlice.Templates/content/hardslice-aggregate/` | The item template of an aggregate, `MyApp`, `ModuleName`, `AggregateName` and `AggregateTable` being replaced by the application, the module, the aggregate and its table. |
| `src/HardSlice.Templates/content/hardslice-command/` | The item template of a command slice, `MyApp`, `ModuleName` and `CommandName` being replaced by the application, the module and the slice. |
| `src/HardSlice.Templates/content/hardslice-query/` | The item template of a query slice, `MyApp`, `ModuleName`, `QueryName` and `AggregateName` being replaced by the application, the module, the slice and the aggregate it reads. |
| `src/HardSlice.Templates/content/hardslice-event/` | The item template of a domain event and its reaction, `MyApp`, `ModuleName`, `EventName`, `ReactionModule` and `ReactionSlice` being replaced by the application, the module and the name of the event, and the module and the slice of the reaction. |
| `src/HardSlice.Tool/` | The `dotnet hardslice` local tool, which embeds the rule sheets of the template. |
| `tests/HardSlice.Analyzers.Tests/` | Analyzer tests, and tests that compile the template content as is. |
| `tests/HardSlice.Tool.Tests/` | Tests of `check` and `explain`, and of the versions and sheets the tool shares with the template. |
| `scripts/` | Template, demonstration and performance scenarios. |
| `demos/hs016-agent-fix/` | An agent fixing a deliberate HS016 violation. |
| `docs/` | Specification, plan, backlog and performance report, in French. |

## License

[MIT](LICENSE).
