---
name: add-slice
description: Adds a use case to a module of this HardSlice solution, a command that changes state, a system command that only the application sends, or a query that only reads, by generating its slice and its tests with dotnet new hardslice-command or hardslice-query, putting its business rule in the aggregate, registering it, filling its logic and its tests, then running the check. Use when asked to add a use case, an endpoint, a command, a job, a query or a feature to an existing module, such as renaming a product or listing the products of a category.
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
     dotnet new hardslice-command --project src/MyApp/MyApp.csproj --module <Module> --name <Slice> --route <route>
     ```

   - the use case is sent by the application only, never by an HTTP request, such as a job or the reaction of a hosted service to an outside event: a system command, with `--endpoint none`. It implements `ISystemCommand<TResponse>`, has no endpoint, and its integration tests send it through `IMediator`. It takes `--parameters` and `--response` as a command does, never `--route` nor `--method`, which are refused.

     ```text
     dotnet new hardslice-command --project src/MyApp/MyApp.csproj --module <Module> --name <Slice> --endpoint none
     ```

   - the use case runs at regular intervals, such as collecting a feed every 30 seconds or purging old rows every night: a recurring system command, with `--every <interval>`, the interval as `[d.]hh:mm:ss`, longer than zero and under 49 days. It implies `--endpoint none` and `--parameters none`, and is refused with `--parameters id` or `--endpoint http`: a new command without parameter is sent at each interval. The template prints its registration, `AddRecurringCommand<TCommand, TResponse>("<Module>:<Slice>", interval)`, whose interval the key `<Module>:<Slice>:Interval` of the configuration replaces, and its integration test runs three cycles on a `FakeTimeProvider`.

     ```text
     dotnet new hardslice-command --project src/MyApp/MyApp.csproj --module <Module> --name <Slice> --every 00:00:30
     ```

   - the use case only reads: a query of one DTO by its identifier, or of the list with `--result many`, with its integration tests.

     ```text
     dotnet new hardslice-query --project src/MyApp/MyApp.csproj --module <Module> --name <Slice> --aggregate <Aggregate> --route <route>
     ```

   Always pass `--route` to a slice with an endpoint, the route of the endpoint under the group of the module, named after the resource it creates, changes or reads, never after the use case: `ImportSchedule` posts to `--route versions`, not to `import-schedule`, its default. Use lowercase words separated by hyphens, and route parameters between braces separated by slashes, such as `{productId:guid}/reviews`; `--route /` maps the group itself. A query of one DTO, and a DELETE, append `/{id:guid}`, the identifier they read. Two slices of a module never share a method and a route: read the routes its endpoints already map first. Add `--method` to a command that is not a `POST`: `PUT` when it replaces the resource, `PATCH` when it changes part of it, `DELETE` when it removes it, which reads its `Id` from the route. A query answers `GET` only.

   If the template refuses, read its message, fix the argument and run it again: a refusal prints `created successfully` first, then fails with exit code 105 and creates nothing.
5. Register the slice with the lines the template printed, in `src/MyApp/Features/<Module>/<Module>Module.cs`: the usings the file does not have yet, the registrations by contract in `Add<Module>Module`, and `<Slice>Endpoint.Map(group);` in `Map<Module>Module`, except for a system command, which has no endpoint. A forgotten registration fails `ProgramTests`, which names the type.
6. Fill the logic in `src/MyApp/Features/<Module>/<Slice>/<Slice>.cs`:
   - command: replace the `Id` field with the fields of the use case, except for a `DELETE`, whose endpoint reads it from the route; the validator checks only their shape (required, format, length); the handler injects `AppDbContext`, loads the aggregate with `Set<TAggregate>()` (or adds a new one), calls the method of the aggregate and returns the response. It never calls `SaveChangesAsync`: the pipeline saves and commits the transaction. It injects `TimeProvider` when the domain needs the current instant;
   - command without parameters, such as an import that reads its source from the configuration: generated with `--parameters none`, it is an empty record, `public sealed record <Slice>Command : ICommand<TResponse>;`, without validator nor validator tests, and the template prints the registration of its handler alone. Never add a validator to it: an empty one is forbidden. The endpoint reads no body and sends `new <Slice>Command()`, and its integration test posts to its route without a body. A command generated without the option that turns out to take nothing from the request: delete its slice folder and its two test folders, then generate it again with `--parameters none`;
   - system command: a type of its module other than its handler and its validator sends it, or HS004 fails the build, which also refuses an endpoint for it. Background work: a periodic task is a recurring command, generated with `--every` and sent by `AddRecurringCommand`, never a hosted service of its own. It sends a new command one interval after the start, then at each interval, one cycle at a time, each in a scope of its own and through the whole pipeline, transaction included; it logs a failed cycle and runs the next one, and waits on the `TimeProvider` of the container. Any other trigger, such as a queue to consume, is a hosted service: declare it in the folder of the slice it triggers, `src/MyApp/Features/<Module>/<Slice>/`, as an `internal sealed` `BackgroundService` registered with `services.AddHostedService<T>()` in `Add<Module>Module`. It creates the command and sends it through `IMediator` in a scope of its own for each message (`IServiceScopeFactory.CreateAsyncScope()`), catches and logs the exception of a message so that it never stops the application, and injects `TimeProvider` for its timers and delays (HS010 refuses `Task.Delay` and `new PeriodicTimer` without it). Its integration tests keep it with `new AppFactory(database).KeepHostedService<T>()`. A manual trigger over HTTP is another use case: a command slice of its own, with its endpoint;
   - command that creates an aggregate under a unique key, such as one row per trip and service day: declare the unique index in its mapping (`HasIndex(...).IsUnique()`) and its migration, and let the index decide. Two requests that create it at the same time, from the endpoint, a recurring command or a second instance of the application, both pass their reads, then the second commit breaks the index: `TransactionBehavior` turns that unique violation into a `ConflictException`, answered with 409 without the values of the key, as for a duplicate created earlier. Its integration test sends the same creation twice and asserts the 409. When the use case has an exception of its own for the duplicate, the handler saves early and catches the violation (`PostgresException` with `SqlState` `PostgresErrorCodes.UniqueViolation`, inside the `DbUpdateException`) to throw it: the exception of the handler wins. A handler that must read before it creates without ever conflicting, such as one that merges into what a concurrent run created, takes a transaction lock before its read, `await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({LockKey})", cancellationToken);` with a `long` constant of the handler, released by the commit or the rollback of the command. It serializes every run of the command: keep it for that case, the index and the 409 cover the others;
   - command that reads from outside, such as a download or a file: the handler injects what it needs, registered in `Add<Module>Module`, such as `IHttpClientFactory` (`services.AddHttpClient()`) and the options of the module, bound from its section of the configuration. Never a static `HttpClient` nor a path written in the code. An effect it has outside, such as a file it writes, survives a rollback of the command: make it harmless then, such as a file named after the hash of its content, which the next run replaces or reuses. An effect that a rollback must cancel, such as an e-mail, a payment or a message to another system, has no mechanism in this solution yet: stop and tell the user instead of writing it;
   - query: add the values the use case reads to the DTO and project them in `Select`. Keep `AsNoTracking()`: a query never loads an aggregate to change it and never saves. Add a parameter, and its validator, only for a filter;
   - endpoint: keep the route and the HTTP method that the template mapped, on the group of the module, sending the request through `IMediator` and answering with `TypedResults`. A route parameter of `--route` other than the `{id:guid}` of the template is yours to bind: add it to the delegate of the endpoint and to the request, and give it a value in `Route` of the generated tests, whose `TODO(hardslice):` marker says so.
7. Replace every `TODO(hardslice):` marker of the slice and of its tests: search for `TODO(hardslice):` under `src/` and `tests/`. The integration tests assert the business outcome, such as the state stored in PostgreSQL, read back from the database, the 400 of an invalid request and the 404 of an unknown identifier; the validator tests assert each refused input. A system command has no route: its integration tests send it through `IMediator`, and an invalid one throws `ValidationException`. A recurring command is tested through its cycles: `RecurringCommandCycles<TCommand, TResponse>` keeps its service in that test only and advances a `FakeTimeProvider` by its interval at each `RunNextAsync()`, which returns the response of the cycle or throws its exception; assert what the cycles changed, such as the rows they stored. A command without parameters has no invalid request: its integration tests assert its success, and a failure of the domain when one can happen, such as the 409 of a state that forbids it. An empty test, or one that asserts nothing, does not replace a marker. The generated tests call the route of the slice under `<Module>Module.Route`, the group of the module.
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
