---
name: add-aggregate
description: Adds an aggregate to a module of this HardSlice solution with dotnet new hardslice-aggregate, or a child entity of an aggregate with dotnet new hardslice-entity, gives it its state, its invariants and their unit tests, maps it and adds its migration, then runs the check. Use when asked to add an aggregate, an entity, a business object or a table that slices will create, change or read, such as a Product in a Catalog module, or the parts of an aggregate, such as the lines of an order.
---

# Add an aggregate

An aggregate is the root of a consistency boundary in `src/MyApp.Domain/<Module>/`: it holds the business rules of its state, which changes only through its methods. The template creates the aggregate, its identifier `<Aggregate>Id`, its EF Core mapping in `src/MyApp/Features/<Module>/<Aggregate>Configuration.cs` and its unit tests in `tests/MyApp.Tests/Domain/<Module>/`: never write that structure by hand.

A child entity has an identity of its own but belongs to one aggregate, such as the lines of an order: `dotnet new hardslice-entity` generates it, its identifier, its mapping and its unit tests, as [Child entities](#child-entities) says.

Run every command from the root of the solution, the folder of `MyApp.slnx`. If the solution was never built, run `dotnet build` first: the template reads the web project.

## Steps

1. The module must exist: `src/MyApp/Features/<Module>/<Module>Module.cs`. If it does not, use the `add-module` skill first.
2. List the state of the aggregate and its invariants: the rules that every instance must respect, such as a required name or a positive price. A value with its own rules, such as a period or an amount, becomes a value object of the module, a `record` that checks them when it is built. A part with an identity of its own becomes a child entity, generated once the root exists. Another aggregate is held by its identifier only, `CategoryId CategoryId` and never `Category Category`.
3. Generate it, with `--table <Table>` only when the table is not the name of the aggregate followed by `s`:

   ```text
   dotnet new hardslice-aggregate --project src/MyApp/MyApp.csproj --module <Module> --name <Aggregate>
   ```

   If the template refuses, read its message, fix the argument and run it again: a refusal prints `created successfully` first, then fails with exit code 105 and creates nothing. It refuses, among others, a name that a module, a namespace or a common type already has, such as `Rooms` or `Task`: name the aggregate after one instance, such as `Room` or `WorkItem`.
4. Complete the aggregate in `src/MyApp.Domain/<Module>/<Aggregate>.cs`:
   - rename the factory method `Create` after the business fact, such as `Register` or `Open`, and take the state of the new aggregate as parameters;
   - throw a `DomainException` for each invariant they break, answered with 400, or a `ConflictException` for a rule that the current state breaks, answered with 409;
   - keep every property without public setter nor `init` accessor: add a method for each change of state, which checks the same invariants;
   - an instant comes as a parameter, never from the system clock.
5. Replace the `TODO(hardslice):` marker of `tests/MyApp.Tests/Domain/<Module>/<Aggregate>Tests.cs` with real assertions: the state given by the factory method, then one test per invariant it refuses (`Should.Throw<DomainException>`), and the same for each method you added.
6. Map the state in `src/MyApp/Features/<Module>/<Aggregate>Configuration.cs`, in fluent API: column types, lengths and required values that match the invariants. `AppDbContext` applies the mapping by itself: there is nothing to register. Never add EF Core attributes or references to the domain.
7. Add the migration that the template printed, once the mapping is complete:

   ```text
   dotnet tool restore
   dotnet ef migrations add Add<Table> --project src/MyApp --output-dir Shared/Persistence/Migrations
   ```

   Read the generated migration: it must create the table of the aggregate and nothing else. A constraint EF Core cannot model goes in it through `migrationBuilder.Sql`. If you change the mapping again before committing, remove the migration with `dotnet ef migrations remove --project src/MyApp` and add it again; never edit a migration that may already be applied.
8. Add the aggregate to the entry of its module in the "Modules and slices" map of `AGENTS.md`, as `Mapping: <Aggregate>Configuration`.
9. Run the check, and fix the code until it exits 0:

   ```text
   bash scripts/check.sh     # macOS, Linux, Git Bash
   pwsh scripts/check.ps1    # Windows, PowerShell
   ```

10. Continue with the `add-slice` skill for each use case that creates, changes or reads the aggregate.

## Child entities

1. The aggregate must exist, with its mapping `src/MyApp/Features/<Module>/<Aggregate>Configuration.cs`. If it does not, generate it first as above.
2. Choose how the child is tied to its root:
   - held by its root, the default: the root holds its children in a collection, loads and saves them with itself, and checks the rules of the whole aggregate, such as a maximum number of lines. Choose it while a root holds a bounded number of children;
   - holding the identifier of its root, with `--link id`: the form of a collection too large to load with its root, such as millions of rows. The root holds nothing of it, a command writes the children of one root apart from it, and a query reads them by the identifier of the root. HS008 then knows no owner for the child and reports nothing on it: hold any other aggregate by its identifier all the same.
3. Generate it, with `--table <Table>` only when the table is not the name of the child followed by `s`. A child held by its root gives the same name to the collection of the root:

   ```text
   dotnet new hardslice-entity --project src/MyApp/MyApp.csproj --module <Module> --aggregate <Aggregate> --name <Entity>
   dotnet new hardslice-entity --project src/MyApp/MyApp.csproj --module <Module> --aggregate <Aggregate> --name <Entity> --link id
   ```

   It refuses an unknown module or aggregate, and a name that the module already maps. The mapping of the child, `<Entity>Configuration.cs`, lives next to the mapping of its root, one file per entity. The identifier of the root is stored through `<Aggregate>IdConverter`, a `ValueConverter` in the same folder that the first child creates and the next ones reuse: the mapping of the root may use it too, with `HasConversion<<Aggregate>IdConverter>()`. Never write a second converter for the same identifier.
4. For a child held by its root, add to the root the private list and its read-only view that the template prints, under these names: until the root declares them, EF Core cannot build the model and the check fails. Then add a method of the root for each change of the collection, such as `AddLine`, which builds the child with its factory method and checks the rules of the whole aggregate, and test these methods in the tests of the root. Never add, change nor remove a child from outside its root.
5. Complete the child as steps 4 to 6 complete a root: its factory method named after the business fact, its rules, the `TODO(hardslice):` marker of `tests/MyApp.Tests/Domain/<Module>/<Entity>Tests.cs`, and the mapping of its state.
6. Add the migration that the template printed, as step 7 says: it must create the table of the child, with its foreign key to the table of the root, and nothing else. Add the mapping of the child to the entry of its module in `AGENTS.md`, then run the check.

## Never

These are the workarounds of [Forbidden](../../../AGENTS.md#forbidden); if the check cannot pass without one, stop and explain the conflict instead.

- Write the aggregate, a child entity, their identifiers or their mappings yourself, or remove `IAggregateRoot` or `IEntity`.
- Give the aggregate a public setter, an `init` accessor or a public field to make a slice or a test simpler.
- Put an invariant in a handler, a validator or an endpoint instead of the aggregate.
- Leave an empty test or an assertion that checks nothing in place of the `TODO(hardslice):` marker.
- Suppress a HardSlice diagnostic, lower its severity or turn the analyzers off.
- Create, edit or delete a protected file: the list of [Forbidden](../../../AGENTS.md#forbidden) names them all.
