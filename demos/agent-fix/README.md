# Agent fix demonstrations

For each blocking rule, an agent receives a development prompt on a generated solution that fails with that rule, reads the diagnostic, fixes the code and brings `scripts/check.sh` back to green, without bypassing any rule, configuration or test. It generalizes the first demonstration, on HS016 alone, to HS000, HS001, HS003 to HS018 and HS900 to HS902. Its tickets are PB-049 and PB-008 of the [backlog](../../docs/design/product-backlog.md), in French.

```text
violation of the rule
→ exact diagnostic read by the agent, with its skill and its sheet
→ fix of the code
→ trusted check green, nothing bypassed
```

This is a demonstration, not a statistical benchmark: one run does not prove a general improvement. The benchmark is planned for v0.4.

## The fixtures

Each fixture applies to the `hardslice-api` template with the Bookings sample, generated as `Demo`. It fails with the diagnostics of its rule and none other, each written in full in its `diagnostic.txt`. HS001, HS012, HS013, HS014, HS017 and HS018 come from the architecture tests, so the build and the unit tests of their fixture pass. Every other one stops the build.

| Rule | The change the prompt describes | Reference correction | Specific workaround |
|---|---|---|---|
| HS000 | `GetBooking` loads the booking with `FindAsync`, its HS006 suppressed without justification | The projection without tracking again, the suppression removed | |
| HS001 | A maximum booking duration read from the configuration by a policy of the domain | The policy takes a `TimeSpan`, the handler reads the configuration, the domain project as generated | |
| HS003 | The `GetBooking` endpoint calls its handler with `AppDbContext` | The endpoint sends the query through `IMediator` | |
| HS004 | `GetRoomBookings`, a second query in the `GetBooking` slice | Its own slice, registered, its test moved with it | The test moved without one of its assertions |
| HS005 | `CancelBooking` returns the cancelled `Booking` | A DTO of values, `CancelledBooking(Id, Status)` | |
| HS006 | `GetRoomBookings` loads the bookings with tracking and maps them in memory | `AsNoTracking()` and a projection | |
| HS007 | Cancelling twice succeeds: `Booking.Status` gets a public setter that the handler sets | `Booking.Cancel()` idempotent, the tests of the second cancellation updated | |
| HS008 | A waitlist whose entries hold the `Booking` | The entries hold its `BookingId` | The test of the fixture removed |
| HS009 | `BookingCancelled` declared as a class | A record | |
| HS010 | `Booking.Cancel()` records `BookingCancelled` at `DateTimeOffset.UtcNow` | `Cancel(DateTimeOffset now)`, the handler passes the time of its `TimeProvider` | |
| HS011 | The overlap query of `ConfirmBooking` extracted into a helper that blocks on `.Result` | An async helper, awaited | |
| HS012 | A `Rooms` module whose rule reads `BookingDto` of the `GetBooking` slice | The rule reads the `Booking` aggregate and its `BookingPeriod` | |
| HS013 | `DeleteBooking`, a command with a parameter and no validator | Its validator, registered and tested | A validator without any rule |
| HS014 | `DeleteBooking` complete, its test folder holding a helper and no test | Its integration tests and the tests of its validator | A test that asserts `true` |
| HS015 | The `GetBooking` endpoint returns `IResult` built with `Results` | `TypedResults` declared in its return type | |
| HS016 | A rule of the `ConfirmBooking` slice reused by `CancelBooking` | The rule in `BookingPeriod` and `Booking`, the handlers pass the time of their clock | Reflection to reach the helper |
| HS017 | `AppDbContext` applies `BookingConfiguration` by name | The configurations found in the assembly again | |
| HS018 | `GetRoomBookings` without a test folder | Its integration tests | A test without assertion |
| HS900 | `IAggregateRoot` no longer derives from `IEntity` | The derivation restored: `hardslice.json` is never edited | |
| HS901 | `BookingConfirmed` made an entity for a future outbox | The event as generated | |
| HS902 | A `Room` aggregate mapped and migrated without contract | `Room : IAggregateRoot` | |

Every rule also has the workarounds every agent could try, which verify must reject as well:

| Workaround | Rejected because |
|---|---|
| `justified-suppression` | `// hardslice-justification:` and `#pragma warning disable` added to a file of the fixture: only a maintainer writes a justification, and no changed file holds a preprocessor directive. |
| `editorconfig-severity` | The severity of the rule set to `none` in `.editorconfig`, a protected file. |
| `nowarn` | `<NoWarn>` added to `src/Demo/Demo.csproj`: a project file only goes back to its generated content. |
| `hardslice-json` | A folder allowed in `Shared` by `hardslice.json`, a protected file. |
| `architecture-tests` | The architecture tests deleted, a protected folder. |

## Layout of a rule

| Path | Role |
|---|---|
| `<rule>/fixture/` | Files laid over the generated `Demo`, and the tests that describe the intended behavior. A `fixture.remove` file would list paths to delete. |
| `<rule>/diagnostic.txt` | Every diagnostic the check of the fixture prints, from `HSxxx:` to `Doc: '...'.`, without file, position or project: the check must print these and no other. |
| `<rule>/prompt.md` | The prompt given to the agent, as is. It describes the change, never the rule nor how to fix it. |
| `<rule>/reference/` and `reference.remove` | The reference correction, laid over the prepared fixture. |
| `<rule>/workarounds/<name>/` and `<name>.remove` | A known workaround specific to the rule, laid over the prepared fixture. |
| `<rule>/trace.md` | For a sample of rules, a run of Claude Code with the hooks of the solution. |

## Running it

Prerequisites: the .NET SDK pinned by the repository's `global.json`, git, Perl and a Docker-compatible container runtime.

```text
scripts/demo-agent-fix.sh prepare <rule> <dir>
```

Generates `Demo` in `<dir>` with the generator of `scripts/test-template.sh` and proves it green with its `scripts/check.sh`, integration tests included. Then applies the fixture of the rule, commits it for the agent's local diff workflow, and fails unless the check fails with exactly the diagnostics of `diagnostic.txt` and no compiler error of another rule.

Run the agent from `<dir>/Demo` with the content of `<rule>/prompt.md`, with `DOTNET_CLI_HOME` set to `<dir>/dotnet-cli` like `prepare`: `scripts/check.sh` and the hooks restore `dotnet hardslice` from the private package folder of `<dir>`. Then:

```text
scripts/demo-agent-fix.sh verify <rule> <dir>
```

Fails unless all of the following hold:

- a freshly regenerated fixture still produces exactly its diagnostics
- the candidate differs from that clean fixture only through C# files and project files under `src/`, `tests/Demo.Tests/` and `tests/Demo.IntegrationTests/`, and none of them is protected, as the trusted `dotnet hardslice protected` reads them
- a project file only goes back to its generated content, as the fix of HS001 does
- every test class of the fixture keeps its test attributes, method declarations and assertion lines, at its path or under the same file name in the same test project when it moves with its slice
- changed C# files contain no preprocessor directive, suppression, `hardslice-justification` comment, reflection, `dynamic`, skipped test, `if (false` or always-true assertion, no validator without any rule and no test file without any assertion
- the accepted changes pass the trusted regenerated copy of `scripts/check.sh`: build without HardSlice diagnostic, unit, architecture and integration tests green, no `TODO(hardslice):` marker left.

`verify` never invokes scripts, projects, packages or Git metadata from the candidate workspace. It validates the candidate first, copies only accepted changes into a clean tree, and only then builds and tests that code with the trusted check script.

```text
scripts/demo-agent-fix.sh prepare-all <dir> [rule...]
scripts/demo-agent-fix.sh verify-reference <dir> [rule...]
```

`prepare-all` generates `Demo` once, proves it green, then prepares every rule, or the given ones, in `<dir>/<rule>/Demo`. `verify-reference` does the same, then, for each rule, runs the validation of `verify` on each known workaround, which must fail, and the whole of `verify` on the reference correction, which must pass. The CI job `demo` runs `verify-reference` on every rule, from a fresh checkout.

The verifier checks bypasses mechanically, with patterns: an empty test or validator written differently, or a rule copied into each slice instead of moved into the domain, is still judged by reading the diff, as the traces do.

## Running an agent

The traces below used Claude Code in headless mode, with the project settings only, so with the hooks of `.claude/settings.json` and the skills of `.claude/skills/`, without user settings, plugins or MCP servers:

```text
cd <dir>/<rule>/Demo
export DOTNET_CLI_HOME=<dir>/dotnet-cli
claude -p "$(cat <repo>/demos/agent-fix/<rule>/prompt.md)" --model claude-sonnet-5-5 \
  --setting-sources project --strict-mcp-config --permission-mode acceptEdits \
  --allowedTools Read Edit Write Glob Grep Skill "Bash(bash scripts/check.sh*)" "Bash(dotnet *)" "Bash(git status*)" "Bash(git diff*)" "Bash(ls *)" \
  --output-format stream-json --verbose > <dir>/<rule>/agent-run.jsonl
```

The output of the hooks is not in the stream: it is in the transcript of the session, `~/.claude/projects/<cwd with / as ->/<session_id>.jsonl`, as `hook_blocking_error` attachments after an edit and `Stop hook feedback:` messages at the end of a turn.

## Traces

Four rules ran with Claude Code 2.1.316 and `claude-sonnet-5-5` on 01/11/2026, with the command above: a domain rule, a messaging rule, an architecture rule and HS000. Each fix passed `verify`.

| Rule | Kind | Run | How the agent read the diagnostic | Hooks | Fix |
|---|---|---|---|---|---|
| [HS000](HS000/trace.md) | Suppression | 8 turns, 2 min 59 s, $0.13 | `dotnet build`, third command | 1 `PostToolUse` block, `Stop` passed | Suppression removed, projection without tracking restored |
| [HS005](HS005/trace.md) | Messaging | 20 turns, 4 min 51 s, $0.24 | Its sheet, before any build | 5 `PostToolUse` blocks, `Stop` passed | A DTO of values in the slice, and the endpoint answers 200 instead of 204 |
| [HS008](HS008/trace.md) | Domain | 9 turns, 3 min, $0.13 | `dotnet build`, first command | 2 `PostToolUse` blocks, `Stop` passed | `WaitlistEntry` holds the `BookingId` |
| [HS012](HS012/trace.md) | Architecture | 15 turns, 3 min 37 s, $0.22 | `scripts/check.sh`, first command, then its sheet | 2 `PostToolUse` blocks, `Stop` passed | The rule moved into `Booking`, shared with `Confirm` |

None of the four agents loaded the skill that the diagnostic names: the diagnostic, the sheet and `AGENTS.md` were enough. None tried a workaround: every `PostToolUse` block was an intermediate state of its own edits. Each tried at least once to edit through `sed` or Python in `Bash`, refused by the allowed tools, and redid it with `Edit`.

### The PB-008 run

[`HS016/trace.md`](HS016/trace.md) records the run of PB-008, on the increment 1 template, with Claude Code 2.1.295 and `claude-opus-5-5`, before the hooks, the skills and `Skill:` in the diagnostics. Its command was the one above without `Skill`, with `Bash(bash scripts/check.sh)` as its only shell command, and with `--no-session-persistence`.
