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

This installs the `hardslice-api` solution template and the seven [item templates](item-templates.md) from NuGet.org. They pin `HardSlice.Analyzers` and `HardSlice.Tool` at the same version, in `Directory.HardSlice.props` and `.config/dotnet-tools.json`: the generated solutions restore both from NuGet.org, with their other packages.

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

### When check stops on a HardSlice file

Before building, `check` reads the HardSlice files of the solution, as the templates of its own version generate them:

- `.config/dotnet-tools.json`, protected, at the root of the solution: it pins `hardslice.tool`, the version of `dotnet hardslice`.
- `Directory.HardSlice.props`, protected: one `GlobalPackageReference` to `HardSlice.Analyzers`, at the version of the tool.
- `Directory.Packages.props`, which the application owns: the versions of its packages, and the import of `Directory.HardSlice.props`.

Each error that `check` prints on one of these files ends with `Fix:` and `Doc:`, a link to this section. A file that is missing, or that holds another version, usually means that the solution was generated by another template or another version of the tool: the layout of these files changed between previews, and a tool updated alone expects the layout of its own templates. Two fixes, both made by a maintainer, since they touch protected files, which the hooks of the agents refuse to edit and the generated CI refuses in a pull request without the label `hardslice-approved`:

- Bring the solution to the tool with `dotnet hardslice upgrade`, as [Upgrading a generated solution](#upgrading-a-generated-solution) describes.
- Bring the tool back to the solution. Set the `hardslice.tool` entry of `.config/dotnet-tools.json` to the version that generated the solution, the one that `Directory.HardSlice.props` references, then run `dotnet tool restore`.

An agent can make neither fix when the file is protected. When the check stops on `.config/dotnet-tools.json` or `Directory.HardSlice.props`, the hook at the end of its turn hands it the error once in the session, then lets it hand back, so that it tells the user what a maintainer must do instead of trying again. An error on `Directory.Packages.props`, which the agent may fix, keeps the relaunches of any other failure.

After a build, `check` also fails when a project did not receive `HardSlice.Analyzers` at its restore: keep the import in `Directory.Packages.props`, and remove what removes or changes the reference, such as a `Remove`, an `Update`, a `Version` or an `ExcludeAssets` of it, or another `Directory.Packages.props` in a folder of the project.

### Upgrading a generated solution

`dotnet hardslice upgrade` brings a solution to the template of the version of the tool. From a git tree without changes, update the tool, then run it:

```text
dotnet tool update hardslice.tool --version <new version>
dotnet hardslice upgrade
```

- It generates `hardslice-api` again at the version of the tool, under the name of the solution, in a temporary folder and a template hive of its own, from the package sources of the `nuget.config` files of the solution. It leaves out the sample when the solution has neither `src/<App>/Features/Bookings/` nor `src/<App>.Domain/Bookings/`: `--sample` or `--no-sample` says otherwise.
- It rewrites the files that the template owns, the [protected files](agent-loop.md#protected-files) and the rule sheets of `docs/rules/`, and lists them. A change that a maintainer made to one of them is gone: `git diff` shows it, to put back while it still applies.
- It adds the files of the template that the solution lacks. A file of the solution that has the name of an added file in another folder is listed with it: the template may have moved it, as it moved the HTTP answers from `Shared/Behaviors` to `Shared/Http`. Its diff against the new file follows, and a line says when they differ only by their namespace declaration, which leaves nothing to merge. Merge what the application changed in it into the new file, then delete it.
- It merges in three ways the other files of the template, which the application owns and changes, such as `AGENTS.md`, `Program.cs`, `AppFactory.cs` and the projects. Its base is `.hardslice/generation/`, which `dotnet new hardslice-api` and each upgrade write: the files of the application as the template generated them, without the protected files, the rule sheets, `GEMINI.md` and `.github/copilot-instructions.md`, so that no agent reads an instruction twice. The folder is protected, and no project compiles it. For each file of the application:
  - One that the application did not change is updated, and listed.
  - One that the template did not change is left, and not listed.
  - One that both changed is merged with `git merge-file`, and listed: review it with `git diff`. Where both changed the same lines, the file holds both changes between the conflict markers of git, `<<<<<<< solution`, `||||||| generation <version>` before the lines as the template generated them, `=======` and `>>>>>>> template <version>`, and is listed as a conflict to resolve. The parts that the application wrote, such as its modules in `AGENTS.md` and `Program.cs`, stay.
  - One that the application deleted stays deleted, and is listed when the template changed it: `.hardslice/generation/` then holds the new one.
  - One that the template no longer generates is deleted when the application did not change it, and listed otherwise.
  - One without a base, as in a solution generated before `.hardslice/generation/` existed, is left, and listed with its diff when it differs from the new generation: merge into it what the template changed, by hand or with the agent, and keep what the application wrote. The diff goes from the solution to the template, `-` lines in the solution only and `+` lines in the template only, with the headers of git (`diff --git a/<path> b/<path>`, `--- a/<path>`, `+++ b/<path>`) and `\ No newline at end of file` after a last line without a line break. The block of one file, cut out of the output from its `diff --git` line to the next `==> ` line, applies with `git apply`: it gives the file of the template, and the parts that the application wrote are then to put back.

  A project keeps its own `UserSecretsId`, which `dotnet new` draws anew at each generation: it is neither a difference nor a conflict. Last, the new generation replaces `.hardslice/generation/`, the base of the next upgrade: a solution without one, at its first upgrade, records it then.
- It lists the files of the folders of the template, such as `scripts/`, the architecture tests or `.claude/`, that the new generation does not have: delete each one that an older template generated, and keep one that a maintainer added.
- It never revisits a file that an item template generated, such as an aggregate, a slice or their tests: the application owns it from its generation. When a version changes what such a template writes, its line in [CHANGELOG.md](../CHANGELOG.md) gives the `git grep` that finds the files to change by hand.

The only change it accepts in the git tree is the one of `.config/dotnet-tools.json` by the update of the tool, so that `git diff` then shows its own changes alone, and `git checkout` and `git clean` undo them. Its last line counts each list. It exits with `0` when no conflict and no file is left to merge, the merged files listed to review, `3` when conflicts are left to resolve or files are listed to merge or to review, `1` when it fails, before writing anything unless its output says otherwise, and `2` on a wrong command line. Then run `dotnet hardslice check`: it passes, or stops on what is left to merge. The upgrade writes protected files: a maintainer runs it, never an agent, and its pull request needs the label `hardslice-approved`.

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

Keep a local secret, such as an API key, out of the repository with `dotnet user-secrets set <Key> <value> --project src/Rooms`: the project has its own `UserSecretsId`, Development reads the secret, and the integration tests never do.

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
