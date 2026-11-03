using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using static HardSlice.Analyzers.Tests.AnalyzerHarness;

namespace HardSlice.Analyzers.Tests;

// HS008: an aggregate references another aggregate by its identifier only.
public sealed class AggregateReferenceAnalyzerTests
{
    private const string Rule = "An aggregate references another aggregate only by its identifier.";

    private const string Configuration =
        """
        {
          "version": 1,
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
        """;

    // Room is the other aggregate the bookings refer to.
    private const string Room =
        """
        namespace MyApp.Domain.Rooms;

        public sealed class Room : IAggregateRoot
        {
            public Guid Id { get; } = Guid.NewGuid();
        }
        """;

    // The domain, with its contracts and Room, as the rule sees it in its own compilation.
    private static Task<ImmutableArray<Diagnostic>> VerifyAsync(params string[] markedSources) =>
        VerifyAsync([], markedSources);

    private static Task<ImmutableArray<Diagnostic>> VerifyAsync(MetadataReference[] references, params string[] markedSources) =>
        AnalyzerHarness.VerifyAsync(
            new AggregateReferenceAnalyzer(),
            AggregateReferenceAnalyzer.DiagnosticId,
            "MyApp.Domain",
            Configuration,
            references,
            OutputKind.DynamicallyLinkedLibrary,
            [.. markedSources, Room, Contracts]);

    // The first sentence of each message, without its rule and its fix.
    private static IEnumerable<string> Subjects(ImmutableArray<Diagnostic> diagnostics) =>
        diagnostics.Select(diagnostic => diagnostic.GetMessage().Split(". ")[0]);

    private static IEnumerable<string> Fixes(ImmutableArray<Diagnostic> diagnostics) =>
        diagnostics.Select(diagnostic => diagnostic.GetMessage().Split(" Fix: ")[1].Split(". Skill: ")[0]);

    [Fact]
    public async Task Reports_a_child_entity_that_references_another_aggregate()
    {
        var diagnostics = await VerifyAsync(
            """
            using MyApp.Domain.Rooms;

            namespace MyApp.Domain.Bookings;

            public sealed class Booking : IAggregateRoot
            {
                private readonly List<BookingLine> _lines = [];

                public IReadOnlyList<BookingLine> Lines => _lines;
            }

            public sealed class BookingLine : IEntity
            {
                public Room [|Room|] { get; private set; } = null!;
            }
            """);

        diagnostics.ShouldHaveSingleItem().GetMessage().ShouldBe(
            "entity 'BookingLine' of aggregate 'Booking' references aggregate 'Room' through property 'Room'. " + Rule
            + " Fix: replace the property with 'RoomId' and load 'Room' in the handler that needs it. Skill: add-aggregate. Doc: 'docs/rules/HS008.md'.");
    }

    [Fact]
    public async Task Reports_a_root_that_references_another_aggregate_directly_or_in_a_collection()
    {
        var diagnostics = await VerifyAsync(
            """
            using MyApp.Domain.Rooms;

            namespace MyApp.Domain.Bookings;

            public sealed class Booking : IAggregateRoot
            {
                private readonly List<Room> [|_rooms|] = [];

                public Room? [|Room|] { get; private set; }

                public Room[] [|Alternatives|] => [];
            }
            """);

        Subjects(diagnostics).ShouldBe(
        [
            "aggregate 'Booking' references aggregate 'Room' through field '_rooms'",
            "aggregate 'Booking' references aggregate 'Room' through property 'Room'",
            "aggregate 'Booking' references aggregate 'Room' through property 'Alternatives'"
        ]);
        Fixes(diagnostics).ShouldBe(
        [
            "replace 'Room' with its identifier in the type of the field and load 'Room' in the handler that needs it",
            "replace the property with 'RoomId' and load 'Room' in the handler that needs it",
            "replace 'Room' with its identifier in the type of the property and load 'Room' in the handler that needs it"
        ]);
    }

    // A root never contains another aggregate, not even another instance of its own type.
    [Fact]
    public async Task Reports_a_root_that_references_another_instance_of_its_own_type()
    {
        var diagnostics = await VerifyAsync(
            """
            namespace MyApp.Domain.Bookings;

            public sealed class Booking : IAggregateRoot
            {
                public Booking? [|PreviousBooking|] { get; private set; }
            }
            """);

        diagnostics.ShouldHaveSingleItem().GetMessage().ShouldBe(
            "aggregate 'Booking' references another aggregate 'Booking' through property 'PreviousBooking'. " + Rule
            + " Fix: replace the property with 'PreviousBookingId' and load 'Booking' in the handler that needs it. Skill: add-aggregate. Doc: 'docs/rules/HS008.md'.");
    }

    [Fact]
    public async Task Accepts_identifiers_child_entities_and_the_reference_of_a_child_to_its_root()
    {
        await VerifyAsync(
            """
            namespace MyApp.Domain.Bookings;

            public readonly record struct BookingId(Guid Value);

            public sealed class Booking : IAggregateRoot
            {
                private readonly List<BookingLine> _lines = [];

                public BookingId Id { get; }

                public Guid RoomId { get; private set; }

                public IReadOnlyList<BookingLine> Lines => _lines;
            }

            public sealed class BookingLine : IEntity
            {
                public Booking Booking { get; private set; } = null!;

                public BookingLine? Previous { get; private set; }
            }
            """);
    }

    // Note belongs to Booking and to Invoice, as distinct instances: either root can own it, any other root is foreign to it.
    [Fact]
    public async Task Reports_a_child_shared_by_two_roots_that_references_a_third_one()
    {
        var diagnostics = await VerifyAsync(
            """
            using MyApp.Domain.Rooms;

            namespace MyApp.Domain.Bookings;

            public sealed class Booking : IAggregateRoot
            {
                public IReadOnlyList<Note> Notes { get; } = [];
            }

            public sealed class Invoice : IAggregateRoot
            {
                public IReadOnlyList<Note> Notes { get; } = [];
            }

            public sealed class Note : IEntity
            {
                public Booking? Booking { get; private set; }

                public Invoice? Invoice { get; private set; }

                public Room? [|Room|] { get; private set; }
            }
            """);

        Subjects(diagnostics).ShouldBe(["entity 'Note' of aggregates 'Booking' and 'Invoice' references aggregate 'Room' through property 'Room'"]);
    }

    // BookingLine reaches Seat: Booking owns Seat through it, and a Seat may refer to its Booking, never to a Room.
    [Fact]
    public async Task Follows_the_child_entities_of_child_entities()
    {
        var diagnostics = await VerifyAsync(
            """
            using MyApp.Domain.Rooms;

            namespace MyApp.Domain.Bookings;

            public sealed class Booking : IAggregateRoot
            {
                private readonly List<BookingLine> _lines = [];
            }

            public sealed class BookingLine : IEntity
            {
                private Seat? _seat;
            }

            public sealed class Seat : IEntity
            {
                private Booking? _booking;

                private Dictionary<int, Room> [|_rooms|] = [];
            }
            """);

        Subjects(diagnostics).ShouldBe(["entity 'Seat' of aggregate 'Booking' references aggregate 'Room' through field '_rooms'"]);
    }

    // A child that no root of the compilation reaches belongs to no known aggregate: the rule asserts nothing about it.
    [Fact]
    public async Task Stays_silent_on_a_child_entity_that_no_root_reaches()
    {
        await VerifyAsync(
            """
            using MyApp.Domain.Rooms;

            namespace MyApp.Domain.Bookings;

            public sealed class Orphan : IEntity
            {
                public Room Room { get; private set; } = null!;
            }
            """);
    }

    [Fact]
    public async Task Reports_generic_collections_tuples_and_generic_roots()
    {
        var diagnostics = await VerifyAsync(
            """
            using MyApp.Domain.Rooms;

            namespace MyApp.Domain.Bookings;

            public sealed class Tenant<TId> : IAggregateRoot
            {
                public TId Id { get; private set; } = default!;
            }

            public sealed class Booking : IAggregateRoot
            {
                public IReadOnlyCollection<Room> [|Rooms|] { get; } = [];

                public (int Floor, Room Room)? [|Choice|] { get; private set; }

                public Tenant<Guid>? [|Tenant|] { get; private set; }

                public Lazy<IEnumerable<KeyValuePair<string, Room>>>? [|Catalog|] { get; private set; }
            }
            """);

        Subjects(diagnostics).ShouldBe(
        [
            "aggregate 'Booking' references aggregate 'Room' through property 'Rooms'",
            "aggregate 'Booking' references aggregate 'Room' through property 'Choice'",
            "aggregate 'Booking' references aggregate 'Tenant<Guid>' through property 'Tenant'",
            "aggregate 'Booking' references aggregate 'Room' through property 'Catalog'"
        ]);
        Fixes(diagnostics).ElementAt(2).ShouldBe("replace the property with 'TenantId' and load 'Tenant<Guid>' in the handler that needs it");
    }

    // The identifier keeps the name of the member when it names the root, and is named after the root otherwise.
    [Fact]
    public async Task Names_the_identifier_after_the_member_or_the_root()
    {
        var diagnostics = await VerifyAsync(
            """
            using MyApp.Domain.Rooms;

            namespace MyApp.Domain.Bookings;

            public sealed class Booking : IAggregateRoot
            {
                private Room? [|_previousRoom|];

                private Room? [|_venue|];

                public Room? [|Venue|] { get; private set; }
            }
            """);

        Fixes(diagnostics).Select(fix => fix.Split(" and load ")[0]).ShouldBe(
        [
            "replace the field with '_previousRoomId'",
            "replace the field with '_roomId'",
            "replace the property with 'RoomId'"
        ]);
    }

    [Fact]
    public async Task Reports_struct_and_record_entities_and_positional_properties()
    {
        var diagnostics = await VerifyAsync(
            """
            using MyApp.Domain.Rooms;

            namespace MyApp.Domain.Bookings;

            public sealed record Booking(Room [|Room|]) : IAggregateRoot
            {
                public IReadOnlyList<Seat> Seats { get; } = [];
            }

            public record struct Seat(int Number) : IEntity
            {
                private Room? [|_room|];
            }
            """);

        Subjects(diagnostics).ShouldBe(
        [
            "aggregate 'Booking' references aggregate 'Room' through property 'Room'",
            "entity 'Seat' of aggregate 'Booking' references aggregate 'Room' through field '_room'"
        ]);
    }

    // Holder is not an entity: what it holds is the state of the roots derived from it.
    [Fact]
    public async Task Reports_on_the_entity_the_members_inherited_from_a_base_class_that_is_not_an_entity()
    {
        var diagnostics = await VerifyAsync(
            """
            using MyApp.Domain.Rooms;

            namespace MyApp.Domain.Bookings;

            public abstract class RoomHolder
            {
                protected Room? Room { get; set; }
            }

            public sealed class [|Booking|] : RoomHolder, IAggregateRoot;
            """);

        diagnostics.ShouldHaveSingleItem().GetMessage().ShouldBe(
            "aggregate 'Booking' references aggregate 'Room' through property 'Room', inherited from 'RoomHolder'. " + Rule
            + " Fix: replace the property 'RoomHolder.Room' with 'RoomId' and load 'Room' in the handler that needs it. Skill: add-aggregate. Doc: 'docs/rules/HS008.md'.");
    }

    // The base root is an entity of the same compilation: it reports its own member, once, and its derived roots do not repeat it.
    [Fact]
    public async Task Reports_a_base_entity_once_on_its_own_declaration()
    {
        var diagnostics = await VerifyAsync(
            """
            using MyApp.Domain.Rooms;

            namespace MyApp.Domain.Bookings;

            public abstract class Reservation : IAggregateRoot
            {
                public virtual Room? [|Room|] { get; protected set; }
            }

            public sealed class Booking : Reservation
            {
                public override Room? Room { get; protected set; }
            }

            public sealed class Hold : Reservation;
            """);

        Subjects(diagnostics).ShouldBe(["aggregate 'Reservation' references aggregate 'Room' through property 'Room'"]);
    }

    [Theory]
    [InlineData("IEntity")]
    [InlineData("IAggregateRoot")]
    public async Task Reports_root_references_revealed_by_a_constructed_entity_base(string contract)
    {
        await VerifyAsync(
            $$"""
            using MyApp.Domain.Rooms;
            namespace MyApp.Domain.Bookings;

            public abstract class EntityLink<T> : {{contract}}
            {
                public T Related { get; protected set; } = default!;
            }
            public sealed class [|Booking|] : EntityLink<Room>, IAggregateRoot;
            """);
    }

    [Fact]
    public async Task Reports_root_references_inherited_from_child_entities_without_owners()
    {
        await VerifyAsync(
            """
            using MyApp.Domain.Rooms;
            namespace MyApp.Domain.Bookings;

            public abstract class EntityLink : IEntity { public virtual Room? Related { get; protected set; } }
            public sealed class [|Booking|] : EntityLink, IAggregateRoot
            {
                public override Room? Related { get; protected set; }
            }
            public abstract class GenericLink<T> : IEntity { public T Related { get; protected set; } = default!; }
            public abstract class ClosedLink : GenericLink<Room>;
            public sealed class [|Hold|] : ClosedLink, IAggregateRoot;
            """);
    }

    [Fact]
    public async Task Reports_a_root_inheriting_a_back_reference_allowed_on_its_child_base()
    {
        await VerifyAsync(
            """
            namespace MyApp.Domain.Bookings;

            public class Parent : IAggregateRoot { public EntityLink? Child { get; private set; } }
            public abstract class EntityLink : IEntity { public Parent? Owner { get; protected set; } }
            public sealed class [|Booking|] : EntityLink, IAggregateRoot;
            """);
    }

    [Fact]
    public async Task Does_not_repeat_a_violation_already_reported_by_a_generic_base()
    {
        await VerifyAsync(
            """
            using MyApp.Domain.Rooms;
            namespace MyApp.Domain.Bookings;

            public abstract class Root<T> : IAggregateRoot { public Room? [|Related|] { get; protected set; } }
            public class Intermediate<T> : Root<List<T>>;
            public sealed class Booking : Intermediate<Guid>;
            """);
    }

    // The roots of another assembly are recognized by their contract in metadata: a root never references them.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reports_a_root_that_references_a_root_declared_in_another_assembly(bool asMetadata)
    {
        var contracts = Reference(Compile("MyApp.Domain", [("Contracts.cs", Contracts), ("Usings.cs", ImplicitUsings)]), asMetadata: true);
        var rooms = Compile(
            "MyApp.Rooms",
            [
                ("Room.cs", "namespace MyApp.Rooms; public sealed class Room : MyApp.Domain.IAggregateRoot;"),
                ("Usings.cs", ImplicitUsings)
            ],
            [contracts]);

        var diagnostics = await AnalyzerHarness.VerifyAsync(
            new AggregateReferenceAnalyzer(),
            AggregateReferenceAnalyzer.DiagnosticId,
            "MyApp.Bookings",
            Configuration,
            [contracts, Reference(rooms, asMetadata)],
            OutputKind.DynamicallyLinkedLibrary,
            """
            namespace MyApp.Bookings;

            public sealed class Booking : MyApp.Domain.IAggregateRoot
            {
                private readonly List<BookingLine> _lines = [];

                public MyApp.Rooms.Room? [|Room|] { get; private set; }
            }

            public sealed class BookingLine : MyApp.Domain.IEntity
            {
                public MyApp.Rooms.Room? Room { get; private set; }
            }
            """);

        // From the child, Room is ambiguous: its private members, which may reach BookingLine, are not visible here.
        Subjects(diagnostics).ShouldBe(["aggregate 'Booking' references aggregate 'Room' through property 'Room'"]);
    }

    // A reference typed by an unclassified type, a type parameter or object is not detected: the rule reads declared types.
    [Fact]
    public async Task Does_not_detect_references_typed_by_object_an_unclassified_interface_or_a_type_parameter()
    {
        await VerifyAsync(
            """
            namespace MyApp.Domain.Bookings;

            public interface IRoomView
            {
                Guid Id { get; }
            }

            public sealed class Booking<TRoom> : IAggregateRoot where TRoom : IAggregateRoot
            {
                public object? Room { get; private set; }

                public IRoomView? View { get; private set; }

                public TRoom? Other { get; private set; }

                public static Booking<TRoom>? Last { get; private set; }
            }
            """);
    }

    // The application reads the aggregates of the domain from its reference: the domain reports them in its own compilation.
    [Fact]
    public async Task Does_not_report_the_entities_of_a_referenced_domain()
    {
        var domain = Compile(
            "MyApp.Domain",
            [
                ("Booking.cs", "namespace MyApp.Domain.Bookings; public sealed class Booking : IAggregateRoot { public MyApp.Domain.Rooms.Room? Room { get; private set; } }"),
                ("Room.cs", Room),
                ("Contracts.cs", Contracts),
                ("Usings.cs", ImplicitUsings)
            ]);

        (await AnalyzeAllAsync(domain, Configuration)).Select(diagnostic => diagnostic.Id).ShouldBe([AggregateReferenceAnalyzer.DiagnosticId]);

        var application = Compile(
            "MyApp",
            [("Rooms.cs", "namespace MyApp; public static class Rooms { public static object? Of(MyApp.Domain.Bookings.Booking booking) => booking.Room; }"), ("Usings.cs", ImplicitUsings)],
            [Reference(domain, asMetadata: true)]);
        var diagnostics = await application
            .WithAnalyzers([new AggregateReferenceAnalyzer()], Options(Configuration))
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
                public MyApp.Domain.Rooms.Room? [|Room|] { get; private set; }
            }
            """);
    }

    // Without hardslice.json, HS900 reports the configuration and HS008 recognizes no aggregate.
    [Fact]
    public async Task Stays_silent_without_its_contracts()
    {
        var diagnostic = (await AnalyzeAllAsync(CompileBookingWithItsRoom(), configuration: null)).ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(ClassificationAnalyzer.ConfigurationId);
        diagnostic.GetMessage().ShouldContain("classify the types checked by HS004, HS005, HS006, HS007, HS008, HS009, HS010, HS901 and HS902.");
    }

    [Fact]
    public async Task Stays_silent_without_the_aggregate_root_contract()
    {
        var configuration = Configuration.Replace("    \"aggregateRoot\": \"MyApp.Domain.IAggregateRoot, MyApp.Domain\",\n", "");
        configuration.ShouldNotBe(Configuration);

        var diagnostic = (await AnalyzeAllAsync(CompileBookingWithItsRoom(), configuration)).ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(ClassificationAnalyzer.ConfigurationId);
        diagnostic.GetMessage().ShouldContain("classify the types checked by HS008, HS901 and HS902.");
    }

    [Fact]
    public async Task Does_not_apply_to_test_projects()
    {
        (await AnalyzeAllAsync(CompileBookingWithItsRoom(), Configuration, isTestProject: true)).ShouldBeEmpty();
    }

    private static CSharpCompilation CompileBookingWithItsRoom() =>
        Compile(
            "MyApp.Domain",
            [
                ("Booking.cs", "namespace MyApp.Domain.Bookings; public sealed class Booking : IAggregateRoot { public MyApp.Domain.Rooms.Room? Room { get; private set; } }"),
                ("Room.cs", Room),
                ("Contracts.cs", Contracts),
                ("Usings.cs", ImplicitUsings)
            ]);
}
