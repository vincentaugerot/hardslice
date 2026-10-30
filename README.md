# HardSlice

Vertical slices, kept clean by the build.

HardSlice is a .NET kit that makes AI coding agents (and humans) respect a vertical slice architecture with a rich domain. The rules are not advice in a prompt: a violation fails `dotnet build` or `dotnet test`, with a one-line diagnostic that names the offending symbol, the expected fix and a rule sheet. An agent reads it and repairs the architecture instead of working around it.

HardSlice adds nothing to the runtime of your application: its analyzer runs at build time only, and its architecture tests live in your test projects.

## Status

**v0.1, local preview.** The packages are not published on NuGet yet: you build them from this repository. v0.1 ships:

- the `hardslice-api` solution template: an ASP.NET Core API in vertical slices around a rich domain, with a `Bookings` sample module, or empty with `--no-sample`;
- the `HardSlice.Analyzers` package, which reports HS003, HS010, HS011, HS015 and HS016 at build time, and HS900 to HS902 when the contracts that classify domain and messaging types are unusable, ambiguous or missing on a mapped entity;
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
dotnet new install artifacts/packages/HardSlice.Templates.0.1.0.nupkg
```

The generated solutions restore `HardSlice.Analyzers` and `HardSlice.Tool` from that folder. Create a folder for them, outside this repository, with this `nuget.config`, replacing the `value` of `hardslice-local` with the absolute path of `artifacts/packages`:

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

## Rules

| Rule | Checks | Reported by |
|---|---|---|
| [HS001](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS001.md) | The domain project depends on the base class library only: no EF Core, ASP.NET Core or dependency injection. | Architecture tests |
| [HS003](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS003.md) | An endpoint never reaches the database: it sends a command or a query through `IMediator`. | Analyzer at build time |
| [HS010](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS010.md) | The domain and the slices never read the system clock (`DateTime.Now`, `DateTime.UtcNow`, `DateTime.Today`, `DateTimeOffset.Now`, `DateTimeOffset.UtcNow`): handlers inject `TimeProvider` and pass the instant to the domain. | Analyzer at build time |
| [HS011](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS011.md) | Code never blocks on asynchronous work (`.Result`, `.Wait()`, `GetAwaiter().GetResult()`) and never declares `async void`, lambdas converted to `void` delegates included. | Analyzer at build time |
| [HS012](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS012.md) | A module uses another module only through its public contract. | Architecture tests |
| [HS013](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS013.md) | Every command with parameters has a validator. | Architecture tests |
| [HS014](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS014.md) | Every command handler has a test at the path of its slice. | Architecture tests |
| [HS015](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS015.md) | The endpoint of a slice is mapped in its slice, on the group of its module, by a method its module calls, and returns `TypedResults`. | Analyzer at build time |
| [HS016](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS016.md) | A slice never uses a type of another slice, and its handler, validator and endpoint stay `internal`. | Analyzer at build time, and architecture tests |
| [HS017](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS017.md) | `Shared` stays cross-cutting: it never depends on `Features`, and holds only the folders `hardslice.json` allows. | Architecture tests |
| [HS018](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS018.md) | The tests of a slice are at the same path under `tests/`, and every slice has some. | Architecture tests |
| [HS900](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS900.md) | `hardslice.json` is usable: it declares the contracts `IEntity`, `IAggregateRoot`, `IDomainEvent`, `ICommand<T>`, `IQuery<T>` and `IRequestHandler<,>`, and each one exists in its declaring assembly. | Analyzer at build time |
| [HS901](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS901.md) | A type has one role: never both an entity and a domain event, a command and a query, or an entity and a request. | Analyzer at build time |
| [HS902](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS902.md) | Every domain type EF Core maps as an entity implements `IEntity` or `IAggregateRoot`. | Analyzer at build time |

Each generated solution carries these sheets in `docs/rules/`, at the path its diagnostics announce, and `dotnet hardslice explain` prints them. A sheet says what the rule forbids, how to fix a violation, and which workarounds are forbidden.

### Analysis modes

The MSBuild property `HardSliceAnalysisMode` chooses which analyzer rules are on: `Minimum`, `Recommended` (when it is not set) or `All`. A generated solution sets `All` in its `Directory.Build.props`. A mode turns a rule on or off, never changes its severity, and a `dotnet_diagnostic.<rule>.severity` line of `.editorconfig` takes precedence over it. Any other value fails the build with the list of modes.

| Rule | `Minimum` | `Recommended` | `All` |
|---|---|---|---|
| HS003 | error | error | error |
| HS010 | error | error | error |
| HS011 | error | error | error |
| HS015 | – | error | error |
| HS016 | error | error | error |
| HS900 to HS902 | error | error | error |

A dash means the mode turns the rule off: `Minimum` keeps the rules that assume neither the shape of a slice nor the messaging of the template, plus HS016. HS900 to HS902 are reported only when an active rule depends on the classification of types. The architecture tests do not depend on the mode.

## Known limitations

- The packages are built locally with the fixed version 0.1.0. After pulling a new version of this repository, repack, reinstall the template with `dotnet new install artifacts/packages/HardSlice.Templates.0.1.0.nupkg --force`, and clear the packages NuGet keeps in its global packages folder with `dotnet nuget locals global-packages --clear`: otherwise solutions keep restoring the old builds of `HardSlice.Analyzers` and `HardSlice.Tool` 0.1.0.
- `HardSlice.Analyzers` is supported only in a solution generated by `hardslice-api`, whose `docs/rules/` holds the sheets its diagnostics point to.
- Only HS003, HS010, HS011, HS015, HS016 and HS900 to HS902 are reported at build time and in the IDE. The other rules are architecture tests: they fail `dotnet test` and the check scripts, not `dotnet build`. HS900, and HS015 for an endpoint its module never maps, are reported at the end of the analysis of a project: the IDE shows them with full solution analysis only.
- HS016 is analyzed within one assembly: slices and modules are folders of the web project.
- HS003 reads the body of the endpoint only: the methods it calls are not followed, and a delegate stored in a variable or declared in another assembly is not analyzed.
- HS010 checks the domain project, recognized by its entity contract in `hardslice.json`, and the slices only: the module, `Shared` and `Program.cs` are not checked, and the methods they declare that a slice calls are not followed. `TimeProvider.System` is not reported.
- HS011 recognizes the blocking members of tasks and their awaiters only: `Thread.Sleep`, `SemaphoreSlim.Wait`, synchronous APIs and the methods the code calls in other assemblies are not followed, and a task never awaited is not reported.
- HS015 checks the endpoints mapped under `Features` only, and accepts the result types of `Microsoft.AspNetCore.Http.HttpResults` only: a custom `IResult` is reported. The module must call the method of the slice that maps the endpoint, not a helper of it.
- PostgreSQL is the only database. SQL Server and Aspire orchestration are planned for v1.0.
- Domain events are recorded by the aggregates but not published to handlers yet.
- `AGENTS.md` is the only agent guidance. `CLAUDE.md`, `GEMINI.md`, `.github/copilot-instructions.md`, skills, hooks and item templates are planned for v0.3, the adoption of existing solutions (`dotnet hardslice init`, `map`, `baseline`, `report` and `verify`) for v0.5.
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
- `scripts/test-template.sh` packs the three packages, generates an application with and without the sample, runs the getting started path above on them, verifies them with both check scripts, then seeds one violation per rule and expects both scripts to fail with its diagnostic and its sheet. It needs PowerShell 7.
- `scripts/demo-hs016-agent-fix.sh` prepares the [HS016 agent fix demonstration](demos/hs016-agent-fix/README.md).
- `scripts/measure-analyzers.sh` measures the build time of a large generated application with and without the HardSlice analyzers, checks the thresholds and records the measurement in [the performance report](docs/performance/analyzers.md), in French.

CI runs three jobs on every push to `main` and every pull request, each on a fresh runner from a fresh checkout, without cache: build and tests, template, and demonstration. A fourth one, performance, measures the analyzers on every push to `main` and on the pull requests that change the analyzers, the template or the measurement.

| Path | Contains |
|---|---|
| `src/HardSlice.Analyzers/` | The Roslyn analyzer package. |
| `src/HardSlice.Templates/content/hardslice-api/` | The solution template, `MyApp` being replaced by the name you choose. |
| `src/HardSlice.Tool/` | The `dotnet hardslice` local tool, which embeds the rule sheets of the template. |
| `tests/HardSlice.Analyzers.Tests/` | Analyzer tests, and tests that compile the template content as is. |
| `tests/HardSlice.Tool.Tests/` | Tests of `check` and `explain`, and of the versions and sheets the tool shares with the template. |
| `scripts/` | Template, demonstration and performance scenarios. |
| `demos/hs016-agent-fix/` | An agent fixing a deliberate HS016 violation. |
| `docs/` | Specification, plan, backlog and performance report, in French. |

## License

[MIT](LICENSE).
