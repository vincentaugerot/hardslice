---
name: add-aggregate
description: Adds an aggregate to a module of this HardSlice solution with dotnet new hardslice-aggregate, or a child entity of an aggregate with dotnet new hardslice-entity, gives it its state, its invariants and their unit tests, maps it and adds its migration, then runs the check. Use when asked to add an aggregate, an entity, a business object or a table that slices will create, change or read, such as a Product in a Catalog module, or the parts of an aggregate, such as the lines of an order, or children that a command writes in bulk. Also use when a mapping changes a table that already holds rows, when an entity stores its state in another shape than the one it shows, in private fields, or a value object in the columns of its row, when two commands may change the same aggregate at the same time, for an aggregate written once and never changed, such as a trace, and when a command deletes rows in bulk, such as a purge.
---

# Add an aggregate

An aggregate is the root of a consistency boundary in `src/MyApp.Domain/<Module>/`: it holds the business rules of its state, which changes only through its methods. The template creates the aggregate, its identifier `<Aggregate>Id`, its EF Core mapping in `src/MyApp/Features/<Module>/<Aggregate>Configuration.cs`, its unit tests in `tests/MyApp.Tests/Domain/<Module>/` and the integration test of its concurrency token in `tests/MyApp.IntegrationTests/Features/<Module>/<Aggregate>ConfigurationTests.cs`: never write that structure by hand.

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
6. Map the state in `src/MyApp/Features/<Module>/<Aggregate>Configuration.cs`, in fluent API: column types, lengths and required values that match the invariants. A state that the table stores in another shape than the one the aggregate shows lives in private fields, as [Mapping private fields](#mapping-private-fields) says. Keep the line `builder.Property<uint>("Version").IsRowVersion();`: it is the concurrency token of the aggregate, as [Concurrent changes](#concurrent-changes) says, and `<Aggregate>ConfigurationTests` fails without it. That test passes as generated: add the integration tests of the mapping next to it. `AppDbContext` applies the mapping by itself: there is nothing to register. Never add EF Core attributes or references to the domain.
7. Add the migration that the template printed, once the mapping is complete:

   ```text
   dotnet tool restore
   dotnet ef migrations add Add<Table> --project src/MyApp --output-dir Shared/Persistence/Migrations
   ```

   Read the generated migration: it must create the table of the aggregate and nothing else. Its column `xmin` is the concurrency token, which Npgsql never creates: PostgreSQL gives that system column to every table, and the migration only records the token in the model snapshot. A change of a table that already holds rows follows [Changing the shape of stored rows](#changing-the-shape-of-stored-rows). A constraint EF Core cannot model goes in it through `migrationBuilder.Sql`. If you change the mapping again before committing, remove the migration with `dotnet ef migrations remove --project src/MyApp` and add it again; never edit a migration that may already be applied.
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

## Concurrent changes

Two commands that change the same aggregate at the same time, from two requests, a recurring command and its endpoint, or two instances of the application, each load it, change their copy and save it. Without a check, the last commit overwrites the first without error. The concurrency token of the mapping is that check:

- `builder.Property<uint>("Version").IsRowVersion();`, generated in `<Aggregate>Configuration.cs`, maps a shadow property on `xmin`, the system column that PostgreSQL changes at each update of a row. The domain knows nothing of it. EF Core updates the row of the root only while its `xmin` is the one it loaded: a command that changes an aggregate that another request changed since it loaded it saves nothing, and `TransactionBehavior` turns the `DbUpdateConcurrencyException` into a `ConflictException`, answered with 409, after rolling the whole command back. The caller reads the aggregate again and retries, and a recurring command retries at its next cycle, or at its next retry when its schedule has some.
- The token of the root covers its children held in its collection. A command that adds or changes only a child leaves the row of the root as it is, and EF Core would check no token: `AppDbContext` saves the tracked root of each child added, changed or removed, so that two commands that each add a line to the same order, each checking a maximum of lines, never both commit. The root must be loaded with its children, as a change of a child always is, through a method of the root. It also needs a column of its own besides its identifier: EF Core sends no update for a row without one, and the save of a root whose only state is its children fails with `The children of the aggregate <Aggregate> changed, but its mapping has no column besides its key and its concurrency token` until its mapping holds a state of the root.
- Children that hold the identifier of their root (`--link id`), and those written in bulk, are written apart from the root, which they leave as it is: its token does not cover them.
- The token never covers a creation: two commands that create the same aggregate rely on its unique index and the 409 of its violation. A merge that must never fail, such as a collection that enriches what a concurrent run stored and would lose its work on a 409, takes a transaction lock instead. `add-slice` details both.
- An aggregate mapped before its token, by hand or by an earlier template, gets the line `builder.Property<uint>("Version").IsRowVersion();` in its mapping, then a migration of its own, `dotnet ef migrations add Add<Aggregate>ConcurrencyToken --project src/MyApp --output-dir Shared/Persistence/Migrations`. Its `AddColumn` of `xmin` creates nothing either: write above it the comment `// xmin is a system column of every PostgreSQL table: Npgsql generates no SQL for it, and this migration only records the concurrency token of <Aggregate>Configuration in the model.`, and add no SQL. Then add the test that the template generates for a new aggregate, in `tests/MyApp.IntegrationTests/Features/<Module>/<Aggregate>ConfigurationTests.cs`, next to the other tests of its mapping, in a class that receives `PostgresDatabase database` and uses `Microsoft.EntityFrameworkCore`:

  ```csharp
  // The concurrency token of <Aggregate>Configuration, read from the model of AppDbContext.
  [Fact]
  public async Task Maps_the_Version_of_<Aggregate>_on_xmin_as_its_concurrency_token()
  {
      await using var db = database.CreateDbContext();

      var version = db.Model.FindEntityType(typeof(<Aggregate>))?.FindProperty("Version");

      version.ShouldNotBeNull("<Aggregate>Configuration maps no concurrency token: keep builder.Property<uint>(\"Version\").IsRowVersion();.");
      version.IsShadowProperty().ShouldBeTrue();
      version.ClrType.ShouldBe(typeof(uint));
      version.IsConcurrencyToken.ShouldBeTrue();
      version.ValueGenerated.ShouldBe(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate);
      version.GetColumnName().ShouldBe("xmin");
  }
  ```

The test of the model proves that the token is mapped: a migration that records its removal leaves the other tests green, those of the 409 aside, and this one fails. The integration test of the 409 comes with the first command that changes the aggregate, as `add-slice` says: it lets two commands load the aggregate before either saves, then asserts that one commits and the other throws `ConflictException`.

## An aggregate that never changes

An aggregate that a command creates and nothing changes after, such as a trace, an entry of a log or a reading, is generated and completed as any other: the template has no other form for it.

- Keep the line `builder.Property<uint>("Version").IsRowVersion();` of its mapping and `<Aggregate>ConfigurationTests` as generated. While nothing updates its rows, nothing meets the token, and it costs nothing: `xmin` is a system column, for which the migration creates no column. The token and its test of the model cover the first method that changes the aggregate later, from its first update. Say so in the comment of the token, such as `Never changed once written, so nothing meets it yet: kept so that a method added later is covered from its first update.`
- Its factory method builds it whole, with every invariant checked there: add no method that changes its state, and no setter for a test. Its unit tests cover the factory method and each value it refuses.
- The integration test of the 409 waits for that first method: it comes with the first command that changes the aggregate, as [Concurrent changes](#concurrent-changes) says. Write none before.
- Two commands that create such aggregates at the same time each write their own row, which the token never covers. When the business allows one row per key only, such as one trace per cycle, a unique index answers the second with 409.
- Its rows removed by a retention or a purge follow [Rows deleted in bulk](#rows-deleted-in-bulk).

## Children written in bulk

A command that writes many children of one root at once, such as the millions of rows of an import, may write them outside the change tracker, which would take minutes and gigabytes to save them. These children hold the identifier of their root (`--link id`). The solution generates no helper for it: write it in the handler, or in `src/MyApp/Shared/Persistence/` once several slices need it, with no module type in it.

1. Build each child with its factory method, as any child, so that its rules hold.
2. Add the root, then save it early, before the first child: `db.Set<<Aggregate>>().Add(root);`, then `await db.SaveChangesAsync(cancellationToken);`. The save runs in the transaction of the command, which still commits or rolls back everything. The root stays tracked: the pipeline still publishes its domain events, then saves what changed after and commits.
3. Write the children after that save, through the connection of `AppDbContext`, which already holds the transaction of the command, such as a binary `COPY` from `((NpgsqlConnection)db.Database.GetDbConnection()).BeginBinaryImportAsync(...)`. Never open a connection or a transaction of your own: the rows would outlive a rollback of the command. Write the table and the columns that the mapping declares, each value as its column stores it, through the converter of the mapping for a typed identifier, such as `<Aggregate>IdConverter`. Nothing is tracked, generated nor published for these rows: no default value of the mapping, no domain event.
4. Keep the foreign key to the root immediate, as the migration of the child creates it: PostgreSQL checks each row as it is written, against the root saved before. Never make it `DEFERRABLE INITIALLY DEFERRED` nor remove it to write the children first: a deferred key is checked row by row at the commit, which then takes minutes and outlasts its timeout.
5. A write that takes longer than the 30 seconds that Npgsql gives a command by default gets a longer timeout in that command only: the `Timeout` of the importer for a `COPY`, `db.Database.SetCommandTimeout(...)` for the SQL that EF Core sends. Never raise it in the connection string, which would change it for every request.
6. The integration test of the command reads the children back from the database, and checks that a command that fails partway, such as on an invalid row of its source, leaves neither the root nor any child.

## Rows deleted in bulk

A command that deletes many rows at once, such as a retention that removes what is older than a period or a purge of what was archived, deletes them with `ExecuteDeleteAsync`, without loading them: removing hundreds of thousands of aggregates through the change tracker would take minutes and gigabytes. The solution generates no helper for it: write it in the handler.

`ExecuteDeleteAsync` deletes rows, not aggregates: no method of the aggregate runs, nor its rules, nor a domain event, and no concurrency token is checked. A command that changes one of these aggregates while the purge runs either commits first, and the purge deletes the row all the same, or finds the row gone, and its save fails with 409. Delete in bulk only rows that the rule of the purge declares due, never an aggregate whose deletion is a business fact that a rule checks or that other code reacts to: that one is loaded, removed through a method of its own and records its domain event.

1. Put the rule that decides what is purged in the domain, as any rule: a method of an aggregate or a value object of the module that says which rows are due, such as the instant before which a day is kept, and that checks the counts deleted. The handler only runs the statements.
2. Delete in the transaction of the command, through `AppDbContext`: each `ExecuteDeleteAsync` runs at once in the transaction that the pipeline opened, and a rollback of the command restores every row. Never open a connection or a transaction of your own, and never delete in bulk an aggregate that the command also tracks.
3. Delete in batches whose size is a constant of the handler, such as 1,000 roots per statement, so that each statement stays well under the 30 seconds that Npgsql gives a command by default. A batch that still needs longer gets a longer timeout in that command only, `db.Database.SetCommandTimeout(...)`, as [Children written in bulk](#children-written-in-bulk) says.
4. Delete the children of a batch first, then its roots. The foreign key of a child, generated with `OnDelete(DeleteBehavior.Cascade)`, would delete them with their root, but inside the statement of the roots, which counts only the roots: the count of the children would be lost.
5. Delete only aggregates of the module of the command. The rows of another module are deleted by that module, through one of its own commands or a reaction to a domain event, as [HS012](../../../docs/rules/HS012.md#reading-the-aggregates-of-another-module) says.
6. Give the counts that each statement returns to the domain, which compares them with what its rule expects, such as the rows written in an archive, and throws a `DomainException` when they differ: the whole command rolls back, every deletion with it.

```csharp
// src/MyApp/Features/<Module>/PurgeReadings/PurgeReadings.cs, in the handler
// About 40 values a reading: about 40,000 rows deleted by statement.
private const int ReadingsPerBatch = 1_000;

// In Handle: ReadingPurge, an aggregate of the module, says which readings are due, then records the purge.
var purge = ReadingPurge.Start(clock.GetUtcNow(), options.Value.Retention);
var readingIds = await db.Set<Reading>()
    .Where(reading => reading.TakenAt < purge.Before)
    .Select(reading => reading.Id)
    .ToListAsync(cancellationToken);

var values = 0;
var readings = 0;
foreach (var batch in readingIds.Chunk(ReadingsPerBatch))
{
    values += await db.Set<ReadingValue>().Where(value => batch.Contains(value.ReadingId)).ExecuteDeleteAsync(cancellationToken);
    readings += await db.Set<Reading>().Where(reading => batch.Contains(reading.Id)).ExecuteDeleteAsync(cancellationToken);
}

// Throws a DomainException when the counts differ from those of its rule, which rolls every deletion back.
purge.Record(readingIds.Count, readings, values);
db.Set<ReadingPurge>().Add(purge);
```

The integration test of the command checks that the rows that the rule keeps are still there, that the rows it purges are gone, children included, and that a purge whose counts differ, or that fails partway, leaves every row.

## Changing the shape of stored rows

A mapping change on a table that already holds rows, such as one row per stop of a trip becoming one row per trip with its stops in arrays, a column split in two, or a child entity that becomes a value object, comes out of `dotnet ef migrations add` as drops and adds of tables or columns, with empty defaults. Applied, it loses every row, or fails on a new key or a required column. EF Core only warns `An operation was scaffolded that may result in the loss of data. Please review the migration for accuracy.`

1. Read `Up` and `Down` of the generated migration before applying it anywhere. On a table that holds rows, a `DropTable`, a `DropColumn`, an `AlterColumn` that changes a type, an `AddColumn` with a `defaultValue` that EF Core made up, or that warning, means the migration loses or invents data: rewrite it before it is applied. It is yours to edit until then, never after: once it may be applied somewhere, add another migration instead.
2. Rewrite the loss as a conversion, in SQL through `migrationBuilder.Sql`:
   - to a new table: rename the old table and its primary key (`RenameTable`, then `ALTER TABLE ... RENAME CONSTRAINT`), create the new table as EF Core scaffolded it, fill it from the old one with `INSERT INTO ... SELECT ...`, such as `array_agg(... ORDER BY ...)` and `GROUP BY` to gather rows into one, then drop the old table;
   - inside a table: add the new column as nullable, fill it with `UPDATE`, then make it required, and drop the old column last.

   Keep the names, types, keys and indexes that EF Core scaffolded for the new shape: the model snapshot describes them, and the integration tests compare the model with that snapshot, never with your SQL.
3. Write the `Down` that converts back, the same way in reverse: the old shape filled from the new one, such as `CROSS JOIN LATERAL unnest(...)` to spread arrays back into rows. When the new shape keeps less than the old one, such as a dropped column, the loss is a decision: ask the user before writing it, and say in a comment of the migration what `Up` drops and `Down` cannot bring back.
4. A conversion of millions of rows takes longer than the 30 seconds that Npgsql gives a command by default, in either direction. Apply it with a longer timeout for that run only, in the connection string that `dotnet ef database update` receives: `dotnet ef database update --project src/MyApp --connection "<connection string>;Command Timeout=600"`. Never raise it in the configuration nor in the code of the application, which would change it for every request.
5. Try it on a copy of the data, in both directions, before a database that matters. Restore a dump of the data in a local container, apply the migration, compare the rows (a count, a few values read through the new mapping), go back with `dotnet ef database update <PreviousMigration> --project src/MyApp`, which runs `Down`, compare with the dump, then apply it again. The integration tests migrate an empty database only: they never see a row converted. An integration test may cover the conversion on a few rows: in a database of its own, `db.GetService<IMigrator>().MigrateAsync("<PreviousMigration>")`, rows of the old shape inserted with SQL, then `db.Database.MigrateAsync()` and the rows read through the new mapping, the database dropped at the end.

## A value object stored in the row of its entity

A value object that an entity holds as one property, such as the period of a reservation or the address of a customer, is stored in columns of the row of its entity with `ComplexProperty`: it has no table, no key and no identity of its own. HS007 accepts the record, and HS901 and HS902 never count it as an entity. For an aggregate `Reading` generated by `dotnet new hardslice-aggregate`, which holds the value object `Measure` twice:

```csharp
// src/MyApp.Domain/<Module>/Measure.cs
namespace MyApp.Domain.<Module>;

// One measure of a reading: a value and its unit, checked when the domain builds it.
public sealed record Measure
{
    // Also the constructor of EF Core, which reads a stored row through it: one parameter per property, named after it.
    private Measure(decimal value, string unit)
    {
        Value = value;
        Unit = unit;
    }

    public decimal Value { get; }

    public string Unit { get; }

    public static Measure Of(decimal value, string unit)
    {
        if (string.IsNullOrWhiteSpace(unit))
            throw new DomainException("A measure has a unit.");

        return new Measure(value, unit.Trim());
    }
}
```

```csharp
// src/MyApp.Domain/<Module>/Reading.cs, members added to the generated aggregate
public Measure Temperature { get; }

public Measure Humidity { get; }

// In the rehydration constructor of the generated aggregate, values that EF Core replaces with the stored ones.
Temperature = null!;
Humidity = null!;
```

```csharp
// src/MyApp/Features/<Module>/ReadingConfiguration.cs, lines added to Configure, then the method they share
// The columns of each measure, in the row of the reading: Temperature_Value, Temperature_Unit, Humidity_Value, Humidity_Unit.
builder.ComplexProperty(reading => reading.Temperature, MapMeasure);
builder.ComplexProperty(reading => reading.Humidity, MapMeasure);

private static void MapMeasure(ComplexPropertyBuilder<Measure> measure)
{
    measure.Property(m => m.Value).HasPrecision(9, 3);
    measure.Property(m => m.Unit).HasMaxLength(8);
}
```

- Each property of the value object becomes a column named `<Property>_<Field>`, such as `Temperature_Value`. `HasColumnName` gives another name, such as `Start` for the field `Start` of a period held once. The migration adds these columns to the table of the entity, and nothing else.
- EF Core builds the value object through a constructor whose parameters have the names of its properties, private or not. A private one that takes the stored values as they are keeps the rules in the factory method: a rule made stricter later never refuses a row already stored. A public constructor that checks the rules works too, and then runs on each row read.
- The value object stays a `sealed record` of the domain, with no EF Core attribute nor reference, and no setter: a change of the entity replaces it whole, through a method of the entity.
- Prefer [Mapping private fields](#mapping-private-fields) when the row stores the state in another shape than the value object, such as a list of `TripStop` stored in two arrays, or a collection of value objects of any length: `ComplexProperty` maps one value object, held once or a fixed number of times, column by column.

## Mapping private fields

When the table stores the state of an entity in another shape than the one the entity shows, such as the stops of a trip stored in two arrays and read as a list of `TripStop`, the entity keeps the stored shape in private fields and shows a read-only view built from them. HS007 accepts private fields, and EF Core reads and writes them by their names. For an aggregate `Trip` generated by `dotnet new hardslice-aggregate`, with its value object `TripStop`:

```csharp
// src/MyApp.Domain/<Module>/TripStop.cs
namespace MyApp.Domain.<Module>;

// A stop of a trip, as the domain reads it: never mapped by itself.
public sealed record TripStop(string StopId, int Minutes);
```

```csharp
// src/MyApp.Domain/<Module>/Trip.cs, members added to the generated aggregate
// Two arrays of the same length, one item per stop in the order of the trip, read through Stops.
private string[] _stopIds = [];

private int[] _minutes = [];

private DateTimeOffset? _plannedAt;

public IReadOnlyList<TripStop> Stops => [.. _stopIds.Select((stopId, index) => new TripStop(stopId, _minutes[index]))];

public bool IsPlanned => _plannedAt is not null;

public void Plan(IReadOnlyList<TripStop> stops, DateTimeOffset plannedAt)
{
    if (stops.Count == 0)
        throw new DomainException("A trip has at least one stop.");

    _stopIds = [.. stops.Select(stop => stop.StopId)];
    _minutes = [.. stops.Select(stop => stop.Minutes)];
    _plannedAt = plannedAt;
}
```

```csharp
// src/MyApp/Features/<Module>/TripConfiguration.cs, lines added to Configure
// The private fields of Trip, by their names: each column takes the name of the domain, without the underscore.
builder.PrimitiveCollection<string[]>("_stopIds").HasColumnName("StopIds").IsRequired();
builder.PrimitiveCollection<int[]>("_minutes").HasColumnName("Minutes").IsRequired();
builder.Property<DateTimeOffset?>("_plannedAt").HasColumnName("PlannedAt");

builder.Ignore(trip => trip.Stops);
builder.Ignore(trip => trip.IsPlanned);
```

- `Property<T>("_field")` maps a scalar field, `PrimitiveCollection<T[]>("_field")` an array or a list of primitive values, which PostgreSQL stores in an array column, such as `text[]`. The name is the name of the field: once it is renamed, EF Core cannot build the model and the check fails. `HasColumnName` gives the column the name of the domain.
- Ignore each public view with `builder.Ignore(...)`: it is computed from the fields, never stored.
- The view is read-only, a new list or `AsReadOnly()`, never the array itself, which a caller could change: HS007 forbids a public mutable object. The fields change only in the methods of the entity, which check its rules.
- Add the migration once the mapping is complete, as step 7 says, and read its columns: one per field, with the type of the field.

## Never

These are the workarounds of [Forbidden](../../../AGENTS.md#forbidden); if the check cannot pass without one, stop and explain the conflict instead.

- Write the aggregate, a child entity, their identifiers or their mappings yourself, or remove `IAggregateRoot` or `IEntity`.
- Give the aggregate a public setter, an `init` accessor or a public field to make a slice or a test simpler.
- Put an invariant in a handler, a validator or an endpoint instead of the aggregate.
- Remove the concurrency token `Version` from a mapping, or add, change or remove a child from outside its root, which its token would then not cover.
- Write children in bulk before their root, through a connection or a transaction of your own, or under a deferred or a missing foreign key.
- Delete in bulk the rows of another module, through a connection or a transaction of your own, or without the domain checking the counts deleted.
- Apply a migration that drops or changes a table holding rows without converting them, in both directions, or raise the command timeout in the connection string of the application.
- Make a mapped private field public, or show the array it holds, so that a slice or a test reaches it.
- Leave an empty test or an assertion that checks nothing in place of the `TODO(hardslice):` marker.
- Suppress a HardSlice diagnostic, lower its severity or turn the analyzers off.
- Create, edit or delete a protected file: the list of [Forbidden](../../../AGENTS.md#forbidden) names them all.
