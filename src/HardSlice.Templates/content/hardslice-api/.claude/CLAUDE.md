# CLAUDE.md

For Claude Code only. `AGENTS.md`, imported below, holds the rules of every agent; the section after it applies to Claude Code alone.

@../AGENTS.md

## Claude Code

- Hooks: none yet. Nothing checks your edits as you make them: before you finish, run the check script as [Verify](../AGENTS.md#verify) says.
- Skills: Claude Code reads the skills of `.claude/skills/`: `add-module`, `add-aggregate`, `add-slice`, `add-domain-event`, `extract-to-domain` and `fix-architecture-violation`, listed in the [Agent kit](../AGENTS.md#agent-kit).
