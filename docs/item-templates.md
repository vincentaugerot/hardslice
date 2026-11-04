# Item templates

`HardSlice.Templates` ships five item templates besides the `hardslice-api` solution template. Each one generates one element of a solution in its place, with its tests, and prints the registrations to add: it never edits an existing file. The skills of the [agent loop](agent-loop.md) call them, so an agent never recreates the structure of an element by hand.

| Template | Generates | Skill that calls it |
|---|---|---|
| [`hardslice-module`](#a-module) | the public contract of a module, the folders of its domain and of its tests | `add-module` |
| [`hardslice-aggregate`](#an-aggregate) | an aggregate root, its identifier, its EF Core mapping and its unit tests | `add-aggregate` |
| [`hardslice-command`](#a-command-slice) | a command slice: command, validator, handler, endpoint and tests | `add-slice` |
| [`hardslice-query`](#a-query-slice) | a query slice that reads an aggregate: query, DTO, handler, endpoint and tests | `add-slice` |
| [`hardslice-event`](#a-domain-event-and-its-reaction) | a domain event and, on request, the reaction slice that handles it, with its tests | `add-domain-event` |

The examples below run from the root of a solution generated as `Rooms` by [Getting started](getting-started.md), once it is restored (after a first build or check). `--project` names the web project: the template reads the name of the application, its `RootNamespace`, and its modules from it.

The generated code always builds without any HardSlice diagnostic, but an element is never generated "done": its tests end with `Assert.Fail("TODO(hardslice): ...")` and `dotnet hardslice check` fails while a `TODO(hardslice):` marker is left, until the business logic and its assertions are written.

## A module

```text
dotnet new hardslice-module --project src/Rooms/Rooms.csproj --name Payments
```

It creates `src/Rooms/Features/Payments/PaymentsModule.cs`, the public contract of the module: `AddPaymentsModule`, which registers its slices, and `MapPaymentsModule`, which maps them on the group `/payments`, the name of the module in kebab-case (`--route` sets another one). It also creates the folder of its domain, `src/Rooms.Domain/Payments/`, and its test folders, `tests/Rooms.Tests/Domain/Payments/`, `tests/Rooms.Tests/Features/Payments/` and `tests/Rooms.IntegrationTests/Features/Payments/`, each holding a `.gitkeep`. Then it prints the using and the two lines to add to `Program.cs`.

The empty module builds and passes the check: no rule asks a module for a slice or a test. Without `--project`, before the first restore, with a name that is not PascalCase or ends with `Module`, or with a route that is not made of lowercase words, it exits with an error and creates nothing. It refuses to overwrite an existing module without `--force`.

## An aggregate

```text
dotnet new hardslice-aggregate --project src/Rooms/Rooms.csproj --module Payments --name Invoice
```

It creates the aggregate root `Invoice` and its identifier `InvoiceId`, a `readonly record struct` that wraps a `Guid Value`, in `src/Rooms.Domain/Payments/`, its EF Core mapping `InvoiceConfiguration` in `src/Rooms/Features/Payments/`, and its unit tests in `tests/Rooms.Tests/Domain/Payments/`. The root is `sealed` and implements `IAggregateRoot`: a private constructor for persistence, a static factory method `Create` to rename after the business fact and give the rules of the aggregate, no public setter, and a private list of events that `DequeueDomainEvents()` returns and empties. The mapping uses the table `Invoices`, the name followed by `s` (`--table` sets another one), converts the identifier and ignores the events. Then it prints the migration to add: there is nothing to register, `AppDbContext` applies the mapping by itself.

Its unit test of the state ends with `TODO(hardslice):`, and the integration tests fail until the migration is added: `MigrationsTests` finds changes missing from the migrations, and once the solution has a migration, EF Core refuses to migrate the database of the others.

It exits with an error and creates nothing without `--project`, before the first restore, with a name or a module that is not PascalCase, for a module without `Features/<Module>/<Module>Module.cs`, with a table that is not made of letters, digits and underscores, or with a name that the generated code could not use: that of a module or a namespace of the application (`Rooms`, `Domain`) or of a type that its files import (`Task`, `File`). It refuses to overwrite an existing aggregate without `--force`.

## A command slice

```text
dotnet new hardslice-command --project src/Rooms/Rooms.csproj --module Bookings --name ExtendBooking
```

It creates `src/Rooms/Features/Bookings/ExtendBooking/ExtendBooking.cs`, with the command `ExtendBookingCommand`, its validator, its handler and its endpoint `POST /bookings/extend-booking`, the validator tests in `tests/Rooms.Tests/Features/Bookings/ExtendBooking/` and the integration tests in `tests/Rooms.IntegrationTests/Features/Bookings/ExtendBooking/`. Then it prints the lines to add to `BookingsModule.cs`: the usings, the registrations of the handler and the validator, and the call of `Map`. `--response` sets what the command returns: `id` (a `Guid`) by default, `none` (a `bool` whose `false` is answered with 404), or the name of a DTO of values, which the slice declares.

`--parameters` sets what the command takes from the request: `id` by default, a `Guid Id` to replace with the fields of the use case, checked by its validator. `none` generates a command without parameters, such as an import that reads its source from the configuration: an empty record, an endpoint that reads no body, no validator nor validator tests, and an integration test that posts without body and checks its success instead of a 400. The template then prints the registration of the handler alone. It combines with `--response id` and with a DTO, never with `--response none`, whose 404 answers an unknown aggregate that such a command has no identifier to look for.

The validator tests pass. The handler throws `NotImplementedException` and the integration test ends with `TODO(hardslice):`. A slice its module does not register fails the check too: a test of the host resolves every request handler and validator of the application from the container, and names the ones that are missing.

Without `--project`, before the first restore, with a name that is not PascalCase or ends with `Command`, for a module without `Features/<Module>/<Module>Module.cs`, with a `--response` DTO named `Guid` or as a type of the slice, or with `--parameters none --response none`, it exits with an error that says what to do and creates nothing. It refuses to overwrite an existing slice without `--force`.

## A query slice

```text
dotnet new hardslice-query --project src/Rooms/Rooms.csproj --module Bookings --name GetBookingSummary --aggregate Booking
```

It creates `src/Rooms/Features/Bookings/GetBookingSummary/GetBookingSummary.cs`, with the query `GetBookingSummaryQuery`, its DTO `GetBookingSummaryDto`, its handler and its endpoint `GET /bookings/get-booking-summary/{id}`, and the integration tests in `tests/Rooms.IntegrationTests/Features/Bookings/GetBookingSummary/`, then prints the lines to add to `BookingsModule.cs`. The handler reads `Set<Booking>()` with `AsNoTracking()` and projects it into the DTO with `Select`, so HS006 has nothing to report. By default it reads one DTO by its id, an unknown id being answered with 404. `--result many` reads the list instead.

`--aggregate` is required: it names a type of the namespace `Rooms.Domain.Bookings` whose `Id` is an `<Aggregate>Id` that wraps a `Guid Value`, as `Booking` and `BookingId` do. An aggregate that does not exist fails the build with a compiler error that names it.

The handler works as generated: with one DTO by its id, the test of an unknown id passes. The test that reads or lists a stored aggregate fails with `TODO(hardslice):` until it creates the aggregate through its factory method and asserts the values the DTO reads.

## A domain event and its reaction

```text
dotnet new hardslice-event --project src/Rooms/Rooms.csproj --module Bookings --name BookingExtended --reaction Payments/InvoiceOnBookingExtended
```

It creates the event `BookingExtended`, a `sealed record` that implements `IDomainEvent` with a `Guid Id` to replace by the values of the fact, in `src/Rooms.Domain/Bookings/Events/`. With `--reaction <Module>/<Slice>`, it also creates the reaction slice `src/Rooms/Features/Payments/InvoiceOnBookingExtended/`, whose only type is the `internal` handler `InvoiceOnBookingExtendedHandler`, an `IDomainEventHandler<BookingExtended>`, and its integration tests in `tests/Rooms.IntegrationTests/Features/Payments/InvoiceOnBookingExtended/`, then prints the usings and the registration to add to `PaymentsModule.cs`. The reaction may live in another module than the event: it depends on the domain only. It runs inside the transaction of the command that raised the event, so it changes aggregates and never calls an external service.

Without `--reaction`, it prints how to raise the event from the aggregate instead. With `--reaction-only` too, it creates the reaction slice alone, for an event that already exists, and leaves the event as it is. It never edits the aggregate: the method where the fact happens adds the event to its list.

The handler throws `NotImplementedException`, and its test, which calls it with an event as the pipeline does, then saves, ends with `TODO(hardslice):`. A reaction its module does not register fails the check twice: its test, and the test of the host, which also resolves every domain event handler. A name that HS019 does not read as a past fact, such as `ExtendBooking`, is created with a warning.

Without `--project`, before the first restore, with a name or a module that is not PascalCase, for a module without `Features/<Module>/<Module>Module.cs` (the module of the event or of the reaction), with a reaction that is not `<Module>/<Slice>` in PascalCase, ends with `Handler` or is named as the event, or with `--reaction-only` without `--reaction`, it exits with an error and creates nothing. It refuses to overwrite an existing event without `--force`.

## Limitations

- The item templates read the application and its modules through `--project` only, once the web project is restored: `dotnet new` evaluates no project from the root of a solution, and cannot read a file before it creates the others.
- A refusal prints `The template ... was created successfully.` before `Post action failed.` and its error, and exits with code 105: nothing is created.
- They only print the registrations of the module, of the slice or of the reaction, which you or the agent add to `Program.cs` or to the module. `hardslice-aggregate` prints the migration to add, and `hardslice-event` how to raise the event.
- `hardslice-module` puts a `.gitkeep` in the folders it creates without file, as `dotnet new` creates no empty folder. With `--force`, it overwrites the contract of an existing module, its registrations included.
- The tests that `hardslice-command` and `hardslice-query` generate call the route of the group in kebab-case: after `--route`, change theirs.
- `hardslice-query` cannot check the aggregate: the compiler does. Its list result is `--result many`, not `list`, which `dotnet new` would read as its own `list` command.
- `hardslice-event --reaction-only` cannot check that the event exists: the compiler does, and the test it generates leaves the building of the event to complete.
