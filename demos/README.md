# Demonstrations

Two demonstrations show what an agent does with a HardSlice solution. Each has a script that prepares the solution an agent starts from and a verifier that never runs anything from the agent's folder: it validates the changes, copies the accepted ones into a clean tree and runs the trusted `scripts/check.sh` there.

| Demonstration | What it proves | Checked by |
|---|---|---|
| [Agent fix](agent-fix/README.md) | For each blocking rule, a solution that breaks it fails with exactly the diagnostic of the rule, the reference correction passes the verifier, and every known workaround (a suppression, a severity lowered, a protected file or test changed) is rejected. Claude Code also fixed four of them from the development prompt, guided by the diagnostic and its rule sheet. | `scripts/demo-agent-fix.sh verify-reference`, in the `demo` job of the CI |
| [Add feature](add-feature/README.md) | From a solution generated with `--no-sample`, Claude Code, Codex and GitHub Copilot CLI each delivered one business request (a module, two aggregates, a command, a query, a domain event and its reaction) through the skills and the item templates, without touching a protected file. | `scripts/demo-add-feature.sh verify-recorded`, on the recorded changes and traces, in the same job |

Neither is a benchmark: each agent ran once, without repetition or comparison, on one machine, and the README of each demonstration says with which versions, models and settings. A benchmark with isolated runs is planned for v0.4.

The recorded traces are the raw output of each CLI, without its local inventory and its encrypted reasoning, as [`scripts/agent-trace.py`](../scripts/agent-trace.py) `filter` prints them; the accounts next to them (`trace.md`) say what each agent did.
