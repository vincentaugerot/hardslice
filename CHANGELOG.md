# Changelog

Every version of the three packages, `HardSlice.Templates`, `HardSlice.Analyzers` and `HardSlice.Tool`, which always share one version. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). While HardSlice is in preview, a version may change its rules, its template and its commands, but a solution keeps the template and the versions it was generated with.

## [0.3.0-preview.1] - unreleased

The first preview published on NuGet.org. No earlier version was published.

### Added

- `hardslice-api`, a solution template: an ASP.NET Core API in vertical slices around a separate domain project, with PostgreSQL and EF Core, a small in-house mediator, FluentValidation, unit, integration and architecture tests, and a `Bookings` sample module, or none with `--no-sample`. Any name accepted by `dotnet new` becomes one safe .NET namespace (`my-app` becomes `my_app`).
- Six item templates, `hardslice-module`, `hardslice-aggregate`, `hardslice-entity`, `hardslice-command`, `hardslice-query` and `hardslice-event`, which generate an element in its place with its tests and print the registrations to add. Their tests fail with a `TODO(hardslice):` marker until the element is completed.
- `HardSlice.Analyzers`, 16 rules reported at build time and in the IDE: HS000 (suppressions without justification), HS003 to HS011, HS015, HS016, HS019 (a suggestion) and the configuration diagnostics HS900 to HS902. Three analysis modes, `Minimum`, `Recommended` and `All`, choose which rules are on.
- Seven architecture tests in every generated solution: HS001, HS012 to HS014 and HS016 to HS018.
- `HardSlice.Tool`, the local tool `dotnet hardslice`, pinned to the version of the analyzers: `check` (exit codes `0`, `1` and `2`), `check --build`, `explain <rule>`, `hook <cli> <event>` and `protected`.
- The agent kit of every generated solution: `AGENTS.md` and the instruction files of Claude Code, Codex, GitHub Copilot CLI and Gemini CLI, six skills, the hooks of Claude Code, Codex and Copilot CLI (they refuse an edit of a protected file, hand the build errors back after an edit and keep the agent working while the check fails), a pre-commit hook, and a GitHub Actions workflow that runs the check and fails a pull request that changes a protected file without the label `hardslice-approved`.
- A rule sheet for every rule in `docs/rules/`, named by each diagnostic with the skill that fixes it.
- `hardslice.json` declares `"version": 1`, the version of its format.
- The application owns `Directory.Packages.props` and adds its packages there. `HardSlice.Analyzers` is referenced from the protected `Directory.HardSlice.props`, which it imports, and `check` fails when a project no longer receives the package. The test projects reference `Microsoft.Extensions.TimeProvider.Testing`, for `FakeTimeProvider`.
- The web project has its own `UserSecretsId`, so `dotnet user-secrets set` needs no init. `AppFactory` runs the integration tests in the `IntegrationTests` environment, which reads no user secret, and starts no hosted service of the application unless the test keeps it with `KeepHostedService<T>()`.
- The agent kit says that a command without parameters has no validator and tests a failure of the domain, or its success, instead of an invalid request, and the fix of HS014 says so for such a command. It says that a command handler may read from outside through the services of its module, such as `IHttpClientFactory`, and that an effect it has outside must stay harmless if its transaction rolls back.
- `hardslice-command --parameters none` generates a command without parameters: an empty record, an endpoint that reads no body, no validator nor validator tests, and an integration test of its success instead of a 400. It refuses `--response none`, whose 404 answers an aggregate that such a command cannot name.
- `hardslice-command` and `hardslice-query` take `--route`, the route of the endpoint under the group of its module (route parameters included, `/` for the group itself), and `--method`: `POST`, `PUT`, `PATCH` or `DELETE` for a command, whose `DELETE` reads its `Id` from the route, `GET` for a query. The contract of a module holds the route of its group in its constant `Route`, under which the generated tests call their slice.
- `hardslice-entity` generates a child entity of an aggregate: the entity, its identifier, its mapping next to the mapping of its root and its unit tests, with the converter of the identifier of the root, created with the first child and reused by the next ones. `--link collection`, by default, holds the child in a collection of the root, loaded and saved with it. `--link id` gives the child the identifier of its root instead, without navigation, for a collection too large to load with its root.
- A system command, `ISystemCommand<TResponse>`, which derives from `ICommand<TResponse>`, for a command that only the application sends, such as a job: it goes through the pipeline of every command, and HS004 requires no endpoint for it but refuses one, an HTTP mapping that sends it, and a system command that no type of its module sends. Its contract is the optional key `systemCommand` of `hardslice.json`, which the template writes, and HS900 reports one that does not derive from `command`. `hardslice-command --endpoint none` generates it, without endpoint, with integration tests that send it through `IMediator`, and refuses `--route` and `--method` with it.
- `services.AddRecurringCommand<TCommand, TResponse>(section, interval)`, in `Shared/Messaging`, sends a system command without parameter at regular intervals, one cycle at a time, each in a scope of its own, on the `TimeProvider` of the container: a failed cycle is logged and the next one runs. The key `<section>:Interval` of the configuration replaces the interval. `hardslice-command --every <interval>` generates such a command and prints its registration, with an integration test that runs three cycles through `RecurringCommandCycles` on a `FakeTimeProvider`. `AGENTS.md` and `add-slice` say where background work goes.
- HS010 reports `Task.Delay` and `new PeriodicTimer` without a `TimeProvider` in a slice.

### Known limitations

- Only solutions generated by `hardslice-api` are supported. Adoption of an existing solution is planned for v0.5.
- PostgreSQL is the only database. Domain events are handled inside the transaction of the command, without outbox: an external effect that a rollback must cancel, such as an e-mail, has no mechanism yet, and the agent kit asks the agent to tell the user instead of writing it.
- Gemini CLI stays at the fallback level, without hooks. Codex runs the hooks only once a human approves them in `/hooks`.
- The limits of each rule are listed in [docs/rules.md](docs/rules.md).

[0.3.0-preview.1]: https://github.com/vincentaugerot/hardslice/releases/tag/v0.3.0-preview.1
