# HardSlice.Templates

`dotnet new` templates for [HardSlice](https://github.com/toregua/hardslice): vertical slices with a rich domain, kept clean by the build.

This is a preview. It holds the `hardslice-api` solution template: an ASP.NET Core API in vertical slices around a rich domain, persisted in PostgreSQL with EF Core, with a `Bookings` sample module, or empty with `--no-sample`.

```text
dotnet new hardslice-api --name Rooms
cd Rooms
bash scripts/check.sh     # macOS, Linux, Git Bash
pwsh scripts/check.ps1    # Windows, PowerShell
```

It also holds the `hardslice-command` item template, run from the root of such a solution once it is restored. It creates a command slice of an existing module, with its validator tests and its integration tests, and prints the lines that register it in its module:

```text
dotnet new hardslice-command --project src/Rooms/Rooms.csproj --module Bookings --name ExtendBooking
```

Every generated solution references [HardSlice.Analyzers](https://www.nuget.org/packages/HardSlice.Analyzers) and pins the [HardSlice.Tool](https://www.nuget.org/packages/HardSlice.Tool) local tool at the same version, holds architecture tests, an `AGENTS.md` for coding agents and the sheet of every rule in `docs/rules/`. The check scripts run `dotnet hardslice check`: the build, then the unit, architecture and integration tests. The integration tests start PostgreSQL with Testcontainers: they need a container runtime.

## Documentation

- [Getting started](https://github.com/toregua/hardslice/blob/main/README.md#getting-started)
- [Rules](https://github.com/toregua/hardslice/blob/main/README.md#rules)
- [License (MIT)](https://github.com/toregua/hardslice/blob/main/LICENSE)
