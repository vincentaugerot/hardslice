# Add-feature demonstration

PB-050 demonstration: from a solution generated with `dotnet new hardslice-api --no-sample`, an agent receives one business request, a module with its aggregates, a command, a query and a domain event with its reaction, and delivers it through the skills and the item templates of the solution, without recreating their structure by hand.

```text
business request, which names neither a skill nor a template
→ the agent reads AGENTS.md and loads the skills
→ dotnet new hardslice-module, -aggregate, -command, -query and -event
→ logic and tests in place of every TODO(hardslice): marker
→ trusted check green, no protected file changed
```

This is a demonstration, not a statistical benchmark: no repetitions and no A/B comparison. The benchmark is planned for v0.4.

## The request

[`prompt.md`](prompt.md), given to the agent as is. It describes the catalog of a shop: a product with its name and its price, a use case that registers it, one that reads it, and a stock opened at zero when a product is registered, by reaction rather than in the use case. It names the elements, so that their paths are known, but never a skill, a template or a rule.

| Element | Name | Template |
|---|---|---|
| Module | `Catalog` | `hardslice-module` |
| Aggregates | `Product`, `Stock` | `hardslice-aggregate` |
| Command | `RegisterProduct` | `hardslice-command` |
| Query | `GetProduct` | `hardslice-query` |
| Domain event and reaction | `ProductRegistered`, `Catalog/OpenStockOnProductRegistered` | `hardslice-event --reaction` |

## What verify checks

`verify` never runs anything from the agent's folder. It regenerates a trusted `Shop`, validates the candidate against it, copies only the accepted changes into a clean tree, and fails unless:

- the candidate changes nothing but `AGENTS.md`, the C# files of `src/`, `tests/Shop.Tests/` and `tests/Shop.IntegrationTests/`, and the `.gitkeep` files of the module template: no project file, and no protected file as the trusted `dotnet hardslice protected` reads them;
- no changed C# file holds a preprocessor directive (except those `dotnet ef` writes in a migration: `#nullable disable` and the `#pragma warning` of the obsolete warnings CS0612 and CS0618), a suppression, a `hardslice-justification` comment, reflection, `dynamic`, a skipped or always-true test, a validator without any rule or a test file without any assertion, as in [the fix demonstrations](../agent-fix/README.md);
- every element is at the path where its template generates it: the module, both aggregates with their identifier, their mapping and their unit tests, both slices and their tests, the event, the reaction and its test, and the model snapshot of the migrations;
- the module is registered in `Program.cs`, both endpoints and the reaction in `CatalogModule.cs`, `Product` raises `ProductRegistered`, the reaction handles it, the model snapshot maps `Product` and `Stock`, and the map of `AGENTS.md` names the module;
- no `TODO(hardslice):` marker is left in `src/` or `tests/`;
- the trusted `scripts/check.sh` passes: build without HardSlice diagnostic, unit, architecture and integration tests green;
- the trace of the agent runs `dotnet new hardslice-module --name Catalog`, `hardslice-aggregate --name Product` and `--name Stock`, `hardslice-command --name RegisterProduct`, `hardslice-query --name GetProduct`, `hardslice-event --name ProductRegistered` and a `hardslice-event` with `--reaction Catalog/OpenStockOnProductRegistered`, whatever the order of their options.

[`scripts/agent-trace.py`](../../scripts/agent-trace.py) reads the shell commands and the skills of a trace of Claude Code (`claude -p --output-format stream-json --verbose`), Codex (`codex exec --json`) or GitHub Copilot CLI (`copilot -p --output-format json`). The skills loaded through a skill tool are printed, not required: Codex reads a skill as a file, which the trace only shows as a command.

The verifier checks the files, their registrations and the trace mechanically: whether a business rule sits in its aggregate rather than in a handler is judged by reading the diff, as the accounts of the runs do.

## Running it

Prerequisites: the .NET SDK pinned by the repository's `global.json`, git, Python 3, Perl and a Docker-compatible container runtime.

```text
scripts/demo-add-feature.sh prepare <dir>
```

Generates `Shop` with `--no-sample` in `<dir>` with the generator of `scripts/test-template.sh`, installs the templates in the private home of the dotnet CLI, `<dir>/dotnet-cli`, where the agent's plain `dotnet new` finds them, proves the solution green with its `scripts/check.sh`, integration tests included, and commits it for the agent's local diff workflow.

Run the agent from `<dir>/Shop` with the content of `prompt.md`, with `DOTNET_CLI_HOME=<dir>/dotnet-cli`, and keep its JSONL output as the trace. Then:

```text
scripts/demo-add-feature.sh verify <dir> <trace>
scripts/demo-add-feature.sh record <dir> <trace> <run>
scripts/demo-add-feature.sh verify-recorded <dir> [run...]
```

`record` keeps the changes of the candidate as an overlay in `runs/<run>/changes/` and the trace in `runs/<run>/trace.jsonl`, without the streaming events of Copilot, and with the work folder and the home folder replaced by `/home/dev/work` and `/home/dev`. `verify-recorded` generates `Shop` once, lays the changes of each recorded run over a copy, runs the whole of `verify` on it with its trace, and fails unless each run reaches the verdict written in its `verdict` file, `pass` or `fail`.

## Running an agent

Each run below started from its own `prepare`, in its native mode: the hooks and skills of the generated solution, the project settings only, no MCP server of the user. `SIDEHUB_*` variables were unset, `DOTNET_CLI_HOME` set as `prepare` prints, and `MSBUILDDISABLENODEREUSE=1` exported, as for the hook trials of [`docs/agents/support.md`](../../docs/agents/support.md).

### Claude Code

```text
claude -p "$(cat <repo>/demos/add-feature/prompt.md)" --model <model> \
  --setting-sources project --strict-mcp-config --permission-mode acceptEdits \
  --allowedTools Read Edit Write Glob Grep Skill "Bash(dotnet *)" "Bash(bash scripts/check.sh*)" "Bash(git status*)" "Bash(git diff*)" "Bash(ls *)" "Bash(grep *)" "Bash(find *)" "Bash(cat *)" \
  --output-format stream-json --verbose > <trace>
```

The output of the hooks is not in the stream: it is in the transcript of the session, `~/.claude/projects/<cwd with / as ->/<session_id>.jsonl`, as `hook_blocking_error` attachments after an edit and a `hook_success` of the `Stop` hook.

### Codex

The hooks of `.codex/hooks.json` run only once approved: launch `codex` once in `<dir>/Shop`, trust the folder, open `/hooks` and trust each of the three hooks. The approval lands in `~/.codex/config.toml`: back it up before and restore it after.

```text
codex exec --json -s workspace-write -c allow_login_shell=false -c sandbox_workspace_write.network_access=true \
  "$(cat <repo>/demos/add-feature/prompt.md)" > <trace>
```

`allow_login_shell=false` keeps the `PATH` of the caller in the commands of the agent: on the machine of the runs, a login shell puts an older .NET SDK first. `network_access=true` lets the integration tests reach the Docker socket from the sandbox.

### GitHub Copilot CLI

```text
copilot -p "$(cat <repo>/demos/add-feature/prompt.md)" --model claude-sonnet-4.6 --output-format json \
  --allow-tool write --allow-tool 'shell(dotnet:*)' --allow-tool 'shell(bash scripts/check.sh)' \
  --allow-tool 'shell(ls:*)' --allow-tool 'shell(cat:*)' --allow-tool 'shell(grep:*)' --allow-tool 'shell(find:*)' --allow-tool 'shell(git status:*)' --allow-tool 'shell(git diff:*)' \
  --deny-tool url --trust-folder --no-ask-user --max-ai-credits 300 > <trace>
```

### Gemini CLI

Fallback mode: no hook, `GEMINI.md` imports `AGENTS.md`, which asks for `scripts/check.sh`. Gemini CLI no longer serves personal Google accounts, only a Gemini API key, Vertex AI or a Gemini Code Assist license for companies (`docs/agents/support.md`, [S15]): without one, the run stops at authentication.

```text
GEMINI_CLI_TRUST_WORKSPACE=true gemini --approval-mode auto_edit --allowed-tools "run_shell_command(dotnet)" --allowed-tools "run_shell_command(bash scripts/check.sh)" \
  --output-format stream-json -p "$(cat <repo>/demos/add-feature/prompt.md)" > <trace>
```

## Runs

Five runs on 01/11/2026, each from its own `prepare`, with the commands above. The four that ran pass `verify`, on their workspace then from their recording with `verify-recorded`; Gemini CLI could not run.

| Run | Agent, model | Mode | Run | Skills loaded | Hooks | Verdict |
|---|---|---|---|---|---|---|
| [claude-sonnet-1](runs/claude-sonnet-1/trace.md) | Claude Code 2.1.316, `claude-sonnet-5-5` | native | 45 turns, 3 min 22 s, $0.53 | the four, at once | 14 `PostToolUse` blocks, `Stop` passed in 26 s | `pass` |
| [claude-opus-2](runs/claude-opus-2/trace.md) | Claude Code 2.1.316, `claude-opus-5-5` | native | 81 turns, 6 min 26 s, $1.51 | the four, each at its step | 20 `PostToolUse` blocks, `Stop` passed in 18 s | `pass` |
| [codex](runs/codex/trace.md) | Codex CLI 0.165.0, `gpt-6-astra` | native, hooks approved in `/hooks` | 6 min 37 s, 1.57 M tokens | the four, read with `cat` | not reported by `codex exec --json`; `Stop` seen through its effects | `pass` |
| [copilot](runs/copilot/trace.md) | Copilot CLI 1.0.105, `claude-sonnet-4.6` | native | 5 min 26 s, 9 premium requests | `add-module` only | 11 `postToolUse` answers, `agentStop` silent | `pass` |
| [gemini](runs/gemini/trace.md) | Gemini CLI 0.64.0 | fallback | stopped at authentication (`UNSUPPORTED_CLIENT`) | | | not run |

Every agent that ran generated the seven elements with their templates, in the order of the skills or of `AGENTS.md`, and none wrote the structure of an element by hand nor touched a protected file. Every `PostToolUse` block was an intermediate state of the agent's own edits, as the generated files are filled one after the other; none was a HardSlice rule left broken. All of them put the rules of the request in `Product` and `Stock` and opened the stock in the reaction, through `AppDbContext`, never in `RegisterProduct`. They differ where the request leaves room: the route and the status of `RegisterProduct` (the 200 of the template, or 201 on `/catalog/products`), a value object for the price (Codex), constraints of the database, and a rule of two decimals that Opus added and pointed out.

Each run of Claude Code was refused a few `Bash` commands by its allowed tools, a Python edit or a chain with a redirection, and redid them with `Edit`, `Write` or a plain command; Copilot was refused a check with a redirection and ran `dotnet hardslice check` instead.
