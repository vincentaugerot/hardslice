using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using static HardSlice.Analyzers.Tests.AnalyzerHarness;

namespace HardSlice.Analyzers.Tests;

// HS009: a domain event is a record.
public sealed class DomainEventRecordAnalyzerTests
{
    private const string Configuration =
        """
        {
          "style": "slices",
          "contracts": {
            "entity": "MyApp.Domain.IEntity, MyApp.Domain",
            "aggregateRoot": "MyApp.Domain.IAggregateRoot, MyApp.Domain",
            "domainEvent": "MyApp.Domain.IDomainEvent, MyApp.Domain",
            "command": "MyApp.Shared.Messaging.ICommand`1, MyApp",
            "query": "MyApp.Shared.Messaging.IQuery`1, MyApp",
            "requestHandler": "MyApp.Shared.Messaging.IRequestHandler`2, MyApp",
            "domainEventHandler": "MyApp.Shared.Messaging.IDomainEventHandler`1, MyApp"
          },
          "shared": {
            "allowedFolders": ["Messaging", "Persistence", "Behaviors"]
          }
        }
        """;

    private const string Contracts =
        """
        namespace MyApp.Domain
        {
            public interface IEntity;

            public interface IAggregateRoot : IEntity;

            public interface IDomainEvent;
        }

        namespace MyApp.Domain.Bookings
        {
            public readonly record struct BookingId(Guid Value);
        }
        """;

    // The domain, with its contracts, as the rule sees it in its own compilation.
    private static Task<ImmutableArray<Diagnostic>> VerifyAsync(params string[] markedSources) =>
        AnalyzerHarness.VerifyAsync(
            new DomainEventRecordAnalyzer(),
            DomainEventRecordAnalyzer.DiagnosticId,
            "MyApp.Domain",
            Configuration,
            [],
            OutputKind.DynamicallyLinkedLibrary,
            [.. markedSources, Contracts]);

    private static CSharpCompilation CompileDomain(params (string Path, string Source)[] files) =>
        Compile("MyApp.Domain", [.. files, ("Contracts.cs", Contracts), ("Usings.cs", ImplicitUsings)]);

    // The first sentence of each message, and the declaration its fix proposes.
    private static IEnumerable<string> Subjects(ImmutableArray<Diagnostic> diagnostics) =>
        diagnostics.Select(diagnostic => diagnostic.GetMessage().Split(". ")[0]);

    private static IEnumerable<string> Fixes(ImmutableArray<Diagnostic> diagnostics) =>
        diagnostics.Select(diagnostic => diagnostic.GetMessage().Split(" Fix: ")[1].Split(". Skill: ")[0]);

    [Fact]
    public async Task Reports_a_sealed_class_that_implements_the_domain_event_contract()
    {
        var diagnostics = await VerifyAsync(
            """
            namespace MyApp.Domain.Bookings.Events;

            public sealed class [|BookingConfirmed|] : IDomainEvent
            {
                public BookingConfirmed(BookingId bookingId) => BookingId = bookingId;

                public BookingId BookingId { get; }
            }
            """);

        diagnostics.ShouldHaveSingleItem().GetMessage().ShouldBe(
            "domain event 'BookingConfirmed' is a class. Domain events are records. "
            + "Fix: declare it as 'public sealed record BookingConfirmed(...) : IDomainEvent'. Skill: add-domain-event. Doc: 'docs/rules/HS009.md'.");
        diagnostics[0].Severity.ShouldBe(DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task Reports_a_class_that_implements_a_derived_event_interface_and_keeps_its_base_list()
    {
        var diagnostics = await VerifyAsync(
            """
            namespace MyApp.Domain.Bookings.Events;

            public interface IBookingEvent : IDomainEvent
            {
                BookingId BookingId { get; }
            }

            public sealed class [|BookingCancelled|] : IBookingEvent, IEquatable<BookingCancelled>
            {
                public BookingId BookingId { get; init; }

                public bool Equals(BookingCancelled? other) => other?.BookingId == BookingId;
            }
            """);

        Fixes(diagnostics).ShouldBe(["declare it as 'public sealed record BookingCancelled(...) : IBookingEvent, IEquatable<BookingCancelled>'"]);
    }

    // A base event that is a class makes its derived events classes too: each one is reported, with its own base.
    [Fact]
    public async Task Reports_an_abstract_base_class_and_the_events_that_derive_from_it()
    {
        var diagnostics = await VerifyAsync(
            """
            namespace MyApp.Domain.Bookings.Events;

            public abstract class [|BookingEvent|] : IDomainEvent
            {
                public BookingId BookingId { get; init; }
            }

            public class [|BookingConfirmed|] : BookingEvent;

            internal sealed partial class [|BookingArchived|] : BookingEvent;

            internal sealed partial class BookingArchived;
            """);

        Subjects(diagnostics).ShouldBe(
        [
            "domain event 'BookingEvent' is a class",
            "domain event 'BookingConfirmed' is a class",
            "domain event 'BookingArchived' is a class"
        ]);
        Fixes(diagnostics).ShouldBe(
        [
            "declare it as 'public abstract record BookingEvent(...) : IDomainEvent'",
            "declare it as 'public record BookingConfirmed(...) : BookingEvent'",
            "declare it as 'internal sealed partial record BookingArchived(...) : BookingEvent'"
        ]);
    }

    [Fact]
    public async Task Reports_a_struct_that_is_not_a_record_struct()
    {
        var diagnostics = await VerifyAsync(
            """
            namespace MyApp.Domain.Bookings.Events;

            public readonly struct [|BookingConfirmed|] : IDomainEvent
            {
                public BookingId BookingId { get; init; }
            }

            public struct [|BookingCancelled|] : IDomainEvent;
            """);

        Subjects(diagnostics).ShouldBe(["domain event 'BookingConfirmed' is a struct", "domain event 'BookingCancelled' is a struct"]);
        Fixes(diagnostics).ShouldBe(
        [
            "declare it as 'public readonly record struct BookingConfirmed(...) : IDomainEvent'",
            "declare it as 'public record struct BookingCancelled(...) : IDomainEvent'"
        ]);
    }

    [Fact]
    public async Task Reports_generic_and_nested_events_by_their_definition()
    {
        var diagnostics = await VerifyAsync(
            """
            namespace MyApp.Domain.Events;

            public sealed class [|EntityChanged|]<TEntity>(TEntity entity) : IDomainEvent where TEntity : IEntity
            {
                public TEntity Entity { get; } = entity;
            }

            public static class BookingEvents
            {
                public sealed class [|Confirmed|] : IDomainEvent;
            }
            """);

        Subjects(diagnostics).ShouldBe(["domain event 'EntityChanged<TEntity>' is a class", "domain event 'BookingEvents.Confirmed' is a class"]);
        Fixes(diagnostics).ShouldBe(
        [
            "declare it as 'public sealed record EntityChanged<TEntity>(...) : IDomainEvent'",
            "declare it as 'public sealed record Confirmed(...) : IDomainEvent'"
        ]);
    }

    [Fact]
    public async Task Accepts_records_record_structs_an_abstract_base_record_and_generic_records()
    {
        await VerifyAsync(
            """
            namespace MyApp.Domain.Bookings.Events;

            public sealed record BookingConfirmed(BookingId BookingId) : IDomainEvent;

            public abstract record BookingEvent(BookingId BookingId) : IDomainEvent;

            public sealed record BookingCancelled(BookingId BookingId, string Reason) : BookingEvent(BookingId);

            public readonly record struct BookingPaid(BookingId BookingId, decimal Amount) : IDomainEvent;

            public record struct BookingArchived(BookingId BookingId) : IDomainEvent;

            public sealed record EntityChanged<TEntity>(TEntity Entity) : IDomainEvent where TEntity : IEntity;

            // A record is accepted even with a public setter: HS009 checks the shape, not immutability.
            public sealed record BookingMoved : IDomainEvent
            {
                public BookingId BookingId { get; set; }
            }
            """);
    }

    [Fact]
    public async Task Accepts_event_interfaces_and_classes_that_are_not_events()
    {
        await VerifyAsync(
            """
            namespace MyApp.Domain.Bookings;

            public interface IBookingEvent : IDomainEvent;

            public sealed class Booking : IAggregateRoot
            {
                private readonly List<IDomainEvent> _domainEvents = [];

                public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;
            }

            public sealed class BookingPolicy;

            public enum BookingStatus { Confirmed, Cancelled }
            """);
    }

    // HS901 reports a type that is both an entity and a domain event: HS009 does not add a second, contradictory fix.
    [Fact]
    public async Task Leaves_a_type_that_is_also_an_entity_to_HS901()
    {
        var domain = CompileDomain(("BookingSnapshot.cs", "namespace MyApp.Domain.Bookings; public sealed class BookingSnapshot : IAggregateRoot, IDomainEvent;"));

        (await AnalyzeAllAsync(domain, Configuration)).Select(diagnostic => diagnostic.Id).ShouldBe([ClassificationAnalyzer.AmbiguityId]);
    }

    // A domain event declared in the application is checked in the compilation of the application, the contract read in
    // the metadata of the domain or from its project.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Reports_an_event_declared_outside_the_domain(bool asMetadata)
    {
        var domain = CompileDomain(("Booking.cs", "namespace MyApp.Domain.Bookings; public interface IBookingEvent : IDomainEvent;"));

        var diagnostics = await AnalyzerHarness.VerifyAsync(
            new DomainEventRecordAnalyzer(),
            DomainEventRecordAnalyzer.DiagnosticId,
            "MyApp",
            Configuration,
            [Reference(domain, asMetadata)],
            OutputKind.DynamicallyLinkedLibrary,
            """
            namespace MyApp.Features.Bookings;

            public sealed class [|BookingReminderSent|] : MyApp.Domain.Bookings.IBookingEvent;

            public sealed record BookingConfirmed : MyApp.Domain.IDomainEvent;
            """);

        Fixes(diagnostics).ShouldBe(["declare it as 'public sealed record BookingReminderSent(...) : IBookingEvent'"]);
    }

    // The events of a referenced assembly are checked in their own compilation, not in the one that uses them.
    [Fact]
    public async Task Does_not_report_the_events_of_a_referenced_domain()
    {
        var domain = CompileDomain(("BookingConfirmed.cs", "namespace MyApp.Domain.Bookings; public sealed class BookingConfirmed : IDomainEvent;"));

        (await AnalyzeAllAsync(domain, Configuration)).Select(diagnostic => diagnostic.Id).ShouldBe([DomainEventRecordAnalyzer.DiagnosticId]);

        var application = Compile(
            "MyApp",
            [("Events.cs", "namespace MyApp; public static class Events { public static object Raise() => new MyApp.Domain.Bookings.BookingConfirmed(); }"), ("Usings.cs", ImplicitUsings)],
            [Reference(domain, asMetadata: true)]);
        var diagnostics = await application
            .WithAnalyzers([new DomainEventRecordAnalyzer()], Options(Configuration))
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Reports_generated_code()
    {
        await VerifyAsync(
            """
            // <auto-generated/>
            namespace MyApp.Domain.Bookings.Events;

            public sealed class [|BookingConfirmed|] : IDomainEvent;
            """);
    }

    // Without its domainEvent contract, HS900 reports the configuration and HS009 recognizes no event.
    [Fact]
    public async Task Stays_silent_without_its_domain_event_contract()
    {
        var configuration = Configuration.Replace("    \"domainEvent\": \"MyApp.Domain.IDomainEvent, MyApp.Domain\",\n", "");
        configuration.ShouldNotBe(Configuration);
        var domain = CompileDomain(("BookingConfirmed.cs", "namespace MyApp.Domain.Bookings; public sealed class BookingConfirmed : IDomainEvent;"));

        var diagnostic = (await AnalyzeAllAsync(domain, configuration)).ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(ClassificationAnalyzer.ConfigurationId);
        diagnostic.GetMessage().ShouldContain("classify the types checked by HS009 and HS901.");
    }

    [Fact]
    public async Task Does_not_apply_to_test_projects()
    {
        var domain = CompileDomain(("BookingConfirmed.cs", "namespace MyApp.Domain.Bookings; public sealed class BookingConfirmed : IDomainEvent;"));

        (await AnalyzeAllAsync(domain, Configuration, isTestProject: true)).ShouldBeEmpty();
    }
}
