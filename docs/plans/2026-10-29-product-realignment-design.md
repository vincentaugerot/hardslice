# Product realignment design

Status: approved by the maintainer on 2026-10-29 through the instruction to proceed with the proposed realignment.

## Context

`docs/specifications.md` describes the product the maintainer intends to build: a complete .NET architecture kit for coding agents, covering greenfield templates, executable architecture rules, agent guidance and loops, adoption on existing solutions, and public benchmarks.

The evidence-first plan narrowed that product into an analysis and adoption engine, made several original deliverables conditional, and led to a phase-0 backlog dominated by research governance. This was a coherent risk-reduction strategy, but it changed the product instead of merely sequencing its delivery.

No product implementation has started. The repository contains no solution, project, or C# source file, so the correction is documentary and does not require discarding product code.

## Considered approaches

### 1. Status-banner patch only

Restore the specification as authoritative and leave the current plan and backlog otherwise unchanged.

This is the smallest edit, but it leaves contradictory active documents and would likely restart the same review loop.

### 2. Surgical realignment — selected

Restore the specification as the product contract, replace the active plan with a short product roadmap, preserve the evidence-first plan as an historical technical reference, mark the old phase-0 execution documents inactive, and create a concise product backlog whose first milestone produces working .NET code.

This preserves useful work without allowing it to govern the product or block implementation.

### 3. Full documentation rewrite

Rewrite the specification, plan, backlog, research notes, spike protocols, and decision log into one new document set.

This could produce a cleaner corpus eventually, but it would spend more time on documentation before the first product increment and risks losing useful technical detail.

## Document hierarchy

The active hierarchy after the realignment is:

1. `docs/specifications.md` — approved product contract and scope.
2. `docs/plan.md` — delivery roadmap subordinate to the specification.
3. `docs/product-backlog.md` — active, code-oriented increments and acceptance criteria.
4. Technical notes, spike protocols, research material, and historical execution documents — supporting evidence that cannot remove or condition product scope by themselves.

A roadmap item may be phased, but removing it or making it permanently conditional requires an explicit maintainer decision recorded in the specification or a product decision record.

## Naming and preserved scope

The product name is HardSlice. Historical `Keel`, `KEELxxx`, and `keel` identifiers map respectively to HardSlice, `HSxxx`, and `dotnet hardslice`.

The following product pillars remain committed roadmap scope:

- greenfield solution and item templates;
- Roslyn analyzers, architecture tests, and agent-repairable diagnostics;
- agent guidance, skills, hooks, and a single verification entry point;
- progressive adoption on existing solutions, including mapping, baseline, ratchet, and reporting;
- reproducible public benchmarks.

Useful refinements from the evidence-first plan remain design inputs: NuGet distribution, a pinned local tool, HS016 as the first analyzer, analyzer performance and false-positive testing, explicit analysis levels, occurrence-aware baselines, and protected CI as the final authority.

## Treatment of existing phase-0 work

The current phase-0 backlog, sprint, and decision registry remain in place for traceability but receive an inactive historical banner. They are no longer prerequisites for product implementation.

The Roslyn and convention notes remain technical references. Research material may continue in parallel without blocking the first code increment. Runner isolation, immutable run identifiers, holdout governance, and benchmark evidence mechanics are deferred until a product loop exists to benchmark.

## First implementation milestone

The first milestone demonstrates the product rather than the research process:

> From a clean checkout, `dotnet new hardslice-api` generates a minimal Bookings application that builds and tests successfully. A forbidden reference between two slices produces an actionable HS016 diagnostic. A coding agent fixes the violation without bypassing the rule, and the build and tests return to green.

The milestone includes a compilable solution, the production-shaped HS016 analyzer and tests, a minimal Bookings reference application, repair documentation, `AGENTS.md`, verification scripts, local template packaging, and one recorded development E2E. Statistical benchmarking, complete adoption tooling, the full rule catalogue, and multi-agent support follow in later increments without being removed from product scope.

## Success criteria for the realignment

- The specification no longer claims to be superseded.
- No active document contradicts the specification's product scope.
- The former evidence-first plan and phase-0 work remain discoverable as historical references.
- The active roadmap preserves every product pillar from the specification.
- The active backlog is short, code-oriented, and starts with an executable vertical slice.
- The next working context can begin implementation without reopening phase-0 governance.
