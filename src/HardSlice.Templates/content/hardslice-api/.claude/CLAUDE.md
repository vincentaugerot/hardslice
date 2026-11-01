# CLAUDE.md

For Claude Code only. `AGENTS.md`, imported below, holds the rules of every agent; the section after it applies to Claude Code alone.

@../AGENTS.md

## Claude Code

- Hooks: `.claude/settings.json` runs `dotnet hardslice hook claude`, the local tool that the check script restores (or `dotnet tool restore`). Each hook hands you its failure, act on it:
  - before an edit or a write, it refuses a protected file, listed in [Forbidden](../AGENTS.md#forbidden), with the reason. Never work around the refusal: stop and explain the conflict;
  - after the edit of a `.cs` file, the errors of `scripts/check.sh --build`, the incremental build alone, come back to you, within 10 seconds on a large solution. Fix them before going on;
  - before you hand back, the full check runs, within 60 seconds, and keeps you working on its failure while it fails. After three relaunches for the same failure, it lets you finish: then tell the user that the check fails, with its failure.
- A file written through the shell escapes the edit hooks, and the hooks never decide that the work is done: run the check script as [Verify](../AGENTS.md#verify) says.
- Skills: Claude Code reads the skills of `.claude/skills/`: `add-module`, `add-aggregate`, `add-slice`, `add-domain-event`, `extract-to-domain` and `fix-architecture-violation`, listed in the [Agent kit](../AGENTS.md#agent-kit).
