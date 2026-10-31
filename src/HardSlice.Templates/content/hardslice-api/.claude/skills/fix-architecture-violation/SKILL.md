---
name: fix-architecture-violation
description: Fixes a HardSlice diagnostic or a failing architecture test of this solution by reading the message, opening the sheet of its rule with dotnet hardslice explain, applying the first fix of the sheet that matches, then running the check again, and stops to explain the conflict when only a workaround would make it pass. Use when the build or the check fails with a HardSlice rule, HS000 to HS902, or when a message ends with Skill fix-architecture-violation, such as a domain type that uses EF Core, an endpoint that reads the database or a query handler that writes.
---

# Fix an architecture violation

A HardSlice failure names everything needed to fix it, in one line: the rule and the offending symbol with where it is, the rule in one sentence, then `Fix:` (the expected fix), `Skill:` (the skill to use) and `Doc:` (the sheet of the rule, in `docs/rules/`). The analyzers report it at build time, the architecture tests when the tests run; both fail the check.

Run every command from the root of the solution, the folder of `MyApp.slnx`.

## Steps

1. Read the message you were given, or run the check and read its output. Take the failures one rule at a time, the first one first: an architecture test lists one violation per line, a build diagnostic gives the file and the line of the offending code.
2. Read the skill after `Skill:`. When it names another skill, use that skill instead: `add-slice` for the shape, the validator, the tests and the endpoint of a slice, `extract-to-domain` for a business rule outside the domain, `add-aggregate` for an aggregate and its references, `add-domain-event` for an event and the reactions between modules. Keep this skill for the other rules.
3. Open the sheet of the rule, and read the golden rule of `AGENTS.md` that names it. Read in the sheet its rule, how to fix it, its forbidden workarounds and what it does not detect.

   ```text
   dotnet tool restore
   dotnet hardslice explain <rule>
   ```

4. Find why the code needed what the rule forbids, then apply the first fix of "How to fix" that matches that reason, to the code the message names. Move code to where the sheet says it belongs, and move its tests with it, at the same path under `tests/`. Keep the behavior: the tests that covered the code still pass, with their assertions unchanged.
   - HS000: remove the suppression and fix the code that the suppressed rule reports, with the skill of that rule. Only a maintainer writes a justification: never write one yourself.
   - HS900, or a message that starts with the name of the configuration file: the configuration or one of its contracts is missing or invalid. Restore it as the sheet says, never adapt it to the code. If the code really needs another configuration, stop: that is a decision of a maintainer.
   - HS901: give the type one role, and split it in two types when the code needs both.
5. Run the check again, and read the whole output: a fix may reveal another rule, such as code moved into a slice that now needs its tests. Go back to step 1 until the check exits 0.

   ```text
   bash scripts/check.sh     # macOS, Linux, Git Bash
   pwsh scripts/check.ps1    # Windows, PowerShell
   ```

6. Stop and explain the conflict instead of handing back a failing check, when every fix of the sheet needs a forbidden workaround, breaks another rule, or changes a behavior that a test describes: name the rule, the offending code, the fixes you tried and why each one fails.

## Never

These are the workarounds of [Forbidden](../../../AGENTS.md#forbidden); if the check cannot pass without one, stop and explain the conflict instead.

- Suppress a HardSlice diagnostic, with `#pragma warning disable`, `[SuppressMessage]` or `<NoWarn>`, lower its severity or turn the analyzers off.
- Write a `// hardslice-justification:` comment yourself: an exception is a decision of a maintainer.
- Delete, skip or weaken a test, or leave an empty validator, an empty test or a placeholder file, to satisfy a rule.
- Use any workaround that the sheet of the rule forbids, such as reflection, `dynamic`, a public handler or moving a type of a slice into the namespace of its module.
- Adapt the configuration or the contracts to the code.
- Edit a protected file: `.editorconfig`, `hardslice.json`, `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props`, `.config/dotnet-tools.json`, `scripts/`, `tests/MyApp.ArchitectureTests/`, `.claude/` and `.agents/`.
