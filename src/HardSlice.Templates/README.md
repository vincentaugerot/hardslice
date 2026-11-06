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

It also holds seven item templates, run from the root of such a solution once it is restored. `hardslice-module` creates a module, its public contract that registers and maps its slices on one route group, and prints the lines that register it in `Program.cs`. `hardslice-aggregate` creates an aggregate of an existing module, its typed identifier, its EF Core mapping with a concurrency token on `xmin`, its unit tests and the integration test of that token, and prints the migration to add. `hardslice-entity` creates a child entity of an existing aggregate, held in a collection of its root or, with `--link id`, holding the identifier of its root for a collection too large to load, with its identifier, its mapping and its unit tests. `hardslice-command` creates a command slice of an existing module, with its validator tests and its integration tests: `--route` and `--method` choose its endpoint, `--parameters ServiceDate:DateOnly` names its fields, each with its rule and its tests, `--parameters none` makes a command without parameters and without validator, `--endpoint none` a system command that only the application sends, without endpoint, `--every 00:00:30` a system command that the application sends at that interval through `AddRecurringCommand`, `--daily-at 04:30 --time-zone America/Toronto` one that it sends every day at that local time, `--retry 01:00:00x3` sends either again after a failed cycle, and `--at-start` once when the application starts too. `hardslice-query` creates a query slice that reads an aggregate of the module without tracking, one DTO by its id, with `--result many` a list, or with `--result filtered --parameters From:DateOnly,To:DateOnly` the list that its filters select, from the query string or the route that names them, with its validator, and its integration tests, under the route of `--route`. Both take `--require <policy>`, which protects the endpoint with an authorization policy of the application, tests its 401, and sends the other tests through the client of the policy that `AppFactory` holds. Both print the lines that register the slice in its module. `hardslice-event` creates a domain event of an existing module and, with `--reaction <Module>/<Slice>`, the reaction slice that handles it, with its integration tests, and prints the lines that register its handler. `hardslice-condition` creates the condition of a command that `AddRecurringCommand` sends, new or existing, which decides before each cycle whether it is sent, with its integration tests, and prints the line that registers it:

```text
dotnet new hardslice-module --project src/Rooms/Rooms.csproj --name Payments
dotnet new hardslice-aggregate --project src/Rooms/Rooms.csproj --module Payments --name Invoice
dotnet new hardslice-entity --project src/Rooms/Rooms.csproj --module Payments --aggregate Invoice --name InvoiceLine
dotnet new hardslice-command --project src/Rooms/Rooms.csproj --module Bookings --name ExtendBooking --route extensions
dotnet new hardslice-command --project src/Rooms/Rooms.csproj --module Payments --name RemindUnpaidInvoices --every 01:00:00
dotnet new hardslice-command --project src/Rooms/Rooms.csproj --module Payments --name ImportExchangeRates --daily-at 04:30 --time-zone America/Toronto --retry 01:00:00x3
dotnet new hardslice-command --project src/Rooms/Rooms.csproj --module Payments --name RefundInvoice --route refunds --require Accounting
dotnet new hardslice-query --project src/Rooms/Rooms.csproj --module Bookings --name GetBookingSummary --aggregate Booking --route summaries
dotnet new hardslice-event --project src/Rooms/Rooms.csproj --module Bookings --name BookingExtended --reaction Payments/InvoiceOnBookingExtended
dotnet new hardslice-condition --project src/Rooms/Rooms.csproj --module Payments --command ImportExchangeRates
```

Every generated solution references [HardSlice.Analyzers](https://www.nuget.org/packages/HardSlice.Analyzers) and pins the [HardSlice.Tool](https://www.nuget.org/packages/HardSlice.Tool) local tool at the same version, holds architecture tests, an `AGENTS.md` for coding agents with the instruction files of Claude Code (`.claude/CLAUDE.md`), Gemini CLI (`GEMINI.md`) and GitHub Copilot (`.github/copilot-instructions.md`), the skills `add-module`, `add-aggregate`, `add-slice`, `add-domain-event`, `extract-to-domain` and `fix-architecture-violation` in `.claude/skills/` and `.agents/skills/`, and the sheet of every rule in `docs/rules/`. The hooks of Claude Code (`.claude/settings.json`), Codex (`.codex/hooks.json`) and GitHub Copilot CLI (`.github/hooks/hardslice.json`) run `dotnet hardslice hook`: they refuse an edit of the files that set the rules, hand the build errors back to the agent after each edit (the errors of the edited files in full, a count of the others), and keep it working while the check fails before it hands back, except on a protected HardSlice file that only a maintainer can fix, which they hand to the agent once. Codex runs them only once a human approves each one in `/hooks`. A pre-commit hook, `.githooks/pre-commit`, once turned on by `git config core.hooksPath .githooks`, and a GitHub Actions workflow, `.github/workflows/hardslice.yml`, run the same check, and the workflow fails a pull request that changes a protected file without the label `hardslice-approved`. The check scripts run `dotnet hardslice check`: the build, then the unit, architecture and integration tests. The integration tests start PostgreSQL with Testcontainers: they need a container runtime. The solution records in `.hardslice/generation/`, a protected folder, its files of the application as the template generated them. Once the tool is updated to a later version, a maintainer runs `dotnet hardslice upgrade`, which rewrites the files that the template owns from a new generation, adds the files the solution lacks, and merges the files of the application in three ways from that record: it updates those that the application did not change and writes a conflict between the markers of git where both changed the same lines.

## Documentation

- [Getting started](https://github.com/vincentaugerot/hardslice/blob/v0.3.0-preview.1/docs/getting-started.md)
- [Item templates](https://github.com/vincentaugerot/hardslice/blob/v0.3.0-preview.1/docs/item-templates.md)
- [Agent loop: hooks, protected files and skills](https://github.com/vincentaugerot/hardslice/blob/v0.3.0-preview.1/docs/agent-loop.md)
- [Rules](https://github.com/vincentaugerot/hardslice/blob/v0.3.0-preview.1/docs/rules.md)
- [Changelog](https://github.com/vincentaugerot/hardslice/blob/v0.3.0-preview.1/CHANGELOG.md)
- [License (MIT)](https://github.com/vincentaugerot/hardslice/blob/main/LICENSE)
