---
name: add-domain-event
description: Adds a domain event to this HardSlice solution with dotnet new hardslice-event, raises it from the method of the aggregate where the fact happens, and adds the reaction slice that handles it with its tests, then runs the check. Use when asked to react to something that happened in the domain, to publish or raise an event, or to update another aggregate or module when a fact occurs, such as when a product is renamed.
---

# Add a domain event

A domain event is a fact that an aggregate records, a `sealed record` named in the past tense in `src/MyApp.Domain/<Module>/Events/`. A reaction slice holds the handler that reacts to it, in `src/MyApp/Features/<Module>/<Reaction>/`, with its integration tests. The template creates both: never write that structure by hand.

The pipeline publishes the events of a command to their handlers after the command handler and before saving, in the same transaction: an exception of a handler rolls the whole command back. Events are not stored.

Run every command from the root of the solution, the folder of `MyApp.slnx`. If the solution was never built, run `dotnet build` first: the template reads the web project.

## Steps

1. Find the method of the aggregate where the fact happens, in `src/MyApp.Domain/<Module>/`, and the command slice that calls it. If neither exists yet, use the `add-slice` skill first.
2. Name the event as a past fact, in PascalCase: `ProductRenamed`, never `RenameProduct` nor `ProductRename`. Name the reaction after what it does: `RecountCategoryOnProductRenamed`. The reaction may live in another module than the event: it depends on the domain only.
3. Generate the event and its reaction:

   ```text
   dotnet new hardslice-event --project src/MyApp/MyApp.csproj --module <Module> --name <Event> --reaction <ReactionModule>/<Reaction>
   ```

   Leave out `--reaction <ReactionModule>/<Reaction>` for an event that nothing handles yet. To add a reaction later to an event the template created and you have not changed, run it again with `--reaction` and `--force`; once the event is completed, write the reaction on the model of a generated one. If the template refuses or warns that the name is not a past fact, read its message, fix the name and run it again.
4. Complete the event in `src/MyApp.Domain/<Module>/Events/<Event>.cs`: replace `Id` with the values of the fact that a handler needs, such as the identifier of the aggregate and the new name. Values only: an identifier, never the aggregate itself.
5. Raise it from the aggregate: in the method where the fact happens, after its rules hold, add `_domainEvents.Add(new <Event>(...));`. Do not raise it when the method changes nothing. Add to the unit tests of the aggregate, in `tests/MyApp.Tests/Domain/<Module>/`, an assertion that the method records the event with its values in `DomainEvents`, and that a refused call records none.
6. Register the reaction with the lines the template printed, in `src/MyApp/Features/<ReactionModule>/<ReactionModule>Module.cs`: the usings the file does not have yet and `.AddScoped<IDomainEventHandler<<Event>>, <Reaction>Handler>()` in `Add<ReactionModule>Module`. A forgotten registration fails `ProgramTests`, which names the type.
7. Implement the handler in `src/MyApp/Features/<ReactionModule>/<Reaction>/<Reaction>.cs`: inject `AppDbContext`, load the aggregate to change with `Set<TAggregate>()`, and call its method. It never calls `SaveChangesAsync`, never sends a request through `IMediator`, and never has an external effect, such as an e-mail or an HTTP call, which would happen even if the transaction rolls back.
8. Replace every `TODO(hardslice):` marker of the reaction and of its tests: search for `TODO(hardslice):` under `src/` and `tests/`. The integration test asserts what the handler changed, read back from PostgreSQL. An empty test, or one that asserts nothing, does not replace a marker.
9. Add the reaction to the entry of its module in the "Modules and slices" map of `AGENTS.md`, as a reaction to `<Event>`.
10. Run the check, and fix the code until it exits 0:

    ```text
    bash scripts/check.sh     # macOS, Linux, Git Bash
    pwsh scripts/check.ps1    # Windows, PowerShell
    ```

## Never

These are the workarounds of [Forbidden](../../../AGENTS.md#forbidden); if the check cannot pass without one, stop and explain the conflict instead.

- Write the event or the reaction slice yourself, or make the event a `class`.
- Raise the event from a handler or an endpoint instead of the aggregate.
- Make the handler `public`, give the reaction slice a request or an endpoint, or use a type of another slice or of another module outside its public contract.
- Leave an empty test or an assertion that checks nothing in place of a `TODO(hardslice):` marker.
- Suppress a HardSlice diagnostic, lower its severity or turn the analyzers off.
- Edit a protected file: `.editorconfig`, `hardslice.json`, `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props`, `.config/dotnet-tools.json`, `scripts/`, `tests/MyApp.ArchitectureTests/`, `.claude/` and `.agents/`.
