# PB-002 review fixes

Status: approved on 29/10/2026.

## Context

The PB-002 implementation passes its nominal acceptance tests, but review found three issues to resolve before committing it: concurrent cancellations can both succeed, Bookings-specific persistence types are placed in the transverse `Shared` area, and the Domain independence test does not detect declared but unused runtime dependencies.

## Selected approach

- Keep the cancellation invariant in `Booking`. Protect the check-and-transition in `Cancel()` with an aggregate-local lock so the shared in-memory instance permits exactly one successful cancellation.
- Add a concurrent handler test that starts competing cancellations together and proves that one succeeds while the other reports the domain violation.
- Move `IBookingStore` and `InMemoryBookingStore` to the `MyApp.Features.Bookings` module root. The two slices consume a module-level contract instead of using `Shared` as a Bookings-specific common area.
- Copy `MyApp.Domain.csproj` into the architecture-test output as a test input. Parse it as XML and reject explicit `PackageReference`, `ProjectReference`, `FrameworkReference`, and assembly `Reference` items. Keep the existing compiled-assembly check as a second layer.
- Leave the verified `content/content/hardslice-api` package layout issue to PB-005, where template packaging and `template.json` are accepted end to end.

## Alternatives rejected

- A store-level `TryCancel` operation would move a domain state transition into persistence.
- An immutable aggregate with `ConcurrentDictionary.TryUpdate` would add unnecessary complexity for the PB-002 in-memory implementation.
- Inspecting only assembly metadata or only the generated `.deps.json` would leave gaps between declared and emitted dependencies.
- Keeping Bookings-specific types in `Shared/Persistence` would teach a pattern that conflicts with the requirement that `Shared` remain transverse.

## Validation

Run the new concurrency test repeatedly, then run `dotnet restore`, `dotnet build --no-restore`, and `dotnet test --no-build` from the repository root. Verify the standalone sample solution, run the NuGet vulnerability audit, and confirm the final diff contains no unrelated changes.
