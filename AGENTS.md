# Working on HardSlice

This file is for agents that work on the HardSlice repository itself. The `AGENTS.md` that generated solutions ship is another file: `src/HardSlice.Templates/content/hardslice-api/AGENTS.md`.

Read [CONTRIBUTING.md](CONTRIBUTING.md) first: it holds the commands, the repository map and the conventions. The essentials:

## Verify

```text
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

`dotnet test` needs a Docker-compatible container runtime. A change to the template, the item templates, the analyzers or the tool also runs the groups of its area from `scripts/test-template.sh`, with `--only`, as the table of [CONTRIBUTING.md](CONTRIBUTING.md) maps them: `scripts/test-template.sh --only sample-rules,blank-rules` for a rule. Do not run the full scenario on your machine, which takes about an hour: push the commits of a ticket, its work and its status at once, to a branch `ci/<ticket>` and wait for its CI, which runs every group in parallel, then push them to `main`. In a series of queued tickets, each one commits on `main` locally and pushes to its `ci/<ticket>` without waiting, and the last one reads each CI in order, fixes the first red one, then waits for its own, which holds the whole series, and pushes `main`. A failed group is fixed, then run again alone.

## Do not break

- Everything the packages ship is frozen once published: the generated code, the agent kit, the rule sheets, the texts of the diagnostics, the package READMEs, the format of `hardslice.json`, the exit codes of `dotnet hardslice` and the list of protected files. Change them deliberately, with a test.
- The generated `AGENTS.md` stays under 150 lines, with and without the sample.
- The generated solution takes no runtime dependency on HardSlice.
- The rule table of `README.md` lists every sheet of `docs/rules/` and no other, and every relative link of the documentation exists (`ReadmeTests`).
- `docs/agents/support.md` and `docs/performance/analyzers.md` keep their paths: tests and `scripts/measure-analyzers.sh` read them.

## Write

- English for code, diagnostics, shipped content and user documentation. French for `docs/design/`.
- No em dash anywhere outside the raw traces of `demos/`.
- Conventional Commits, with the ticket of the [backlog](docs/design/product-backlog.md) at the end: `fix(tool): ... (PB-053)`.
- A fixed finding gets a test that failed before the fix.
