# Security policy

## Supported versions

HardSlice is in preview: only the latest preview published on NuGet.org receives fixes. A fix ships in a new preview. A published version is never replaced.

## Reporting a vulnerability

Report it in private with [Report a vulnerability](https://github.com/vincentaugerot/hardslice/security/advisories/new) on GitHub, not in a public issue. Say which package and version, what you did and what happened.

What counts here includes a way for an agent to get around the protections of a generated solution without being refused or caught: editing a protected file past the hooks and the generated CI, turning a rule off without HS000, or making `dotnet hardslice check` pass on a solution that breaks a rule.

## What the protections are not

The hooks, the pre-commit hook and the generated CI stop accidental drift by coding agents, but they are not a security boundary. An agent can write a file through the shell, and a human can turn the hooks off. Only a protected branch, with the check of `.github/workflows/hardslice.yml` required and the label `hardslice-approved` restricted to maintainers, makes them binding. A known gap of that kind is documented in the [agent loop](docs/agent-loop.md#limitations) rather than reported.
