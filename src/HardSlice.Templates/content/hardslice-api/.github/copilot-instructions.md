# Copilot instructions

For GitHub Copilot only. Copilot already reads `AGENTS.md`, which holds the rules of every agent: this file adds what is specific to Copilot. Skip the section of `GEMINI.md` meant for Gemini CLI.

- Hooks: none yet. Copilot also runs the hooks of `.claude/settings.json`, which are for Claude Code and do nothing under Copilot. Nothing checks your edits as you make them: before you finish, run the check script as [Verify](../AGENTS.md#verify) says.
- Skills: Copilot reads the skills of `.claude/skills/`, `.agents/skills/` and `.github/skills/`. Each skill of HardSlice is in the first two, identical, and Copilot keeps it once: `add-module`, `add-aggregate`, `add-slice`, `add-domain-event`, `extract-to-domain` and `fix-architecture-violation`, listed in the [Agent kit](../AGENTS.md#agent-kit).
