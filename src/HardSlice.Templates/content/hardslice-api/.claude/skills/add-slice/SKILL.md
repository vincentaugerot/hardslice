---
name: add-slice
description: Adds a use case to a module of this HardSlice solution, a command that changes state or a query that only reads, by generating its slice and its tests with dotnet new hardslice-command or hardslice-query, putting its business rule in the aggregate, registering it, filling its logic and its tests, then running the check. Use when asked to add a use case, an endpoint, a command, a query or a feature to an existing module, such as renaming a product or listing the products of a category.
---

# Add a slice

A slice is one use case in `src/MyApp/Features/<Module>/<Slice>/`: its request, its validator, its handler and its endpoint, with its tests at the same path under `tests/`. The templates create them: never write that structure by hand.

Run every command from the root of the solution, the folder of `MyApp.slnx`. If the solution was never built, run `dotnet build` first: the templates read the web project.

## Steps

1. Read the module: its entry in the "Modules and slices" map of `AGENTS.md` and `src/MyApp/Features/<Module>/<Module>Module.cs`. If the module does not exist, use the `add-module` skill; if the aggregate of the use case does not exist, use `add-aggregate`.
2. Read the aggregate the use case creates, changes or reads, in `src/MyApp.Domain/<Module>/`, and its unit tests in `tests/MyApp.Tests/Domain/<Module>/`.
3. Look for the business rule of the use case in the aggregate. If it already owns it, the slice calls that method. If not, add it to the aggregate first: a method that changes its state and throws a `DomainException` when a rule breaks (answered with 400), or a `ConflictException` when the current state forbids it (409), with its unit tests, one per rule. A handler, a validator or an endpoint never holds a business rule.
4. Name the slice after the use case, in PascalCase, verb first, without the `Command` or `Query` suffix: `RenameProduct`, `GetProduct`. Then generate it:
   - the use case changes state: a command, with its validator tests and its integration tests. Add `--parameters none` when it takes nothing from the request (step 6). It returns the `Guid` identifier of the aggregate by default; add `--response none` when it returns nothing (false is answered with 404, refused with `--parameters none`, which has no identifier to look for), or `--response <Dto>` for a DTO of values that the slice declares.

     ```text
     dotnet new hardslice-command --project src/MyApp/MyApp.csproj --module <Module> --name <Slice>
     ```

   - the use case only reads: a query of one DTO by its identifier, or of the list with `--result many`, with its integration tests.

     ```text
     dotnet new hardslice-query --project src/MyApp/MyApp.csproj --module <Module> --name <Slice> --aggregate <Aggregate>
     ```

   If the template refuses, read its message, fix the argument and run it again: a refusal prints `created successfully` first, then fails with exit code 105 and creates nothing.
5. Register the slice with the lines the template printed, in `src/MyApp/Features/<Module>/<Module>Module.cs`: the usings the file does not have yet, the registrations by contract in `Add<Module>Module`, and `<Slice>Endpoint.Map(group);` in `Map<Module>Module`. A forgotten registration fails `ProgramTests`, which names the type.
6. Fill the logic in `src/MyApp/Features/<Module>/<Slice>/<Slice>.cs`:
   - command: replace the `Id` field with the fields of the use case; the validator checks only their shape (required, format, length); the handler injects `AppDbContext`, loads the aggregate with `Set<TAggregate>()` (or adds a new one), calls the method of the aggregate and returns the response. It never calls `SaveChangesAsync`: the pipeline saves and commits the transaction. It injects `TimeProvider` when the domain needs the current instant;
   - command without parameters, such as an import that reads its source from the configuration: generated with `--parameters none`, it is an empty record, `public sealed record <Slice>Command : ICommand<TResponse>;`, without validator nor validator tests, and the template prints the registration of its handler alone. Never add a validator to it: an empty one is forbidden. The endpoint reads no body and sends `new <Slice>Command()`, and its integration test posts to its route without a body. A command generated without the option that turns out to take nothing from the request: delete its slice folder and its two test folders, then generate it again with `--parameters none`;
   - command that reads from outside, such as a download or a file: the handler injects what it needs, registered in `Add<Module>Module`, such as `IHttpClientFactory` (`services.AddHttpClient()`) and the options of the module, bound from its section of the configuration. Never a static `HttpClient` nor a path written in the code. An effect it has outside, such as a file it writes, survives a rollback of the command: make it harmless then, such as a file named after the hash of its content, which the next run replaces or reuses. An effect that a rollback must cancel, such as an e-mail, a payment or a message to another system, has no mechanism in this solution yet: stop and tell the user instead of writing it;
   - query: add the values the use case reads to the DTO and project them in `Select`. Keep `AsNoTracking()`: a query never loads an aggregate to change it and never saves. Add a parameter, and its validator, only for a filter;
   - endpoint: give it its route and its HTTP method, keep it on the group of the module, sending the request through `IMediator` and answering with `TypedResults`.
7. Replace every `TODO(hardslice):` marker of the slice and of its tests: search for `TODO(hardslice):` under `src/` and `tests/`. The integration tests assert the business outcome, such as the state stored in PostgreSQL, read back from the database, the 400 of an invalid request and the 404 of an unknown identifier; the validator tests assert each refused input. A command without parameters has no invalid request: its integration tests assert its success, and a failure of the domain when one can happen, such as the 409 of a state that forbids it. An empty test, or one that asserts nothing, does not replace a marker. If the module maps another group than its name in kebab-case, a route of its own given when it was created, change `Route` in the generated tests: they answer 404 until then.
8. Add the slice to the entry of its module in the "Modules and slices" map of `AGENTS.md`, among its commands or its queries.
9. Run the check, and fix the code until it exits 0. A HardSlice diagnostic names its rule and its sheet in `docs/rules/`: apply the first fix of the sheet that matches.

   ```text
   bash scripts/check.sh     # macOS, Linux, Git Bash
   pwsh scripts/check.ps1    # Windows, PowerShell
   ```

When the use case produces a fact that other code reacts to, use the `add-domain-event` skill once the slice passes. A domain event handler without tests (HS014, HS018) is a reaction slice: `add-domain-event` gives it its tests, which handle the event and assert what the handler changed.

## Never

These are the workarounds of [Forbidden](../../../AGENTS.md#forbidden); if the check cannot pass without one, stop and explain the conflict instead.

- Write the files of a slice yourself, or move its tests away from its path.
- Use a type of another slice, make a handler, a validator or an endpoint `public`, or move a type of the slice into the namespace of the module.
- Return an entity from a command, even inside a DTO or a collection.
- Put a business rule in the handler, the validator or the endpoint.
- Leave an empty validator or an empty test, or write a placeholder file, to satisfy a rule.
- Suppress a HardSlice diagnostic, lower its severity or turn the analyzers off.
- Create, edit or delete a protected file: the list of [Forbidden](../../../AGENTS.md#forbidden) names them all.
