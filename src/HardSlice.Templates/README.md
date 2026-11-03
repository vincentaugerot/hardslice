# HardSlice.Templates

`dotnet new` templates for [HardSlice](https://github.com/vincentaugerot/hardslice): vertical slices with a rich domain, kept clean by the build.

This is a preview. It holds the `hardslice-api` solution template: an ASP.NET Core API in vertical slices around a rich domain, persisted in PostgreSQL with EF Core, with a `Bookings` sample module, or empty with `--no-sample`.

```text
dotnet new install HardSlice.Templates@0.3.0-preview.1
dotnet new hardslice-api --name Rooms
cd Rooms
bash scripts/check.sh     # macOS, Linux, Git Bash
pwsh scripts/check.ps1    # Windows, PowerShell
```

It also holds five item templates, run from the root of such a solution once it is restored. `hardslice-module` creates a module, its public contract that registers and maps its slices on one route group, the folder of its domain and its test folders, and prints the lines that register it in `Program.cs`. `hardslice-aggregate` creates an aggregate of an existing module, its typed identifier, its EF Core mapping and its unit tests, and prints the migration to add. `hardslice-command` creates a command slice of an existing module, with its validator tests and its integration tests; `hardslice-query` creates a query slice that reads an aggregate of the module without tracking, one DTO by its id or, with `--result many`, a list, with its integration tests. Both print the lines that register the slice in its module. `hardslice-event` creates a domain event of an existing module and, with `--reaction <Module>/<Slice>`, the reaction slice that handles it, with its integration tests, and prints the lines that register its handler:

```text
dotnet new hardslice-module --project src/Rooms/Rooms.csproj --name Payments
dotnet new hardslice-aggregate --project src/Rooms/Rooms.csproj --module Payments --name Invoice
dotnet new hardslice-command --project src/Rooms/Rooms.csproj --module Bookings --name ExtendBooking
dotnet new hardslice-query --project src/Rooms/Rooms.csproj --module Bookings --name GetBookingSummary --aggregate Booking
dotnet new hardslice-event --project src/Rooms/Rooms.csproj --module Bookings --name BookingExtended --reaction Payments/InvoiceOnBookingExtended
```

Every generated solution references [HardSlice.Analyzers](https://www.nuget.org/packages/HardSlice.Analyzers) and pins the [HardSlice.Tool](https://www.nuget.org/packages/HardSlice.Tool) local tool at the same version, holds architecture tests, an `AGENTS.md` for coding agents with the instruction files of Claude Code (`.claude/CLAUDE.md`), Gemini CLI (`GEMINI.md`) and GitHub Copilot (`.github/copilot-instructions.md`), the skills `add-module`, `add-aggregate`, `add-slice`, `add-domain-event`, `extract-to-domain` and `fix-architecture-violation` in `.claude/skills/` and `.agents/skills/`, and the sheet of every rule in `docs/rules/`. The hooks of Claude Code (`.claude/settings.json`), Codex (`.codex/hooks.json`) and GitHub Copilot CLI (`.github/hooks/hardslice.json`) run `dotnet hardslice hook`: they refuse an edit of the files that set the rules, hand the build errors back to the agent after each edit, and keep it working while the check fails before it hands back. Codex runs them only once a human approves each one in `/hooks`. A pre-commit hook, `.githooks/pre-commit`, once turned on by `git config core.hooksPath .githooks`, and a GitHub Actions workflow, `.github/workflows/hardslice.yml`, run the same check, and the workflow fails a pull request that changes a protected file without the label `hardslice-approved`. The check scripts run `dotnet hardslice check`: the build, then the unit, architecture and integration tests. The integration tests start PostgreSQL with Testcontainers: they need a container runtime.

## Documentation

- [Getting started](https://github.com/vincentaugerot/hardslice/blob/v0.3.0-preview.1/docs/getting-started.md)
- [Item templates](https://github.com/vincentaugerot/hardslice/blob/v0.3.0-preview.1/docs/item-templates.md)
- [Agent loop: hooks, protected files and skills](https://github.com/vincentaugerot/hardslice/blob/v0.3.0-preview.1/docs/agent-loop.md)
- [Rules](https://github.com/vincentaugerot/hardslice/blob/v0.3.0-preview.1/docs/rules.md)
- [Changelog](https://github.com/vincentaugerot/hardslice/blob/v0.3.0-preview.1/CHANGELOG.md)
- [License (MIT)](https://github.com/vincentaugerot/hardslice/blob/main/LICENSE)
