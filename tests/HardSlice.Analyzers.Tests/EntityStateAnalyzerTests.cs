using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using static HardSlice.Analyzers.Tests.AnalyzerHarness;

namespace HardSlice.Analyzers.Tests;

// HS007: the state of an entity is never written from outside its assembly.
public sealed class EntityStateAnalyzerTests
{
    private const string Rule = "The state of an entity changes only through its methods, which protect its invariants.";

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
            public enum BookingStatus { Confirmed, Cancelled }
        }
        """;

    // The domain, with its contracts, as the rule sees it in its own compilation.
    private static Task<ImmutableArray<Diagnostic>> VerifyAsync(params string[] markedSources) =>
        VerifyAsync([], markedSources);

    private static Task<ImmutableArray<Diagnostic>> VerifyAsync(MetadataReference[] references, params string[] markedSources) =>
        AnalyzerHarness.VerifyAsync(
            new EntityStateAnalyzer(),
            EntityStateAnalyzer.DiagnosticId,
            "MyApp.Domain",
            Configuration,
            references,
            OutputKind.DynamicallyLinkedLibrary,
            [.. markedSources, Contracts]);

    private static CSharpCompilation CompileContracts() =>
        Compile("MyApp.Domain", [("Contracts.cs", Contracts), ("Usings.cs", ImplicitUsings)]);

    // The first sentence of each message, without its rule and its fix.
    private static IEnumerable<string> Subjects(ImmutableArray<Diagnostic> diagnostics) =>
        diagnostics.Select(diagnostic => diagnostic.GetMessage().Split(". ")[0]);

    private static IEnumerable<string> Fixes(ImmutableArray<Diagnostic> diagnostics) =>
        diagnostics.Select(diagnostic => diagnostic.GetMessage().Split(" Fix: ")[1].Split(". Skill: ")[0]);

    [Fact]
    public async Task Reports_a_public_setter_of_an_aggregate_root()
    {
        var diagnostics = await VerifyAsync(
            """
            namespace MyApp.Domain.Bookings;

            public sealed class Booking : IAggregateRoot
            {
                public BookingStatus Status { get; [|set|]; }

                public void Cancel() => Status = BookingStatus.Cancelled;
            }
            """);

        diagnostics.ShouldHaveSingleItem().GetMessage().ShouldBe(
            "entity 'Booking' exposes 'Status' with a public setter. " + Rule
            + " Fix: make the setter private and change the state in a domain method, such as 'Booking.Cancel()'. Skill: extract-to-domain. Doc: 'docs/rules/HS007.md'.");
    }

    [Fact]
    public async Task Reports_a_setter_that_implements_a_public_interface_explicitly()
    {
        var diagnostics = await VerifyAsync(
            """
            namespace MyApp.Domain.Bookings;

            public interface IEditableBooking
            {
                BookingStatus Status { get; set; }
            }

            public sealed class Booking : IAggregateRoot, IEditableBooking
            {
                private BookingStatus _status;

                public BookingStatus Status => _status;

                BookingStatus IEditableBooking.Status { get => _status; [|set|] => _status = value; }

                public void Cancel() => _status = BookingStatus.Cancelled;
            }
            """);

        diagnostics.ShouldHaveSingleItem().GetMessage().ShouldBe(
            "entity 'Booking' lets 'IEditableBooking.Status' be set from outside. " + Rule
            + " Fix: remove the setter from the public interface and change the state in a domain method, such as 'Booking.Cancel()'. Skill: extract-to-domain. Doc: 'docs/rules/HS007.md'.");
    }

    [Fact]
    public async Task Reports_a_public_init_accessor_and_a_public_field_that_is_not_readonly()
    {
        var diagnostics = await VerifyAsync(
            """
            namespace MyApp.Domain.Bookings;

            public sealed class Booking : IAggregateRoot
            {
                public Guid RoomId { get; [|init|]; }

                public DateTimeOffset? [|CancelledAt|];

                public void Cancel(DateTimeOffset now) => CancelledAt = now;
            }
            """);

        Subjects(diagnostics).ShouldBe(
        [
            "entity 'Booking' exposes 'RoomId' with a public init accessor",
            "entity 'Booking' exposes 'CancelledAt' as a public field that is not readonly"
        ]);
        Fixes(diagnostics).ShouldBe(
        [
            "make the init accessor private and set the value in a constructor or a domain method, such as 'Booking.Cancel(...)'",
            "make the field private and change the state in a domain method, such as 'Booking.Cancel(...)'"
        ]);
    }

    [Fact]
    public async Task Reports_a_child_entity_and_the_positional_properties_of_a_record_entity()
    {
        var diagnostics = await VerifyAsync(
            """
            namespace MyApp.Domain.Bookings;

            public sealed class BookingLine : IEntity
            {
                public string Label { get; [|set|]; } = "";
            }

            public sealed record Room(Guid [|Id|], string [|Name|]) : IAggregateRoot;

            public record struct Seat(int [|Number|]) : IEntity;
            """);

        Subjects(diagnostics).ShouldBe(
        [
            "entity 'BookingLine' exposes 'Label' with a public setter",
            "entity 'Room' exposes 'Id', declared by a positional parameter, with a public init accessor",
            "entity 'Room' exposes 'Name', declared by a positional parameter, with a public init accessor",
            "entity 'Seat' exposes 'Number', declared by a positional parameter, with a public setter"
        ]);
        Fixes(diagnostics).ElementAt(1).ShouldBe(
            "declare 'Id' in the body of the record with a private setter, initialized from the parameter, and change the state in a domain method");
    }

    // AuditedEntity is not an entity: what it exposes is the state of the entities derived from it.
    [Fact]
    public async Task Reports_on_the_entity_the_members_inherited_from_a_base_class_that_is_not_an_entity()
    {
        var diagnostics = await VerifyAsync(
            """
            namespace MyApp.Domain.Bookings;

            public abstract class AuditedEntity
            {
                public DateTimeOffset UpdatedAt { get; set; }

                public string? UpdatedBy;
            }

            public sealed class [|[|Booking|]|] : AuditedEntity, IAggregateRoot
            {
                public void Cancel() { }
            }

            public sealed class AuditedNote : AuditedEntity;
            """);

        Subjects(diagnostics).ShouldBe(
        [
            "entity 'Booking' exposes 'UpdatedAt', inherited from 'AuditedEntity', with a public setter",
            "entity 'Booking' exposes 'UpdatedBy', inherited from 'AuditedEntity', as a public field that is not readonly"
        ]);
        Fixes(diagnostics).ShouldBe(
        [
            "make the setter of 'AuditedEntity.UpdatedAt' private or protected and change the state in a domain method, such as 'Booking.Cancel()'",
            "make the field 'AuditedEntity.UpdatedBy' private or protected and change the state in a domain method, such as 'Booking.Cancel()'"
        ]);
    }

    // The generic base is an entity of the same compilation: it reports its own setter, once, and its derived entities do not repeat it.
    [Fact]
    public async Task Reports_a_generic_base_entity_once_on_its_own_declaration()
    {
        var diagnostics = await VerifyAsync(
            """
            namespace MyApp.Domain.Bookings;

            public abstract class Entity<TId> : IEntity
            {
                public TId Id { get; [|set|]; } = default!;
            }

            public sealed class Booking : Entity<Guid>, IAggregateRoot;

            public sealed class BookingLine : Entity<int>;
            """);

        Subjects(diagnostics).ShouldBe(["entity 'Entity<TId>' exposes 'Id' with a public setter"]);
    }

    // Entity<TId> comes from another assembly: the compilation of the derived entity reports what it inherits.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reports_on_the_entity_the_setter_of_a_base_entity_declared_in_another_assembly(bool asMetadata)
    {
        var kernel = Compile(
            "MyApp.Kernel",
            [
                ("Entity.cs", "namespace MyApp.Kernel; public abstract class Entity<TId> : MyApp.Domain.IEntity { public TId Id { get; set; } = default!; }"),
                ("Usings.cs", ImplicitUsings)
            ],
            [Reference(CompileContracts(), asMetadata: true)]);

        var diagnostics = await VerifyAsync(
            [Reference(kernel, asMetadata)],
            """
            namespace MyApp.Domain.Bookings;

            public sealed class [|Booking|] : MyApp.Kernel.Entity<Guid>, IAggregateRoot;
            """);

        Subjects(diagnostics).ShouldBe(["entity 'Booking' exposes 'Id', inherited from 'Entity<TId>', with a public setter"]);
    }

    // The contracts live in another assembly than the entities: the rule runs wherever an entity is declared.
    [Fact]
    public async Task Reports_the_setter_of_a_public_interface_declared_in_another_assembly()
    {
        var contracts = CompileContracts();
        var editing = Compile(
            "MyApp.Editing",
            [("IEditable.cs", "namespace MyApp.Editing; public interface IEditable { string Label { get; set; } }"), ("Usings.cs", ImplicitUsings)]);

        var diagnostics = await AnalyzerHarness.VerifyAsync(
            new EntityStateAnalyzer(),
            EntityStateAnalyzer.DiagnosticId,
            "MyApp.Domain.Bookings",
            Configuration,
            [Reference(contracts, asMetadata: true), Reference(editing, asMetadata: true)],
            OutputKind.DynamicallyLinkedLibrary,
            """
            namespace MyApp.Domain.Bookings;

            public sealed class Booking : MyApp.Domain.IAggregateRoot, MyApp.Editing.IEditable
            {
                private string _label = "";

                string MyApp.Editing.IEditable.Label { get => _label; [|set|] => _label = value; }
            }
            """);

        Subjects(diagnostics).ShouldBe(["entity 'Booking' lets 'IEditable.Label' be set from outside"]);
    }

    // An implicit implementation is a public setter: reported once, as such.
    [Fact]
    public async Task Reports_an_implicit_implementation_of_an_interface_setter_once()
    {
        var diagnostics = await VerifyAsync(
            """
            namespace MyApp.Domain.Bookings;

            public interface IEditableBooking
            {
                BookingStatus Status { get; set; }
            }

            public sealed class Booking : IAggregateRoot, IEditableBooking
            {
                public BookingStatus Status { get; [|set|]; }
            }
            """);

        Subjects(diagnostics).ShouldBe(["entity 'Booking' exposes 'Status' with a public setter"]);
    }

    [Fact]
    public async Task Reports_an_indexer_an_override_and_an_abstract_entity()
    {
        var diagnostics = await VerifyAsync(
            """
            namespace MyApp.Domain.Bookings;

            public abstract class Base
            {
                public virtual string Note { get; set; } = "";
            }

            public abstract class Bookable : Base, IAggregateRoot
            {
                private readonly Dictionary<int, string> _labels = [];

                public string this[int seat] { get => _labels[seat]; [|set|] => _labels[seat] = value; }

                public override string Note { get => base.Note; [|set|] => base.Note = value; }
            }
            """);

        Subjects(diagnostics).ShouldBe(
        [
            "entity 'Bookable' exposes 'this[int]' with a public setter",
            "entity 'Bookable' exposes 'Note' with a public setter"
        ]);
    }

    [Fact]
    public async Task Accepts_the_Booking_aggregate_and_setters_that_stay_inside_the_assembly()
    {
        await VerifyAsync(
            """
            namespace MyApp.Domain.Bookings;

            public sealed class Booking : IAggregateRoot
            {
                private BookingStatus _status;
                public readonly Guid Tag = Guid.NewGuid();

                public Guid Id { get; }
                public BookingStatus Status => _status;
                public DateTimeOffset CreatedAt { get; private set; }
                public string Note { get; internal set; } = "";
                public int Version { get; protected internal set; }
                public string? Reason { get; private init; }
                public static int Count { get; set; }
                public static string Label = "";
                public const int Limit = 3;

                public void Cancel() => _status = BookingStatus.Cancelled;
            }

            public abstract class Bookable : IAggregateRoot
            {
                public int Capacity { get; protected set; }
                public int Floor { get; private protected set; }
                protected int Seats;
            }

            // Only the assembly can reach the members of an internal entity.
            internal sealed class Draft : IEntity
            {
                public string Title { get; set; } = "";
                public int Pages;
            }
            """);
    }

    // DTOs, commands, events and value objects are not entities.
    [Fact]
    public async Task Accepts_value_objects_events_and_DTOs()
    {
        await VerifyAsync(
            """
            namespace MyApp.Domain.Bookings;

            public sealed record BookingPeriod(DateTimeOffset Start, DateTimeOffset End);

            public readonly record struct BookingId(Guid Value);

            public sealed record BookingConfirmed(BookingId BookingId) : IDomainEvent;

            public sealed class BookingDto
            {
                public Guid Id { get; init; }
                public string Status { get; set; } = "";
                public int Seats;
            }
            """);
    }

    [Fact]
    public async Task Accepts_internal_interfaces_internal_setters_and_default_implementations()
    {
        await VerifyAsync(
            """
            namespace MyApp.Domain.Bookings;

            internal interface IRehydratable
            {
                BookingStatus Status { get; set; }
            }

            public interface IHasNote
            {
                string Note { get => ""; set { } }
            }

            public interface IStatusHolder
            {
                BookingStatus Status { get; internal set; }
            }

            public sealed class Booking : IAggregateRoot, IRehydratable, IHasNote, IStatusHolder
            {
                private BookingStatus _status;

                BookingStatus IRehydratable.Status { get => _status; set => _status = value; }

                BookingStatus IStatusHolder.Status { get => _status; set => _status = value; }
            }
            """);
    }

    // The application reads the entities of the domain from its reference: the domain reports them in its own compilation.
    [Fact]
    public async Task Does_not_report_the_entities_of_a_referenced_domain()
    {
        var domain = Compile(
            "MyApp.Domain",
            [
                ("Booking.cs", "namespace MyApp.Domain.Bookings; public sealed class Booking : IAggregateRoot { public int Seats { get; set; } }"),
                ("Contracts.cs", Contracts),
                ("Usings.cs", ImplicitUsings)
            ]);

        (await AnalyzeAllAsync(domain, Configuration)).Select(diagnostic => diagnostic.Id).ShouldBe([EntityStateAnalyzer.DiagnosticId]);

        var application = Compile(
            "MyApp",
            [("Seats.cs", "namespace MyApp; public static class Seats { public static void Reset(MyApp.Domain.Bookings.Booking booking) => booking.Seats = 0; }"), ("Usings.cs", ImplicitUsings)],
            [Reference(domain, asMetadata: true)]);
        var diagnostics = await application
            .WithAnalyzers([new EntityStateAnalyzer()], Options(Configuration))
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Reports_generated_code()
    {
        await VerifyAsync(
            """
            // <auto-generated/>
            namespace MyApp.Domain.Bookings;

            public sealed class Booking : IAggregateRoot
            {
                public int Seats { get; [|set|]; }
            }
            """);
    }

    // Without hardslice.json, HS900 reports the configuration and HS007 recognizes no entity.
    [Fact]
    public async Task Stays_silent_without_its_entity_contract()
    {
        var domain = Compile(
            "MyApp.Domain",
            [
                ("Booking.cs", "namespace MyApp.Domain.Bookings; public sealed class Booking : IAggregateRoot { public int Seats { get; set; } }"),
                ("Contracts.cs", Contracts),
                ("Usings.cs", ImplicitUsings)
            ]);

        var diagnostic = (await AnalyzeAllAsync(domain, configuration: null)).ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(ClassificationAnalyzer.ConfigurationId);
        diagnostic.GetMessage().ShouldContain("classify the types checked by HS004, HS005, HS006, HS007, HS008, HS009, HS010, HS901 and HS902.");
    }

    [Fact]
    public async Task Does_not_apply_to_test_projects()
    {
        var domain = Compile(
            "MyApp.Domain",
            [
                ("Booking.cs", "namespace MyApp.Domain.Bookings; public sealed class Booking : IAggregateRoot { public int Seats { get; set; } }"),
                ("Contracts.cs", Contracts),
                ("Usings.cs", ImplicitUsings)
            ]);

        (await AnalyzeAllAsync(domain, Configuration, isTestProject: true)).ShouldBeEmpty();
    }
}
