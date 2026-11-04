# Getting started

From nothing to a running API in five steps: install the templates, generate a solution, verify it, run it, then hand it to your agent. `scripts/test-template.sh` runs these commands in CI on every push, on the packages built from the commit, in temporary folders and on free ports, with and without the sample. Each release runs steps 1 to 3 again from NuGet.org on a fresh runner.

## Prerequisites

- .NET SDK 10.0.100 or a later 10.0 feature band (pinned by `global.json`).
- A Docker-compatible container runtime (Docker Desktop, Docker Engine, Podman, Colima...): the integration tests and the local database run in containers.
- Bash for `scripts/check.sh` (Git Bash on Windows), or PowerShell 7 for `scripts/check.ps1`.
- `curl` to try the API.

## 1. Install the templates

```text
dotnet new install HardSlice.Templates@0.3.0-preview.1
```

This installs the `hardslice-api` solution template and the five [item templates](item-templates.md) from NuGet.org. They pin `HardSlice.Analyzers` and `HardSlice.Tool` at the same version, in `Directory.HardSlice.props` and `.config/dotnet-tools.json`: the generated solutions restore both from NuGet.org, with their other packages.

To try a build of this repository instead, see [Try a local build of the packages](../CONTRIBUTING.md#try-a-local-build-of-the-packages).

## 2. Generate an application

In the folder of your choice:

```text
dotnet new hardslice-api --name Rooms
```

The name becomes one safe .NET namespace that names every folder, project, assembly and namespace, `hardslice.json` included: `Acme.Rooms` stays `Acme.Rooms`, `my-app` becomes `my_app` and `1Rooms` becomes `_1Rooms`. Only the output folder keeps the name as typed.

`Rooms` holds the `Bookings` sample: a `Booking` aggregate whose bookings of a room never overlap, guaranteed by PostgreSQL, and three slices, `ConfirmBooking`, `CancelBooking` and `GetBooking`, with their unit, integration and architecture tests. Its `AGENTS.md` maps the solution for an agent.

Booking dates accept timezone offsets and are normalized to UTC. Fractions smaller than a microsecond are truncated before validating the period and checking overlaps, matching PostgreSQL precision. A period whose end is no longer after its start returns 400 and is not stored.

To start from the same foundation without the sample:

```text
dotnet new hardslice-api --name Rooms --no-sample
```

## 3. Verify it

```text
cd Rooms
bash scripts/check.sh     # macOS, Linux, Git Bash
pwsh scripts/check.ps1    # Windows, PowerShell
```

Both scripts restore the local tools of `.config/dotnet-tools.json`, then run `dotnet hardslice check`. It builds the solution, then runs the unit tests, the architecture tests and the integration tests, which start PostgreSQL with Testcontainers. Without a reachable container runtime, it stops before the integration tests and says so. Last, it fails while a `TODO(hardslice):` marker that an item template left is still in `src/` or `tests/`, and lists each file and line: replacing a failing generated test with a trivial assertion does not pass it.

It exits with `0` when the check passes, `1` when a step fails, its output left as is, and `2` on a wrong argument only: this is the command an agent must bring back to green before it is done. The diagnostics are those of `dotnet build` and `dotnet test`, as they print them.

The tool has the version of the analyzers: `check` fails before building if `Directory.HardSlice.props` references another version of `HardSlice.Analyzers` or if `Directory.Packages.props` no longer imports it, and after building if a project did not receive the package. `Directory.Packages.props` belongs to the application: add its packages there. It has no option to skip a step. `bash scripts/check.sh --build` (or `check.ps1 --build`, `dotnet hardslice check --build`) is the verification for a hook after an edit: the incremental build of the solution with `--no-restore`, after a restore only when a project has never been restored or a file its restore reads changed since its last restore, printing the errors alone as the compiler wrote them, then the `TODO(hardslice):` markers. It runs no test, says so, and never decides that the work is done: only the check without argument does. Any other argument exits with code 2. Both fit the budgets of the hooks, 10 seconds after an edit and 60 seconds before an agent hands back, as [measured](performance/analyzers.md).

To read the sheet of a rule without leaving the terminal:

```text
dotnet hardslice explain HS016
```

An unknown rule exits with code 2 and lists the rules that have a sheet.

The solution also runs the check before each commit and in CI, both with `scripts/check.sh` without argument: see [the pre-commit hook and the generated CI](agent-loop.md#before-a-commit-and-in-ci).

## 4. Run the API

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

## 5. Change it

Ask your agent to read `AGENTS.md`: it says where a use case goes, how to add a slice, a module or a migration, and that the work is not done until the check script exits 0. When a rule fails, its diagnostic points to its sheet in `docs/rules/` and names the skill that fixes it.

- [Item templates](item-templates.md): the `dotnet new` commands that generate a module, an aggregate, a command, a query or a domain event, which the skills call.
- [Agent loop](agent-loop.md): what each supported agent reads, its hooks, the protected files and the skills.
- [Rules](rules.md): what each rule checks, the analysis modes and the known limits of each rule.
