---
name: extract-to-domain
description: Moves a business rule out of the slices of this HardSlice solution into its aggregate, when two slices share or copy the rule, or when a slice sets the state of an entity itself, by adding a method with its unit tests to the aggregate, making each slice call it, then running the check. Use when a message names `Skill: extract-to-domain` (HS007, HS016), when a slice uses a type of another slice, when asked to remove a rule duplicated in two use cases, such as two commands that both check whether an order can still change, or when a query needs a business rule that must run in the database.
---

# Extract a rule to the domain

A business rule lives in its aggregate, in `src/MyApp.Domain/<Module>/`, as the golden rules of `AGENTS.md` say: a method named after the business operation checks the rule and changes the state, and every slice that needs the rule calls that method. A rule written in a handler belongs to that slice only: a second slice that reuses it uses a type of another slice (HS016), and a slice that sets the state of an entity itself bypasses the rules of the entity (HS007).

Run every command from the root of the solution, the folder of `MyApp.slnx`.

## Steps

1. Read the message: HS016 names the type of your slice and the type of the other slice it uses; HS007 names the entity and the member written from outside. When HS016 says that a handler, a validator or an endpoint is accessible outside the assembly, declare it `internal`, run the check and stop there: nothing moves.
2. Read the slices involved, in `src/MyApp/Features/<Module>/<Slice>/`, and find what is shared, then use the matching path:
   - a condition, a computation or a change of state that both slices need, or that one slice writes into an entity: it is a business rule, continue with step 3;
   - a condition or a selection that a query must run in the database, such as the orders still open on a date: it is a business rule too, continue with step 3 and write it as step 4 says for a query;
   - something one slice does after another, such as updating another aggregate when a fact happens: use the `add-domain-event` skill instead;
   - a port to an external service that several slices of a module need: declare it in the public contract of the module, a type declared directly in the namespace `MyApp.Features.<Module>`, then run the check;
   - a request or a response that looks the same: each slice declares its own, then run the check.
3. Read the aggregate that the rule protects, in `src/MyApp.Domain/<Module>/`, and its unit tests in `tests/MyApp.Tests/Domain/<Module>/`. If it already has a method that owns the rule, go to step 5.
4. Move the rule into the aggregate: a public method named after the business operation, such as `Cancel` or `Reschedule`, never `SetStatus`. It checks the rule and changes the state, through private setters only. A broken rule throws a `DomainException`, answered with 400; a rule that the current state breaks throws a `ConflictException`, answered with 409. The current instant, or a value of another aggregate, comes as a parameter, loaded by the handler. Add its unit tests to the tests of the aggregate: for each rule, one accepted case that asserts the new state, and one refused case that asserts the exception.

   A rule that a query runs in the database is a static member of the aggregate instead, in one of two forms: an `Expression<Func<T, bool>>` that the handler passes to `Where`, or a method from `IQueryable<T>` to `IQueryable<TResult>` that receives every query it composes as a parameter and only adds operators to them. It never creates its source, never reads and never references EF Core. Its unit tests call it on lists, through `AsQueryable()`: one element the rule keeps and one it drops, for each condition.
5. Make each slice call the method of the aggregate: its handler loads the aggregate with `Set<TAggregate>()`, calls the method and returns its response. A query handler passes its sources to a rule for a query, each one started with `AsNoTracking()`, then projects the result to a DTO of values and runs it, as the section A rule of the domain in a query of `docs/rules/HS006.md` shows. Remove the copy of the rule from each slice, and delete the type that the other slice used once no slice uses it. The request, the validator and the endpoint of each slice stay as they are.
6. Keep the integration tests of both slices: they describe the behavior, which does not change, and they still pass as they are. A unit test of a type you deleted becomes a unit test of the aggregate.
7. Run the check, and fix the code until it exits 0:

   ```text
   bash scripts/check.sh     # macOS, Linux, Git Bash
   pwsh scripts/check.ps1    # Windows, PowerShell
   ```

## Never

These are the workarounds of [Forbidden](../../../AGENTS.md#forbidden); if the check cannot pass without one, stop and explain the conflict instead.

- Copy the rule into each slice: it would then exist twice, and change in one place only.
- Make a handler, a validator or an endpoint `public`, move a type of a slice into the namespace of its module or into `Shared`, or change a namespace so it no longer matches its folder.
- Reach the other slice by reflection, `dynamic`, the service provider or its HTTP endpoint.
- Give the aggregate a public setter, an `init` accessor or a method that only assigns its parameter.
- Write a rule for a query that creates its source itself, takes the context or returns entities already read: HS006 cannot see such a read.
- Delete or weaken a test that describes the rule.
- Suppress a HardSlice diagnostic, lower its severity or turn the analyzers off.
- Create, edit or delete a protected file: the list of [Forbidden](../../../AGENTS.md#forbidden) names them all.
