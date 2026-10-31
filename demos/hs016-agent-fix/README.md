# HS016 agent fix

PB-008 demonstration: an agent receives a simple development prompt on a solution that fails with HS016, reads the diagnostic, fixes the architecture and brings `scripts/check.sh` back to green without bypassing any rule, configuration or test.

```text
violation HS016
→ diagnostic read by the agent
→ architectural fix
→ scripts/check.sh green
```

This is a demonstration, not a statistical benchmark: one run does not prove a general improvement.

The fixture applies to the v0.1 `hardslice-api` template, with the Bookings sample: `Booking` persisted in PostgreSQL, and the `ConfirmBooking`, `CancelBooking` and `GetBooking` slices. The CI prepares it from a fresh checkout on every push, so it keeps producing its HS016 on the current template.

## Contents

| Path | Role |
|---|---|
| `fixture/` | Files laid over a freshly generated `Demo` solution. They add a business rule (a booking that has started can no longer be confirmed or cancelled) whose check lives in the `ConfirmBooking` slice and is reused by `CancelBooking`: a deliberate HS016 violation. The integration tests describing the rule are included, with the fixed clock they use. |
| `prompt.md` | The development prompt given to the agent, as is. It does not mention HS016 or how to fix it. |
| `trace.md` | Concise trace of a run recorded on the increment 1 template, before slices used PostgreSQL. On the v0.1 template, the handlers use `AppDbContext`, the rule's tests are integration tests and `scripts/check.sh` also runs them: a new run differs in these details, not in the loop. Its diagnostic also predates `Skill:`: the message now ends with `Skill: extract-to-domain.` before `Doc:`. |

## Running it

Prerequisites: the .NET SDK pinned by the repository's `global.json`, and a Docker-compatible container runtime.

```text
scripts/demo-hs016-agent-fix.sh prepare <dir>
```

Generates `Demo` in `<dir>` with the same generator as `scripts/test-template.sh`. Like `verify`, it runs the generated `scripts/check.sh`, whose integration tests need a Docker-compatible container runtime. It first proves the generated application is green, then applies and commits the fixture for the agent's local diff workflow. Preparation fails unless the fixture produces the expected `CancelBookingHandler` to `BookingSchedule` HS016 diagnostic.

Then run any agent from `<dir>/Demo` with the content of `prompt.md`, with `DOTNET_CLI_HOME` set to `<dir>/dotnet-cli` like `prepare`: `scripts/check.sh` restores `dotnet hardslice` from the private package folder of `<dir>`, and the .NET SDK never updates the path of a tool version it already resolved, so a shared resolver cache would keep pointing to that folder once `<dir>` is deleted. The recorded run used Claude Code in headless mode, without user settings, plugins or MCP servers, so it relied only on what the generated solution provides:

```text
cd <dir>/Demo
export DOTNET_CLI_HOME=<dir>/dotnet-cli
claude -p "$(cat <repo>/demos/hs016-agent-fix/prompt.md)" \
  --setting-sources project --strict-mcp-config --no-session-persistence \
  --permission-mode acceptEdits \
  --allowedTools "Bash(bash scripts/check.sh)" "Bash(dotnet:*)" "Bash(git status:*)" "Bash(git diff:*)" "Bash(ls:*)" Read Edit Write Glob Grep \
  --output-format stream-json --verbose > <dir>/agent-run.jsonl
```

Finally:

```text
scripts/demo-hs016-agent-fix.sh verify <dir>
```

Fails unless all of the following hold:

- a freshly regenerated fixture still produces the exact expected HS016 diagnostic;
- the candidate differs from that clean fixture only through regular C# files under `src/`, `tests/Demo.Tests/` and `tests/Demo.IntegrationTests/`;
- every fixture test keeps its original path, test attributes, method declarations and assertion lines;
- changed sources contain none of the suppression, indirect-reference or skipped-test constructs defined by the verifier;
- the accepted C# changes pass the trusted regenerated copy of `scripts/check.sh`: build without HS016, unit, architecture and integration tests green.

`verify` never invokes scripts, projects, packages or Git metadata from the candidate workspace. It validates the candidate first, copies only accepted C# changes into a clean tree, and only then builds and tests that code with the trusted check script. Candidate Git tags, commits, configuration, hooks and ignored files have no authority over the verdict.

The verifier checks bypasses mechanically. Whether the fix is the right architectural one (the rule moved into the `Booking` aggregate rather than copied into each slice) is still judged by reading the diff, as `trace.md` does.
