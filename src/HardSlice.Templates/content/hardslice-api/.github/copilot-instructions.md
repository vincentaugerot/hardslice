# Copilot instructions

For GitHub Copilot only. Copilot already reads `AGENTS.md`, which holds the rules of every agent: this file adds what is specific to Copilot. Skip the section of `GEMINI.md` meant for Gemini CLI.

- Hooks: none yet. Nothing checks your edits as you make them: before you finish, run the check script as [Verify](../AGENTS.md#verify) says.
- Skills: Copilot reads the skills of `.claude/skills/`, `.agents/skills/` and `.github/skills/`. Each skill of HardSlice is in the first two, identical, and Copilot keeps it once: `add-module`, `add-aggregate`, `add-slice` and `add-domain-event`, listed in the [Agent kit](../AGENTS.md#agent-kit).
