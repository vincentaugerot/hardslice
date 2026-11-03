# Working on HardSlice

This file is for agents that work on the HardSlice repository itself. The `AGENTS.md` that generated solutions ship is another file: `src/HardSlice.Templates/content/hardslice-api/AGENTS.md`.

Read [CONTRIBUTING.md](CONTRIBUTING.md) first: it holds the commands, the repository map and the conventions. The essentials:

## Verify

```text
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

`dotnet test` needs a Docker-compatible container runtime. A change to the template, the item templates, the analyzers or the tool also runs the probes of its area from `scripts/test-template.sh`, on a solution generated from the local packages. The full scenario runs in CI.

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
