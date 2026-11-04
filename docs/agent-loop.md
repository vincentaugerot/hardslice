# Agent loop

Every generated solution ships what a coding agent needs to work inside the rules: instructions it loads by itself, skills that turn a request into the right [item template](item-templates.md), hooks that check its edits as it makes them, and a check that runs again before each commit and in CI. The rules themselves never depend on the agent: a violation fails `dotnet build` or `dotnet test` whoever wrote it.

## What each agent reads

Each supported agent loads `AGENTS.md` by itself, through its own instruction file when it needs one, which holds only what is specific to it:

| Agent | Reads | Level today |
|---|---|---|
| Claude Code | `.claude/CLAUDE.md`, which imports `AGENTS.md` | native: the hooks of `.claude/settings.json` |
| Codex | `AGENTS.md` | native: the hooks of `.codex/hooks.json`, once approved in `/hooks` |
| GitHub Copilot CLI | `AGENTS.md`, `.github/copilot-instructions.md` and `GEMINI.md` | native: the hooks of `.github/hooks/hardslice.json` |
| Gemini CLI | `GEMINI.md`, which imports `AGENTS.md` | fallback |

A native adapter runs the check through the hooks of the agent, after each edit and before it hands back. With the fallback, nothing checks an edit as it is made: the agent follows `AGENTS.md`, which tells it to run the check script before it finishes. Gemini CLI has hooks too, but no adapter is shipped until one is tried, and no `.gemini/settings.json` either: Gemini CLI could not be tried, as it no longer serves personal Google accounts.

`CLAUDE.md` lives in `.claude/` because Copilot reads a `CLAUDE.md` at the root and expands its import, which would load `AGENTS.md` twice. [Agent support](agents/support.md) records what each CLI reads, at a dated version, with the trials behind each choice.

## Hooks

Each hook runs `dotnet hardslice hook <cli> <event>`, which reads the payload of the CLI on its standard input, the same command on Linux, macOS and Windows, and answers the way the CLI hands it to its agent:

| Step | Runs | Budget | On failure |
|---|---|---|---|
| before an edit | the list of protected files | none | the edit is refused with the reason and a pointer to "Forbidden" of `AGENTS.md` |
| after the edit of a `.cs` file | the check of `scripts/check.sh --build` | 10 s | the errors of the build reach the agent, which fixes them. Those of the edited files come in full, those of the other files only as a count, since the next edits of a change may fix them. When no error is in an edited file, every error comes in full |
| the end of a turn | the check of `scripts/check.sh` | 60 s | the agent goes on with the failure. After three relaunches for the same failure, or eight in a session, it hands back and says that the check fails. A missing or mismatched protected HardSlice file, which only a maintainer can fix, reaches the agent once in the session, which then hands back and tells the user what a maintainer must do |

| CLI | Configuration | Before an edit | After an edit | End of a turn | Answer |
|---|---|---|---|---|---|
| Claude Code | `.claude/settings.json` | `PreToolUse` on `Edit`, `Write` | `PostToolUse` on `Edit`, `Write` | `Stop` | code 2 and the standard error |
| Codex | `.codex/hooks.json` | `PreToolUse` on `apply_patch` | `PostToolUse` on `apply_patch`, every file of the patch | `Stop` | code 2 and the standard error |
| Copilot CLI | `.github/hooks/hardslice.json` | `preToolUse` on `edit`, `create` | `postToolUse` on `edit`, `create` | `agentStop` | JSON with code 0: `permissionDecision`, `additionalContext`, `decision: "block"` |

When the end check passes, Claude Code and Codex show the user `HardSlice: the check passed.`: the agent wrote its last message before the check ran. Copilot CLI has no message for the user: it finishes without a word.

The budgets hold on a generated solution with 20 extra modules of 10 slices each, as [measured](performance/analyzers.md) on every push to `main`.

### Approving the hooks of Codex

Codex runs the hooks of a project only once it is trusted and each hook is approved: start `codex` in the solution, open `/hooks`, review the three hooks of `.codex/hooks.json` and trust them. Codex records the approval for this clone in `~/.codex/config.toml`. Until then, it skips the hooks without a message, and only the check that `AGENTS.md` asks for applies. The commands never change from one version of HardSlice to the next, so an upgrade keeps the approval. Copilot CLI runs the hooks of a trusted folder only.

## Protected files

The protected files are `.editorconfig`, `.globalconfig`, `Directory.Build.props`, `Directory.Build.targets` and `Directory.Build.rsp` in any folder, `hardslice.json`, `Directory.HardSlice.props`, `.config/dotnet-tools.json`, `Directory.Solution.props`, `Directory.Solution.targets` and `nuget.config` at the root, `scripts/`, `tests/<App>.ArchitectureTests/`, the hooks and skills in `.claude/`, `.agents/`, `.codex/` and `.github/hooks/`, the pre-commit hook in `.githooks/` and the CI in `.github/workflows/hardslice.yml`. They are the files that set the rules, so an agent that changes one could turn a rule off instead of fixing the code.

`Directory.Packages.props` belongs to the application: an agent adds a package there, with its version, and references it from the project that uses it. It imports `Directory.HardSlice.props`, which references `HardSlice.Analyzers` for every project. `check` fails, naming the file, when the import is gone, and after the restore, naming the project, when a project no longer receives the package, for example through a `Remove` or an `ExcludeAssets` of its reference or another `Directory.Packages.props` in a folder of the project.

"Forbidden" of `AGENTS.md` holds this list for agents, and the skills link to it. `dotnet hardslice protected` reads paths on its standard input, one per line or separated by NUL characters, lists the protected ones and exits 1 when there is one.

`dotnet hardslice upgrade` rewrites the protected files and the rule sheets from the template of a newer version of the tool, and lists the files of the application to merge, as [Upgrading a generated solution](getting-started.md#upgrading-a-generated-solution) describes. A maintainer runs it, never an agent, and its pull request needs the label `hardslice-approved`.

The hooks run in the process of the local tool, restored by the check script or `dotnet tool restore`. Until then, `dotnet hardslice` only prints that the tool must be restored, with code 0, and the agent goes on unchecked. Copilot CLI also runs the hooks of `.claude/settings.json`, without handing their failures to the agent: they exit 0 at once there (`COPILOT_CLI`).

The hooks only shorten the loop. A file written through the shell escapes the edit hooks, and an agent or a human who turns them off is stopped by the check script and CI, which fails a pull request that changes a protected file without the label `hardslice-approved`.

## Skills

Six skills, in the Agent Skills format (`SKILL.md`), are shipped. Four turn a business request into the template that generates its element, the code that completes it and the check:

| Skill | Adds | Template |
|---|---|---|
| `add-module` | a module, registered in `Program.cs` and the map of `AGENTS.md` | `hardslice-module` |
| `add-aggregate` | an aggregate with its invariants, their unit tests, its mapping and its migration, and its child entities | `hardslice-aggregate`, `hardslice-entity` |
| `add-slice` | a command or a query, its business rule in the aggregate, its registration and its tests | `hardslice-command`, `hardslice-query` |
| `add-domain-event` | a domain event raised by its aggregate, and the reaction that handles it with its tests | `hardslice-event` |

Two fix the code that a failure names. `extract-to-domain` moves a business rule that slices share, or that a slice writes into an entity, into its aggregate with its unit tests, and makes each slice call it. `fix-architecture-violation` reads the diagnostic, applies the first fix of the sheet of its rule, runs the check again, and stops to explain the conflict when only a workaround would pass.

Every HardSlice diagnostic and every architecture test failure names its skill between `Fix:` and `Doc:`:

| Skill | Named by |
|---|---|
| `add-slice` | HS004, HS013, HS014, HS015, HS018 |
| `extract-to-domain` | HS007, HS016 |
| `add-aggregate` | HS008, HS902 |
| `add-domain-event` | HS009, HS012, HS019 |
| `fix-architecture-violation` | HS000, HS001, HS003, HS005, HS006, HS010, HS011, HS017, HS900, HS901 |

Their source is `.claude/skills/`, which Claude Code and Copilot read. The template package copies them into `.agents/skills/`, which Codex and Gemini CLI read. Copilot reads both and keeps one skill per name. Each skill recalls the forbidden workarounds and never edits a protected file.

## Before a commit and in CI

The solution also runs the check before each commit and in CI, both with `scripts/check.sh` without argument:

- `.githooks/pre-commit` refuses a commit while the check fails. Nothing turns it on for you: in a clone of the solution, run `git config core.hooksPath .githooks`. `dotnet new` makes the hook executable on macOS and Linux. A clone that lost the bit, such as one of a solution generated on Windows, needs `chmod +x .githooks/pre-commit` first. It checks the working tree, changes left out of the commit included, and `git commit --no-verify` skips it.
- `.github/workflows/hardslice.yml` runs the check on GitHub Actions on every push to `main` and every pull request, with the SDK of `global.json` and the Docker of the runner, its actions pinned by commit SHA. A pull request also fails while it changes a [protected file](#protected-files), unless a maintainer adds the label `hardslice-approved` to it: this protects the rules from every agent and every human, whatever their CLI. The job pipes `git diff --name-only -z` of the merge commit into `dotnet hardslice protected`.

## Limitations

- Gemini CLI has no hooks adapter: it stays at the fallback level, without a trial possible on a personal Google account.
- Codex skips its hooks until a human approves them in `/hooks`.
- Copilot CLI ends a turn after eight blocks in a row, whatever the hook says, and cuts the errors handed after an edit at 10 KB.
- The end hook runs the full check at the end of every turn, even one that changed nothing.
- The pre-commit hook checks the working tree, not the commit alone, and is skipped by `git commit --no-verify`.
- The CI of the solution runs on GitHub Actions only, its push trigger on `main` only. Azure Pipelines is planned for v1.0. A pull request runs its own version of `.github/workflows/hardslice.yml`, and anyone who can label it can set `hardslice-approved`, which then also approves every commit pushed to it afterwards: only the protection of the branch (required checks, code owners), planned for v1.0, makes these checks binding. The local guards stop accidental drift, but they are not a security boundary.
