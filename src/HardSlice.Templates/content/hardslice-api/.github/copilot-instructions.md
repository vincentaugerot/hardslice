# Copilot instructions

For GitHub Copilot only. Copilot already reads `AGENTS.md`, which holds the rules of every agent: this file adds what is specific to Copilot. Skip the section of `GEMINI.md` meant for Gemini CLI.

- Hooks: `.github/hooks/hardslice.json` runs `dotnet hardslice hook copilot`, the local tool that the check script restores (or `dotnet tool restore`). Until it is restored, the hooks do nothing. Each hook hands you its failure, act on it:
  - before an edit or a file creation, it denies a protected file, listed in [Forbidden](../AGENTS.md#forbidden), with the reason. Never work around the denial: stop and explain the conflict;
  - after the edit of a `.cs` file, the errors of `scripts/check.sh --build`, the incremental build alone, are added to the result of the tool. Fix them before going on. Once it builds, it names the `TODO(hardslice):` markers left: finish them before you hand back;
  - before you hand back, the full check runs and keeps you working on its failure while it fails. After three relaunches for the same failure, or eight in a session, it lets you finish: then tell the user that the check fails, with its failure.
- Copilot also runs the hooks of `.claude/settings.json`, which are for Claude Code and do nothing under Copilot.
- A file written through the shell escapes the edit hooks, and the hooks never decide that the work is done: run the check script as [Verify](../AGENTS.md#verify) says.
- Skills: Copilot reads those of `.claude/skills/`, `.agents/skills/` and `.github/skills/`. Each skill of HardSlice is in the first two, identical, and Copilot keeps it once: the [Agent kit](../AGENTS.md#agent-kit) lists them.
