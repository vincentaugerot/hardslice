---
name: add-module
description: Adds a module to this HardSlice solution with dotnet new hardslice-module, registers it in Program.cs and in the map of AGENTS.md, then runs the check. Use when asked to add a module, a bounded context or a new business area, such as Catalog or Billing, before its aggregates and slices exist.
---

# Add a module

A module is a business area: its domain in `src/MyApp.Domain/<Module>/`, its slices in `src/MyApp/Features/<Module>/`, and its public contract `<Module>Module.cs`, the only types other modules may use. `hardslice-module` creates the contract alone, and the templates of its aggregates and slices create the folders of its domain and of its tests as they fill them: never write them by hand.

Run every command from the root of the solution, the folder of `MyApp.slnx`. If the solution was never built, run `dotnet build` first: the template reads the web project.

## Steps

1. Name the module after the business area, in PascalCase and without the `Module` suffix: `Catalog`, not `CatalogModule`. Check in `AGENTS.md` (Modules and slices) that no module already covers it.
2. Generate it:

   ```text
   dotnet new hardslice-module --project src/MyApp/MyApp.csproj --name <Module>
   ```

   Add `--route <route>` only when its endpoints must answer under a group other than the name in kebab-case, such as `--route meeting-rooms`. The contract holds it in its constant `Route`, under which the tests that the `add-slice` skill generates call their slices. If the template refuses, read its message, fix the argument and run it again: a refusal prints `created successfully` first, then fails with exit code 105 and creates nothing.
3. Copy the three lines it prints into `src/MyApp/Program.cs`, at the places it names: the `using`, `builder.Services.Add<Module>Module();` before `var app = builder.Build();`, and `app.Map<Module>Module();` before `app.Run();`. Change nothing else in that file.
4. Add the module to the "Modules and slices" map of `AGENTS.md`, its name in backticks followed by `: no slice yet.`, in place of the line `- None yet: ...` for the first module.
5. Run the check, and fix the code until it exits 0:

   ```text
   bash scripts/check.sh     # macOS, Linux, Git Bash
   pwsh scripts/check.ps1    # Windows, PowerShell
   ```

6. Continue with the `add-aggregate` skill for its first aggregate, then `add-slice` for each use case.

## Never

These are the workarounds of [Forbidden](../../../AGENTS.md#forbidden); if the check cannot pass without one, stop and explain the conflict instead.

- Write `<Module>Module.cs` yourself, create an empty folder of the module ahead of its files, with a `.gitkeep` or otherwise, or rename the generated contract.
- Make a module use a type of another module outside its public contract, such as a slice or a mapping.
- Suppress a HardSlice diagnostic, lower its severity or turn the analyzers off.
- Create, edit or delete a protected file: the list of [Forbidden](../../../AGENTS.md#forbidden) names them all.
