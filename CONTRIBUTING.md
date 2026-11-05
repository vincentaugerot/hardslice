# Contributing to HardSlice

HardSlice is in preview, with one maintainer. Issues and pull requests are welcome. For anything larger than a fix, open an issue first so that we agree on the change before you write it. A way around the protections of a generated solution is a security report: see [SECURITY.md](SECURITY.md).

## Build and test

You need the .NET SDK pinned by `global.json`, a Docker-compatible container runtime, git, Python 3 and PowerShell 7 (for `scripts/test-template.sh`).

```text
dotnet restore
dotnet build --no-restore
dotnet test --no-build
scripts/test-template.sh
scripts/demo-agent-fix.sh verify-reference "$(mktemp -d)"
scripts/demo-add-feature.sh verify-recorded "$(mktemp -d)"
scripts/measure-analyzers.sh
```

- `dotnet test` runs the analyzer tests, the tool tests and the tests of the template content, integration tests included: it needs a container runtime.
- `scripts/test-template.sh` packs the three packages and generates applications from them: with and without the sample, and under names that are not plain identifiers (`Acme.Rooms`, `my-app`). On each, it runs the path of [Getting started](docs/getting-started.md), the API included, verifies it with both check scripts, then seeds one violation per rule and expects both scripts to fail with its diagnostic, its sheet and a skill that the solution ships. It applies every item template through each of its refusals and checks that only the `TODO(hardslice):` tests fail until a reference completion passes the check. It checks the pre-commit hook, the job `protected-files` of the generated workflow, the hooks of Claude Code, Codex and Copilot CLI on protected files, a package added by the application with the check failing once a project no longer receives `HardSlice.Analyzers`, and both modes of the check scripts. It needs PowerShell 7, git and a container runtime. The probes run in named groups, which `scripts/test-template.sh --list` prints, and `--only <group>[,<group>...]` runs some of them alone, each on the solution it needs, to verify a change in its area or to resume after a failure. Without argument, the script runs every group in one sequence, as the release does.

  | A change to | Groups to run |
  |---|---|
  | a rule of the analyzers, its diagnostic or its sheet | `sample-rules,blank-rules` |
  | the check scripts, the hooks or the commands `check` and `hook` of the tool | `sample-hooks` |
  | an item template or the skill that applies it | `sample-items,blank-items` |
  | the generated solution, its `AGENTS.md` or `docs/getting-started.md` | `sample,blank` |
  | the list of protected files or `dotnet hardslice upgrade` | `upgrade` |
  | the form of the name of the application | `names` |
- `scripts/verify-published.sh <version> <dir> [--source <folder>] [--expect <folder>] [--clean]` installs `HardSlice.Templates` at that version from NuGet.org, or from a folder of packages, in a home of its own, checks the README, the icon and the release notes of the three packages, generates a solution with and without the sample, runs `dotnet tool restore` then `dotnet hardslice check` in each, adds a module and a command slice with `hardslice-module` and `hardslice-command`, completes them and checks again, and checks that the template, the analyzers and the tool come from that source. `--expect` also requires the packages served to have the content of the packages of a folder, and `--clean` removes `<dir>` at the end.
- `scripts/publish-packages.sh` pushes the packages of a release to NuGet.org for `.github/workflows/release.yml`, only while the tag points to the commit of the run and the versions already served have the content of the run ([Releasing](docs/releasing.md)).
- `scripts/generated-workflow.sh lint|run` validates the workflow of a generated solution with actionlint, or runs the run steps of one of its jobs, in containers pinned by digest.
- `scripts/demo-agent-fix.sh` and `scripts/demo-add-feature.sh` prepare and verify the [demonstrations](demos/README.md): `prepare` gives an agent a solution to work on, `verify` accepts its work only if the trusted check passes without any protected file, suppression or weakened test changed, and `verify-reference` and `verify-recorded` replay the reference fixes and the recorded runs.
- `scripts/measure-analyzers.sh` measures the build time of a large generated application with and without the analyzers, and the checks of the hooks, then records the measurement in [the performance report](docs/performance/analyzers.md).

CI runs on every push to `main`, to a branch `ci/<name>` and on every pull request, each job on a fresh runner from a fresh checkout, without cache: build and tests, template, demonstration, and generated workflow. The template runs as one job per group of `scripts/test-template.sh`, in parallel. To verify a change on every group before it reaches `main`, push it to a branch `ci/<ticket>`, such as `ci/pb-089`, and wait for its CI: that is faster than the full scenario on a workstation. A fifth job, performance, measures the analyzers on every push to `main` and on the pull requests that change the analyzers, the template, the tool or the measurement. [Releasing](docs/releasing.md) describes the release workflow.

### When a local tool stays missing after `dotnet tool restore`

`dotnet ef` or `dotnet hardslice` may answer `Run "dotnet tool restore" to make the "dotnet-ef" command available.` right after a successful `dotnet tool restore`. The cache of the local tools of your machine then points to a package folder that no longer exists. A `dotnet tool restore` run with a temporary `NUGET_PACKAGES` but your usual home records that folder in `~/.dotnet/toolResolverCache/1/`, and a later restore finds the package in its place and never corrects the entry. A probe of `scripts/test-template.sh` run alone without `isolate_dotnet_cli_home` (`scripts/dotnet-cli-home.sh`) does exactly that, while the full run gives every restore a home of its own. To fix it, delete the file named after the package in `~/.dotnet/toolResolverCache/1/`, such as `dotnet-ef` or `hardslice.tool`, then run `dotnet tool restore` again. A user of the published packages is not concerned.

## Repository map

| Path | Contains |
|---|---|
| `src/HardSlice.Analyzers/` | The Roslyn analyzer package. |
| `src/HardSlice.Templates/content/hardslice-api/` | The solution template, `MyApp` being replaced by the name you choose, in its safe namespace form. |
| `src/HardSlice.Templates/content/hardslice-module/`, `-aggregate/`, `-entity/`, `-command/`, `-query/`, `-event/`, `-condition/` | The item templates, `MyApp`, `ModuleName` and the names of the element being replaced by the application, the module and the element. |
| `src/HardSlice.Tool/` | The `dotnet hardslice` local tool, which embeds the rule sheets of the template. |
| `tests/HardSlice.Analyzers.Tests/` | Analyzer tests, tests that compile the template content as is, and the tests of this documentation. |
| `tests/HardSlice.Tool.Tests/` | Tests of `check`, `explain`, the hooks and `protected`, with the payloads recorded from each CLI. |
| `scripts/` | Template, demonstration, release and performance scenarios. |
| `demos/` | The agent fix and add-feature demonstrations, with their recorded runs. |
| `docs/` | The user documentation, in English, and the design documents of `docs/design/`, in French. |
| `.github/workflows/` | CI, performance and release workflows. |

## Try a local build of the packages

From a clone of this repository:

```text
VERSION=$(dotnet msbuild src/HardSlice.Templates/HardSlice.Templates.csproj -getProperty:Version)
dotnet pack src/HardSlice.Analyzers/HardSlice.Analyzers.csproj --configuration Release --output artifacts/packages
dotnet pack src/HardSlice.Tool/HardSlice.Tool.csproj --configuration Release --output artifacts/packages
dotnet pack src/HardSlice.Templates/HardSlice.Templates.csproj --configuration Release --output artifacts/packages
dotnet new install "artifacts/packages/HardSlice.Templates.$VERSION.nupkg" --force
```

`artifacts/packages` then holds `HardSlice.Analyzers`, `HardSlice.Tool` and `HardSlice.Templates` at the version of `Directory.Build.props`, which a rebuild does not change. The generated solutions must restore the first two from that folder, not from NuGet.org. Create a folder for them, outside this repository, with this `nuget.config`, replacing the `value` of `hardslice-local` with the absolute path of `artifacts/packages`:

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

The mapping makes NuGet take `HardSlice.*` packages from your local folder only, never from NuGet.org. Then follow [Getting started](docs/getting-started.md) from step 2 in that folder. After a rebuild, repack the three packages and reinstall the template as above. A rebuild keeps the version, so two caches of your machine still serve the previous build without a word, and both must be purged: the global packages folder of NuGet, with `dotnet nuget locals global-packages --clear` (or only the folders `hardslice.analyzers`, `hardslice.tool` and `hardslice.templates` of `~/.nuget/packages/`), and the file `hardslice.tool` of `~/.dotnet/toolResolverCache/1/`. Otherwise `dotnet restore` and `dotnet tool restore` keep giving the solutions the previous builds of `HardSlice.Analyzers` and `HardSlice.Tool`.

## Conventions

- **Language.** Code, diagnostics, everything the packages ship and the user documentation (`README.md`, `docs/` outside `docs/design/`) are in English. The design documents of `docs/design/` are in French. No em dash anywhere, outside the raw traces of `demos/`: `git grep -nP '\x{2014}' -- . ':!demos'` must print nothing. No semicolon in the prose of this repository's documentation either (root guides, `docs/`, package and demonstration READMEs), checked by `ReadmeTests`: code keeps its own, and the files the template writes into a generated solution are not concerned.
- **Tickets.** Work is planned in the [backlog](docs/design/product-backlog.md) as tickets `PB-xxx`, each with its observable result, its scope and an acceptance command. A finding that is fixed gets a test that failed before the fix: a unit test, a template test, or a probe of `scripts/test-template.sh`.
- **Commits.** [Conventional Commits](https://www.conventionalcommits.org/), with the ticket at the end when there is one: `fix(analyzers): stay safe on code being typed (PB-054)`.
- **A new rule** comes in one change with its descriptor in `AnalyzerReleases.Unshipped.md`, its sheet in `src/HardSlice.Templates/content/hardslice-api/docs/rules/`, its row in the rule table of `README.md` (checked by `ReadmeTests`) and in `AGENTS.md`, its analyzer tests (compliant, faulty and cross-assembly cases), a probe in `scripts/test-template.sh` with and without the sample, its line in the matrix of the analysis modes, and a performance measurement within the thresholds.
- **The generated `AGENTS.md`** stays under 150 lines with and without the sample, checked by a test. The generated solution takes no runtime dependency on HardSlice.
- **Hooks** are tested with the payloads each CLI really sends, recorded in `tests/HardSlice.Tool.Tests/Fixtures/hooks/` and listed in [Agent support](docs/agents/support.md).
- **Links.** Every relative link of `README.md`, of the root guides and of `docs/` must exist (`ReadmeTests`). The package READMEs use absolute links only, as NuGet.org shows them.
