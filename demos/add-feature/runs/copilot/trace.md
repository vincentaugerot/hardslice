# Run copilot: run of 01/11/2026

| | |
|---|---|
| Agent | GitHub Copilot CLI 1.0.105, headless (`copilot -p --output-format json`), model `claude-sonnet-4.6` |
| Mode | Native adapter: the hooks of `.github/hooks/hardslice.json` in a trusted folder; the skills of `.claude/skills/` and `.agents/skills/` |
| Prompt | [`prompt.md`](../../prompt.md), unchanged |
| Run | 5 min 26 s, 9 premium requests |
| Command | As in [GitHub Copilot CLI](../../README.md#github-copilot-cli), with `--log-dir` and `--log-level all` to read the answers of the hooks |
| Verdict | `pass`: `verify` passed on the workspace, `verify-recorded` passes on the recorded [changes](changes/) and [trace](trace.jsonl) |

## Path

**1. Skill and templates.** The agent loaded `add-module` only, through its skill tool, then ran every template in a row, before filling any file:

```text
dotnet new hardslice-module --project src/Shop/Shop.csproj --name Catalog
dotnet new hardslice-aggregate --project src/Shop/Shop.csproj --module Catalog --name Product
dotnet new hardslice-aggregate --project src/Shop/Shop.csproj --module Catalog --name Stock
dotnet new hardslice-command --project src/Shop/Shop.csproj --module Catalog --name RegisterProduct
dotnet new hardslice-query --project src/Shop/Shop.csproj --module Catalog --name GetProduct --aggregate Product
dotnet new hardslice-event --project src/Shop/Shop.csproj --module Catalog --name ProductRegistered --reaction Catalog/OpenStockOnProductRegistered
dotnet ef migrations add AddProducts --project src/Shop --output-dir Shared/Persistence/Migrations
dotnet ef migrations add AddStocks --project src/Shop --output-dir Shared/Persistence/Migrations
```

It did not load `add-aggregate`, `add-slice` nor `add-domain-event`: `AGENTS.md` names each template with its options, in "Adding a first module" and "Adding a slice". It added the lines the templates printed to `Program.cs` and `CatalogModule.cs`, and the module to the map of `AGENTS.md`.

**2. Domain.** `Product.Register(name, price)` throws a `DomainException` for a missing name, a name over 100 characters and a price that is not greater than zero, then records `ProductRegistered`, written `new Events.ProductRegistered(product.Id)`. `Stock.Open` opens a stock at zero by the identifier of its product. Unit tests: 8 for `Product`, 3 for `Stock`.

**3. Slices.** `RegisterProduct` keeps the route and the 200 of the template and returns the identifier; its integration test reads the product back from PostgreSQL and checks the 400 of an invalid request. `GetProduct` projects the name and the price without tracking, and answers 404 for an unknown identifier. The reaction adds the stock through `AppDbContext`; its test calls the handler as the pipeline does and reads the stock at zero from PostgreSQL. Tests: 5 of the validator, 5 integration tests. Its check: 30 unit tests, 84 architecture tests, 8 integration tests.

**4. Hooks.** Copilot loaded six hooks (`hookCount=6`): the three of `.github/hooks/hardslice.json` and the three of `.claude/settings.json`, which exit at once under Copilot. `postToolUse` answered `additionalContext` with the build errors 11 times, each an intermediate state of the agent's own edits, as it filled the generated files one after the other. `agentStop` answers nothing when the check passes: 20 s pass between the end of the last turn (04:55:53) and the end of the session (04:56:13), the time of the full check, and no block was recorded.

## Verification

`scripts/demo-add-feature.sh verify` passed: 28 files changed, none protected; the 18 expected files at their paths, the registrations, the event and the reaction found, no `TODO(hardslice):` marker; trusted check green; the trace runs the seven `dotnet new` calls above.

A first verification failed: the verifier looked for the literal `new ProductRegistered(` in `Product.cs`, and the agent qualified the event by its namespace. The checks of the contents now accept a type written with or without its namespace; the verification of the run passed after that change, and the runs of Claude Code and Codex pass it too.

## Incidents

`bash scripts/check.sh 2>&1` was refused: the run allowed `shell(bash scripts/check.sh)`, and Copilot did not match this line to it (`Permission denied because no interactive user response was available`). The agent ran `dotnet hardslice check` instead, the command that the script runs once the tools are restored.
