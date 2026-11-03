# HardSlice

Vertical slices, kept clean by the build.

[![CI](https://github.com/vincentaugerot/hardslice/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/vincentaugerot/hardslice/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

HardSlice is a .NET kit that makes AI coding agents (and humans) respect a vertical slice architecture with a rich domain. The rules are not advice in a prompt: a violation fails `dotnet build` or `dotnet test`, with a one-line diagnostic that names the offending symbol, the expected fix, the skill that fixes it and a rule sheet. An agent reads it and repairs the architecture instead of working around it:

```text
HS016: 'CancelBookingHandler' in slice 'Bookings/CancelBooking' uses 'ConfirmBookingHandler' from slice 'Bookings/ConfirmBooking'. Slices never reference each other. Fix: move the shared rule into a domain aggregate, raise a domain event, or use the public contract of module 'Bookings'. Skill: extract-to-domain. Doc: 'docs/rules/HS016.md'.
```

HardSlice adds nothing to the runtime of your application: its analyzers run at build time only, and its architecture tests live in your test projects.

## Status

**Preview.** The first preview, `0.3.0-preview.1`, is being prepared for NuGet.org; until it is published, [try a local build of the packages](CONTRIBUTING.md#try-a-local-build-of-the-packages). The three packages, `HardSlice.Templates`, `HardSlice.Analyzers` and `HardSlice.Tool`, share one version. A preview may change its rules, its template and its commands from one preview to the next; a generated solution keeps the template and the versions it was generated with. The [changelog](CHANGELOG.md) says what each version brings.

## Getting started

You need the .NET 10 SDK, a Docker-compatible container runtime for the integration tests, and Bash or PowerShell 7.

```text
dotnet new install HardSlice.Templates@0.3.0-preview.1
dotnet new hardslice-api --name Rooms
cd Rooms
bash scripts/check.sh     # or: pwsh scripts/check.ps1
```

The check builds the solution and runs its unit, architecture and integration tests; it exits 0 when everything passes. `Rooms` holds a `Bookings` sample module (`--no-sample` leaves it out). Then ask your agent to read `AGENTS.md` and give it a feature: it generates each element with a template, completes it, and is not done until the check passes. For example, the command slice it would generate to extend a booking:

```text
dotnet new hardslice-command --project src/Rooms/Rooms.csproj --module Bookings --name ExtendBooking
```

[Getting started](docs/getting-started.md) has every step in detail, how to run the API against PostgreSQL, and what the check does; [Item templates](docs/item-templates.md) covers the five element templates.

## What you get

| Part | What it does |
|---|---|
| `hardslice-api` solution template | An ASP.NET Core API in vertical slices around a separate domain project, with PostgreSQL, EF Core, a small in-house mediator and FluentValidation, its unit, integration and architecture tests, and the `Bookings` sample or none. |
| Five item templates | `hardslice-module`, `hardslice-aggregate`, `hardslice-command`, `hardslice-query` and `hardslice-event` generate an element in its place with its tests, which fail until the element is completed. |
| `HardSlice.Analyzers` | Roslyn analyzers that report the rules at build time and in the IDE. |
| Architecture tests | The rules that span projects, modules and test folders, in every generated solution. |
| `dotnet hardslice` | A local tool pinned to the version of the analyzers: `check` runs the whole verification, `explain` prints the sheet of a rule, `hook` runs the hooks of the agents, `protected` lists the protected files. |
| Agent kit | `AGENTS.md` and the instruction file of each agent, six skills, the hooks of Claude Code, Codex and Copilot CLI, a pre-commit hook and a GitHub Actions workflow. |

## Supported agents

| Agent | Reads | Level today |
|---|---|---|
| Claude Code | `.claude/CLAUDE.md`, which imports `AGENTS.md` | native: the hooks of `.claude/settings.json` |
| Codex | `AGENTS.md` | native: the hooks of `.codex/hooks.json`, once approved in `/hooks` |
| GitHub Copilot CLI | `AGENTS.md`, `.github/copilot-instructions.md` and `GEMINI.md` | native: the hooks of `.github/hooks/hardslice.json` |
| Gemini CLI | `GEMINI.md`, which imports `AGENTS.md` | fallback |

A native adapter refuses an edit of the files that set the rules, hands the build errors back to the agent after each edit, and keeps it working while the check fails at the end of its turn. With the fallback, the agent follows `AGENTS.md`, which tells it to run the check before it finishes. Codex skips the hooks of a project until a human approves each one in `/hooks`. [Agent loop](docs/agent-loop.md) details the hooks, the protected files and the skills.

## Rules

| Rule | Checks | Reported by |
|---|---|---|
| [HS000](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS000.md) | Every suppression of a HardSlice rule in source, `#pragma warning disable` or `[SuppressMessage]`, carries a `// hardslice-justification: <reason>` comment of at least ten characters on its line or the line above. HS000 itself cannot be suppressed. | Analyzer at build time, test projects included |
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
| [HS900](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS900.md) | `hardslice.json` is usable: it declares `"version": 1`, the version of its format, and the contracts `IEntity`, `IAggregateRoot`, `IDomainEvent`, `ICommand<T>`, `IQuery<T>` and `IRequestHandler<,>`, and each one exists in its declaring assembly. | Analyzer at build time |
| [HS901](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS901.md) | A type has one role: never both an entity and a domain event, a command and a query, or an entity and a request. | Analyzer at build time |
| [HS902](src/HardSlice.Templates/content/hardslice-api/docs/rules/HS902.md) | Every domain type EF Core maps as an entity implements `IEntity` or `IAggregateRoot`. | Analyzer at build time |

Each generated solution carries these sheets in `docs/rules/`, and `dotnet hardslice explain <rule>` prints them. The MSBuild property `HardSliceAnalysisMode` (`Minimum`, `Recommended` or `All`, the one a generated solution sets) chooses which analyzer rules are on. [Rules](docs/rules.md) has the matrix of the modes and the known limits of each rule.

## Evidence

- [Agent fix demonstrations](demos/agent-fix/README.md): for each blocking rule, a solution that breaks it fails with exactly the diagnostic of that rule, the reference fix passes, and every known workaround is rejected; Claude Code fixed four of them, guided by the diagnostic and its rule sheet. CI replays them on every push.
- [Add-feature demonstration](demos/add-feature/README.md): Claude Code, Codex and Copilot CLI each delivered one business request (a module, two aggregates, a command, a query and a domain event with its reaction) through the templates, the check green and no protected file changed, as a verifier proves from their recorded traces.
- [Analyzer cost](docs/performance/analyzers.md): on a solution of 21 modules, the analyzers add about 0.2 s to an incremental build after an edit, and the checks of the hooks hold their budgets of 10 s after an edit and 60 s before an agent hands back.

These are demonstrations, not a benchmark: each agent ran once. A benchmark with and without HardSlice is the next milestone.

## Known limitations

- Only solutions generated by `hardslice-api` are supported; adding HardSlice to an existing solution is planned for v0.5.
- PostgreSQL is the only database; SQL Server and Aspire are planned for v1.0.
- Domain events are handled inside the transaction of the command: no outbox, no bus, no external effect in a handler.
- Gemini CLI stays at the fallback level: it could not be tried, as it no longer serves personal Google accounts.
- The hooks, the pre-commit hook and the generated CI stop accidental drift; they are not a security boundary until the branch is protected.

The limits of each rule, of the item templates and of the agent loop are in [Rules](docs/rules.md), [Item templates](docs/item-templates.md#limitations) and [Agent loop](docs/agent-loop.md#limitations).

## Roadmap

| Version | Brings |
|---|---|
| v0.4 | A public, reproducible benchmark of agents with and without HardSlice. |
| v0.5 | Adoption of an existing solution, layered or in slices: `init`, `map`, `baseline` with a ratchet, `report`. |
| v0.6 | A benchmark of layers against vertical slices for agents. |
| v1.0 | Stable packages, a documentation site, SQL Server and Aspire options, Azure Pipelines. |

The [delivery plan](docs/design/plan.md), in French, details each version.

## Contributing, security and license

- [Contributing](CONTRIBUTING.md): build, test, conventions, and how to try a local build of the packages.
- [Security](SECURITY.md): how to report a way around the protections in private.
- [Documentation](docs/README.md): every page, in English, and the design documents, in French.
- License: [MIT](LICENSE).
