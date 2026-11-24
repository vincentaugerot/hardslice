---
name: add-module
description: Adds a module to this HardSlice solution with dotnet new hardslice-module, registers it in Program.cs and in the map of AGENTS.md, then runs the check. Use when asked to add a module, a bounded context or a new business area, such as Catalog or Billing, before its aggregates and slices exist. Also use when a module loads a store or builds values when the application starts, such as reference data read from files, from its own store or from that of another module, and for a service of its public contract or a module without aggregate.
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

6. Continue with the `add-aggregate` skill for its first aggregate, then `add-slice` for each use case. A module may start without aggregate, with value objects and enumerations of its domain only (`add-aggregate`, [Value objects](../add-aggregate/SKILL.md#value-objects)): the first `hardslice-value-object` creates its domain folder and its test folder, and the group of `Map<Module>Module` stays unused until its first slice.

## The public contract of a module

The contract of a module is what the other modules and its own slices may use, in the namespace `MyApp.Features.<Module>` at the root of `src/MyApp/Features/<Module>/`: `<Module>Module` and its `Route`, its integration events, and the services that several slices or modules read, such as a store of reference data that builds the value objects of its domain, a computation that two slices run, or an interface that another module implements. A slice never belongs to it (HS012). No template generates a service of the contract: write it by hand, register it in `Add<Module>Module`, as a singleton built at start when it loads data (below), and put its unit tests in `tests/MyApp.Tests/Features/<Module>/`, the test folder of the module (HS018). A store that builds the types of the domain of one module belongs to the contract of that module, even when several modules read it: `Shared/Persistence` keeps the stores of bytes, such as a file or object store, that know no type of the domain (HS017).

## Values built when the application starts

A module that loads a store from files and checks it, such as reference data embedded in the web project, or that builds its own values from the store of another module, registers it in `Add<Module>Module` with a factory that the host runs when it starts:

```csharp
services.AddSingletonBuiltAtStart(provider => TaxationReference.From(provider.GetRequiredService<ReferenceCatalog>()));
```

The factory runs once every module is registered, so the order of the lines of `Program.cs` does not matter, and it reads the public contract of the other module from the provider. An exception it throws stops the application before its first request, and fails the start of every `AppFactory` of the integration tests: data that the domain refuses never wait for the request that reads them. A setting that a job needs is checked by the condition of its command instead, as `add-slice` says, so that a missing key only holds the job back.

## Never

These are the workarounds of [Forbidden](../../../AGENTS.md#forbidden); if the check cannot pass without one, stop and explain the conflict instead.

- Write `<Module>Module.cs` yourself, create an empty folder of the module ahead of its files, with a `.gitkeep` or otherwise, or rename the generated contract.
- Make a module use a type of another module outside its public contract, such as a slice or a mapping.
- Build a store while the services are registered, or read back from the `IServiceCollection` an instance that another module registered: register it with `AddSingletonBuiltAtStart`.
- Suppress a HardSlice diagnostic, lower its severity or turn the analyzers off.
- Create, edit or delete a protected file: the list of [Forbidden](../../../AGENTS.md#forbidden) names them all.
