using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using static HardSlice.Analyzers.Tests.AnalyzerHarness;

namespace HardSlice.Analyzers.Tests;

// HS900 to HS902 on an application and its Domain compiled as two assemblies, as in a generated solution.
public sealed class ClassificationAnalyzerTests
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
            "requestHandler": "MyApp.Shared.Messaging.IRequestHandler`2, MyApp"
          },
          "shared": {
            "allowedFolders": ["Messaging", "Persistence", "Behaviors"]
          }
        }
        """;

    private const string DomainContracts =
        """
        namespace MyApp.Domain;

        public interface IEntity;

        public interface IAggregateRoot : IEntity;

        public interface IDomainEvent;
        """;

    private const string Messaging =
        """
        namespace MyApp.Shared.Messaging;

        public interface IRequest<TResponse>;

        public interface ICommand<TResponse> : IRequest<TResponse>;

        public interface IQuery<TResponse> : IRequest<TResponse>;

        public interface IRequestHandler<in TRequest, TResponse> where TRequest : IRequest<TResponse>
        {
            Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
        }
        """;

    private const string EntityFrameworkUsings =
        """
        global using Microsoft.EntityFrameworkCore;
        global using Microsoft.EntityFrameworkCore.Metadata.Builders;
        global using MyApp.Domain;
        global using MyApp.Shared.Messaging;
        """;

    private const string Room =
        """
        namespace MyApp.Domain.Rooms;

        public sealed class Room
        {
            public Guid Id { get; private set; }
        }
        """;

    private const string RoomMappings =
        """
        using MyApp.Domain.Rooms;

        namespace MyApp.Features.Rooms;

        internal sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
        {
            public void Configure(EntityTypeBuilder<Room> builder) => builder.HasKey(room => room.Id);
        }

        internal sealed class RoomsContext(DbContextOptions<RoomsContext> options) : DbContext(options)
        {
            public DbSet<Room> Rooms => Set<Room>();

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<Room>();
                modelBuilder.Entity<Room>(room => room.HasKey(r => r.Id));
            }
        }
        """;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reports_nothing_on_contracted_aggregates_owned_entities_complex_types_and_requests(bool domainAsMetadata)
    {
        var domain = CompileDomain(
            """
            namespace MyApp.Domain.Bookings;

            public sealed class Booking : IAggregateRoot
            {
                private readonly List<BookingLine> _lines = [];

                public Guid Id { get; private set; }

                public BookingPeriod Period { get; private set; } = null!;

                public IReadOnlyList<BookingLine> Lines => _lines;
            }

            public sealed record BookingPeriod(DateTimeOffset Start, DateTimeOffset End);

            public sealed class BookingLine : IEntity
            {
                public Guid Id { get; private set; }
            }

            public sealed record BookingConfirmed(Guid BookingId) : IDomainEvent;
            """);
        var application = CompileApplication(
            Reference(domain, domainAsMetadata),
            """
            using MyApp.Domain.Bookings;

            namespace MyApp.Features.Bookings;

            internal sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
            {
                public void Configure(EntityTypeBuilder<Booking> builder)
                {
                    builder.HasKey(booking => booking.Id);
                    builder.ComplexProperty(booking => booking.Period);
                    builder.OwnsMany(booking => booking.Lines);
                }
            }

            internal sealed class BookingsContext(DbContextOptions<BookingsContext> options) : DbContext(options)
            {
                public DbSet<Booking> Bookings => Set<Booking>();

                protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Booking>();
            }

            public sealed record ConfirmBookingCommand(Guid RoomId) : ICommand<Guid>;

            public sealed record BookingDto(Guid Id);

            public sealed record GetBookingQuery(Guid Id) : IQuery<BookingDto?>;

            internal sealed class GetBookingHandler : IRequestHandler<GetBookingQuery, BookingDto?>
            {
                public Task<BookingDto?> Handle(GetBookingQuery query, CancellationToken cancellationToken) => Task.FromResult<BookingDto?>(null);
            }
            """);

        (await AnalyzeAllAsync(domain, Configuration)).ShouldBeEmpty();
        // The requests stay in their module here: HS004, which checks the shape of a slice, would report them.
        (await AnalyzeAllAsync(WithSeverities(application, ("HS004", ReportDiagnostic.Suppress)), Configuration)).ShouldBeEmpty();
    }

    // MyApp.Domain does not reference MyApp: ICommand<T>, IQuery<T> and IRequestHandler<,> are invisible, not missing.
    [Fact]
    public async Task Does_not_require_the_messaging_contracts_in_the_domain_compiled_alone()
    {
        var domain = CompileDomain("namespace MyApp.Domain.Rooms; public sealed class Room : IAggregateRoot;");

        (await AnalyzeAllAsync(domain, Configuration)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Reports_HS900_in_the_domain_and_the_application_when_IEntity_is_removed()
    {
        var domain = Compile(
            "MyApp.Domain",
            [("Contracts.cs", "namespace MyApp.Domain; public interface IAggregateRoot; public interface IDomainEvent;"), ("Usings.cs", ImplicitUsings)]);
        var application = CompileApplication(domain.ToMetadataReference(), "namespace MyApp.Features;");

        foreach (var compilation in new Compilation[] { domain, application })
        {
            var diagnostic = (await AnalyzeAllAsync(compilation, Configuration)).ShouldHaveSingleItem();

            diagnostic.Id.ShouldBe(ClassificationAnalyzer.ConfigurationId);
            diagnostic.GetMessage().ShouldBe(
                "contract 'contracts.entity' of hardslice.json, 'MyApp.Domain.IEntity', is not declared by assembly 'MyApp.Domain'. "
                + "The contracts of hardslice.json classify the types checked by HS005, HS006, HS010, HS901 and HS902. "
                + "Fix: restore the contract type 'MyApp.Domain.IEntity' in project 'MyApp.Domain', or hardslice.json, from version control. "
                + "Doc: 'docs/rules/HS900.md'.");
            var position = diagnostic.Location.GetLineSpan();
            position.Path.ShouldBe(ConfigurationPath);
            position.StartLinePosition.Line.ShouldBe(3);
        }
    }

    [Fact]
    public async Task Reports_HS900_only_in_the_application_when_the_command_contract_is_renamed()
    {
        var configuration = Configuration.Replace("ICommand`1, MyApp", "ICommandX`1, MyApp");
        var domain = CompileDomain();
        var application = CompileApplication(domain.ToMetadataReference());

        (await AnalyzeAllAsync(domain, configuration)).ShouldBeEmpty();
        var diagnostic = (await AnalyzeAllAsync(application, configuration)).ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(ClassificationAnalyzer.ConfigurationId);
        diagnostic.GetMessage().ShouldStartWith(
            "contract 'contracts.command' of hardslice.json, 'MyApp.Shared.Messaging.ICommandX`1', is not declared by assembly 'MyApp'. "
            + "The contracts of hardslice.json classify the types checked by HS004, HS005 and HS901.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reports_HS902_on_each_mapping_form_of_a_domain_type_without_contract(bool domainAsMetadata)
    {
        var domain = CompileDomain(Room);
        var application = CompileApplication(Reference(domain, domainAsMetadata), RoomMappings);

        (await AnalyzeAllAsync(domain, Configuration)).ShouldBeEmpty();
        var diagnostics = await AnalyzeAllAsync(application, Configuration);

        Located(diagnostics).ShouldBe(
            ["HS902 Entity<Room>", "HS902 Entity<Room>", "HS902 RoomConfiguration", "HS902 Rooms"],
            ignoreOrder: true);
        diagnostics.Select(diagnostic => diagnostic.GetMessage()).Distinct().ShouldBe(
            [
                "'Room' is mapped as an EF Core entity by 'RoomConfiguration' but implements neither 'IEntity' nor 'IAggregateRoot'. "
                + "HardSlice classifies domain types only through these contracts. "
                + "Fix: implement IAggregateRoot if 'Room' is the root of its aggregate, IEntity otherwise. Doc: 'docs/rules/HS902.md'.",
                "'Room' is mapped as an EF Core entity by 'RoomsContext.Rooms' but implements neither 'IEntity' nor 'IAggregateRoot'. "
                + "HardSlice classifies domain types only through these contracts. "
                + "Fix: implement IAggregateRoot if 'Room' is the root of its aggregate, IEntity otherwise. Doc: 'docs/rules/HS902.md'.",
                "'Room' is mapped as an EF Core entity by 'RoomsContext.OnModelCreating' but implements neither 'IEntity' nor 'IAggregateRoot'. "
                + "HardSlice classifies domain types only through these contracts. "
                + "Fix: implement IAggregateRoot if 'Room' is the root of its aggregate, IEntity otherwise. Doc: 'docs/rules/HS902.md'."
            ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task Does_not_submit_owned_or_complex_types_nor_types_outside_the_domain_assembly()
    {
        var domain = CompileDomain(
            """
            namespace MyApp.Domain.Rooms;

            public sealed class Room : IAggregateRoot
            {
                public Guid Id { get; private set; }

                public Address Address { get; private set; } = null!;

                public Capacity Capacity { get; private set; } = null!;

                public IReadOnlyList<Equipment> Equipment { get; } = [];
            }

            public sealed class Address
            {
                public string Street { get; private set; } = "";
            }

            public sealed record Capacity(int Seats);

            public sealed class Equipment
            {
                public string Name { get; private set; } = "";
            }
            """);
        var application = CompileApplication(
            domain.ToMetadataReference(),
            """
            using MyApp.Domain.Rooms;

            namespace MyApp.Features.Rooms;

            internal sealed class AuditEntry
            {
                public Guid Id { get; private set; }
            }

            internal sealed class RoomsContext(DbContextOptions<RoomsContext> options) : DbContext(options)
            {
                public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

                protected override void OnModelCreating(ModelBuilder modelBuilder) =>
                    modelBuilder.Entity<Room>(room =>
                    {
                        room.OwnsOne(r => r.Address);
                        room.ComplexProperty(r => r.Capacity);
                        room.OwnsMany(r => r.Equipment);
                    });
            }
            """);

        (await AnalyzeAllAsync(application, Configuration)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Classifies_through_a_generic_base_class_a_derived_interface_and_generic_definitions(bool domainAsMetadata)
    {
        var domain = CompileDomain(
            """
            namespace MyApp.Domain.Billing;

            public abstract class Entity<TId> : IEntity
            {
                public TId Id { get; protected set; } = default!;
            }

            public interface IAuditedEntity : IEntity;

            public sealed class Customer : Entity<Guid>, IAggregateRoot;

            public sealed class Invoice : IAuditedEntity
            {
                public Guid Id { get; private set; }
            }

            public sealed class Tagged<T> : IEntity
            {
                public Guid Id { get; private set; }
            }

            public sealed class Untagged<T>
            {
                public Guid Id { get; private set; }
            }
            """);
        var application = CompileApplication(
            Reference(domain, domainAsMetadata),
            """
            using MyApp.Domain.Billing;

            namespace MyApp.Features.Billing;

            internal abstract class ConfigurationBase<T> : IEntityTypeConfiguration<T> where T : class
            {
                public void Configure(EntityTypeBuilder<T> builder) { }
            }

            internal sealed class InvoiceConfiguration : ConfigurationBase<Invoice>;

            internal sealed class UntaggedConfiguration : ConfigurationBase<Untagged<long>>;

            internal sealed class BillingContext(DbContextOptions<BillingContext> options) : DbContext(options)
            {
                public DbSet<Customer> Customers => Set<Customer>();

                public DbSet<Tagged<int>> Tagged => Set<Tagged<int>>();

                protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Untagged<int>>();
            }
            """);

        var diagnostics = await AnalyzeAllAsync(application, Configuration);

        Located(diagnostics).ShouldBe(["HS902 Entity<Untagged<int>>", "HS902 UntaggedConfiguration"], ignoreOrder: true);
        diagnostics.ShouldAllBe(diagnostic => diagnostic.GetMessage().StartsWith("'Untagged<T>' is mapped as an EF Core entity by '"));
    }

    // Only the contract hardslice.json names, in its declaring assembly, classifies: a lookalike never does.
    [Fact]
    public async Task Ignores_homonymous_contracts_of_another_namespace_or_assembly()
    {
        var lookalike = Compile("Lookalike", [("IEntity.cs", "namespace MyApp.Domain; public interface IEntity;")]);
        var lookalikeReference = lookalike.ToMetadataReference(aliases: ["lookalike"]);
        var domain = Compile(
            "MyApp.Domain",
            [
                ("Contracts.cs", DomainContracts),
                ("Usings.cs", ImplicitUsings),
                ("Rooms.cs",
                    """
                    extern alias lookalike;

                    namespace MyApp.Domain.Other
                    {
                        public interface IEntity;
                    }

                    namespace MyApp.Domain.Rooms
                    {
                        public sealed class Room : MyApp.Domain.Other.IEntity
                        {
                            public Guid Id { get; private set; }
                        }

                        public sealed class Hall : lookalike::MyApp.Domain.IEntity
                        {
                            public Guid Id { get; private set; }
                        }
                    }
                    """)
            ],
            [lookalikeReference]);
        var application = CompileApplication(
            [domain.ToMetadataReference(), lookalikeReference],
            """
            using MyApp.Domain.Rooms;

            namespace MyApp.Features.Rooms;

            internal sealed class RoomsContext(DbContextOptions<RoomsContext> options) : DbContext(options)
            {
                public DbSet<Room> Rooms => Set<Room>();

                public DbSet<Hall> Halls => Set<Hall>();
            }
            """);

        Located(await AnalyzeAllAsync(application, Configuration)).ShouldBe(["HS902 Halls", "HS902 Rooms"], ignoreOrder: true);
    }

    [Fact]
    public async Task Reports_HS901_on_a_domain_type_that_is_both_an_entity_and_a_domain_event()
    {
        var domain = CompileDomain(
            """
            namespace MyApp.Domain.Bookings;

            public sealed class BookingSnapshot : IAggregateRoot, IDomainEvent;
            """);

        var diagnostic = (await AnalyzeAllAsync(domain, Configuration)).ShouldHaveSingleItem();

        Located([diagnostic]).ShouldBe(["HS901 BookingSnapshot"]);
        diagnostic.GetMessage().ShouldBe(
            "'BookingSnapshot' is both an entity and a domain event: it implements 'IAggregateRoot' and 'IDomainEvent'. "
            + "A type is either an entity, which has an identity and changes through its methods, or a domain event, an immutable fact an entity records. "
            + "Fix: keep 'BookingSnapshot' as the entity and declare the event as a separate record that it records. Doc: 'docs/rules/HS901.md'.");
    }

    [Fact]
    public async Task Reports_HS901_on_a_request_that_is_both_a_command_and_a_query_or_an_entity()
    {
        var domain = CompileDomain();
        var application = CompileApplication(
            domain.ToMetadataReference(),
            """
            namespace MyApp.Features.Rooms.BookRoom;

            public sealed record BookOrRead(Guid RoomId) : ICommand<bool>, IQuery<bool>;

            public sealed record BookOrReadAny<T>(T Value) : ICommand<T>, IQuery<T>;

            public sealed class RoomRequest : IEntity, IQuery<bool>;

            public abstract record CommandBase<T> : ICommand<T>;

            public sealed record InheritedBoth(Guid RoomId) : CommandBase<bool>, IQuery<bool>;
            """);

        // HS004 would report the shape of the slice BookRoom, which holds these requests only.
        var diagnostics = await AnalyzeAllAsync(WithSeverities(application, ("HS004", ReportDiagnostic.Suppress)), Configuration);

        Located(diagnostics).ShouldBe(
            ["HS901 BookOrRead", "HS901 BookOrReadAny", "HS901 InheritedBoth", "HS901 RoomRequest"],
            ignoreOrder: true);
        diagnostics.Single(diagnostic => diagnostic.GetMessage().StartsWith("'BookOrRead' ")).GetMessage().ShouldBe(
            "'BookOrRead' is both a command and a query: it implements 'ICommand<TResponse>' and 'IQuery<TResponse>'. "
            + "A request either changes state, as a command, or only reads, as a query. "
            + "Fix: split it into a command that changes state and a query that reads. Doc: 'docs/rules/HS901.md'.");
        diagnostics.Single(diagnostic => diagnostic.GetMessage().StartsWith("'RoomRequest' ")).GetMessage().ShouldBe(
            "'RoomRequest' is both an entity and a query: it implements 'IEntity' and 'IQuery<TResponse>'. "
            + "A request is a message sent through the mediator, never a domain entity. "
            + "Fix: declare the request as its own record in its slice, carrying the values 'RoomRequest' needs. Doc: 'docs/rules/HS901.md'.");
    }

    [Fact]
    public async Task Reports_HS900_when_hardslice_json_is_missing()
    {
        var diagnostic = (await AnalyzeAllAsync(CompileDomain(), configuration: null)).ShouldHaveSingleItem();

        diagnostic.Id.ShouldBe(ClassificationAnalyzer.ConfigurationId);
        diagnostic.Location.ShouldBe(Location.None);
        diagnostic.GetMessage().ShouldBe(
            "hardslice.json was not found in '/solution/src/MyApp' or above it. "
            + "The contracts of hardslice.json classify the types checked by HS004, HS005, HS006, HS010, HS901 and HS902. "
            + "Fix: restore hardslice.json at the solution root from version control. Doc: 'docs/rules/HS900.md'.");
    }

    [Theory]
    [InlineData("{ \"style\": \"slices\", ", "hardslice.json is not valid JSON at line 1, column 22: expected a property name in double quotes")]
    [InlineData("{ \"style\": \"slices\" } }", "hardslice.json is not valid JSON at line 1, column 23: unexpected '}' after the root value")]
    [InlineData("{ 'style': 'slices' }", "hardslice.json is not valid JSON at line 1, column 3: expected a property name in double quotes")]
    [InlineData("[]", "the root of hardslice.json must be a JSON object")]
    [InlineData("{ \"styles\": \"slices\" }", "hardslice.json has an unknown key 'styles'")]
    [InlineData("{ \"contracts\": [] }", "'contracts' in hardslice.json must be a JSON object")]
    [InlineData("{ \"contracts\": { \"entities\": \"MyApp.Domain.IEntity, MyApp.Domain\" } }", "hardslice.json has an unknown key 'contracts.entities'")]
    [InlineData("{ \"contracts\": { \"entity\": \"MyApp.Domain.IEntity, MyApp.Domain\", \"entity\": \"MyApp.Domain.IEntity, MyApp.Domain\" } }", "hardslice.json declares 'contracts.entity' twice")]
    public async Task Reports_HS900_when_hardslice_json_is_not_in_the_expected_format(string configuration, string expected)
    {
        var diagnostic = (await AnalyzeAllAsync(CompileDomain(), configuration)).ShouldHaveSingleItem();

        diagnostic.Id.ShouldBe(ClassificationAnalyzer.ConfigurationId);
        diagnostic.GetMessage().ShouldStartWith(expected + ". ");
        diagnostic.Location.GetLineSpan().Path.ShouldBe(ConfigurationPath);
    }

    [Theory]
    [InlineData("\"MyApp.Domain.IEntity\"")]
    [InlineData("\"MyApp.Domain.IEntity,MyApp.Domain\"")]
    [InlineData("\"MyApp.Domain.IEntity, MyApp.Domain, Version=1.0.0.0\"")]
    [InlineData("\"MyApp.Domain.IEntity , MyApp.Domain\"")]
    [InlineData("42")]
    [InlineData("null")]
    public async Task Reports_HS900_naming_a_malformed_contract(string entity)
    {
        var configuration = Configuration.Replace("\"MyApp.Domain.IEntity, MyApp.Domain\"", entity);

        var diagnostic = (await AnalyzeAllAsync(CompileDomain(), configuration)).ShouldHaveSingleItem();

        diagnostic.GetMessage().ShouldStartWith(
            "'contracts.entity' in hardslice.json is not '<metadata name>, <assembly>'. "
            + "The contracts of hardslice.json classify the types checked by HS005, HS006, HS010, HS901 and HS902. "
            + "Fix: restore hardslice.json from version control; it expects the metadata name of the contract type then its declaring assembly, "
            + "such as \"MyApp.Domain.IEntity, MyApp.Domain\". Doc: 'docs/rules/HS900.md'.");
        diagnostic.Location.GetLineSpan().StartLinePosition.Line.ShouldBe(3);
    }

    [Fact]
    public async Task Reports_HS900_when_a_contract_key_is_missing()
    {
        var configuration = Configuration.Replace("\"domainEvent\": \"MyApp.Domain.IDomainEvent, MyApp.Domain\",", "");

        var diagnostic = (await AnalyzeAllAsync(CompileDomain(), configuration)).ShouldHaveSingleItem();

        diagnostic.GetMessage().ShouldStartWith(
            "hardslice.json declares no contract 'contracts.domainEvent'. The contracts of hardslice.json classify the types checked by HS901. "
            + "Fix: restore hardslice.json from version control; it declares \"domainEvent\": \"MyApp.Domain.IDomainEvent, MyApp.Domain\".");
    }

    [Fact]
    public async Task Reports_HS900_when_two_keys_designate_the_same_type()
    {
        var configuration = Configuration.Replace("IQuery`1, MyApp", "ICommand`1, MyApp");

        var diagnostic = (await AnalyzeAllAsync(CompileDomain(), configuration)).ShouldHaveSingleItem();

        diagnostic.GetMessage().ShouldStartWith(
            "contracts 'contracts.command' and 'contracts.query' of hardslice.json both designate 'MyApp.Shared.Messaging.ICommand`1, MyApp'. "
            + "The contracts of hardslice.json classify the types checked by HS004, HS005, HS006 and HS901.");
    }

    [Fact]
    public async Task Reports_HS900_when_the_aggregate_root_contract_does_not_derive_from_the_entity_contract()
    {
        var domain = Compile(
            "MyApp.Domain",
            [("Contracts.cs", "namespace MyApp.Domain; public interface IEntity; public interface IAggregateRoot; public interface IDomainEvent;"), ("Usings.cs", ImplicitUsings)]);

        var diagnostic = (await AnalyzeAllAsync(domain, Configuration)).ShouldHaveSingleItem();

        diagnostic.GetMessage().ShouldBe(
            "contract 'contracts.aggregateRoot', 'IAggregateRoot', does not derive from contract 'contracts.entity', 'IEntity'. "
            + "The contracts of hardslice.json classify the types checked by HS005, HS006, HS010, HS901 and HS902. "
            + "Fix: declare 'IAggregateRoot' as deriving from 'IEntity': an aggregate root is an entity. Doc: 'docs/rules/HS900.md'.");
    }

    [Fact]
    public async Task Accepts_comments_in_hardslice_json()
    {
        var configuration = "// HardSlice configuration\n" + Configuration.Replace("\"style\": \"slices\",", "\"style\": \"slices\", /* the only style */");

        (await AnalyzeAllAsync(CompileDomain(), configuration)).ShouldBeEmpty();
    }

    // Only HS004 to HS006 need the handler contract: HS900 stays silent while they are off.
    [Fact]
    public async Task Does_not_report_a_contract_no_active_rule_needs()
    {
        var configuration = Configuration.Replace(",\n    \"requestHandler\": \"MyApp.Shared.Messaging.IRequestHandler`2, MyApp\"", "");
        configuration.ShouldNotContain("requestHandler");
        var application = CompileApplication(CompileDomain().ToMetadataReference());

        (await AnalyzeAllAsync(
                WithSeverities(application, ("HS004", ReportDiagnostic.Suppress), ("HS005", ReportDiagnostic.Suppress), ("HS006", ReportDiagnostic.Suppress)),
                configuration))
            .ShouldBeEmpty();
        (await AnalyzeAllAsync(application, configuration)).ShouldHaveSingleItem().GetMessage().ShouldStartWith(
            "hardslice.json declares no contract 'contracts.requestHandler'. The contracts of hardslice.json classify the types checked by HS004, HS005 and HS006. ");
    }

    [Fact]
    public async Task Reports_HS900_only_for_the_rules_that_are_active()
    {
        var domain = CompileDomain();

        (await AnalyzeAllAsync(
                WithSeverities(
                    domain,
                    ("HS004", ReportDiagnostic.Suppress),
                    ("HS005", ReportDiagnostic.Suppress),
                    ("HS006", ReportDiagnostic.Suppress),
                    ("HS010", ReportDiagnostic.Suppress),
                    ("HS901", ReportDiagnostic.Suppress),
                    ("HS902", ReportDiagnostic.Suppress)),
                configuration: null))
            .ShouldBeEmpty();
        (await AnalyzeAllAsync(
                WithSeverities(
                    domain,
                    ("HS004", ReportDiagnostic.Suppress),
                    ("HS005", ReportDiagnostic.Suppress),
                    ("HS006", ReportDiagnostic.Suppress),
                    ("HS010", ReportDiagnostic.Suppress),
                    ("HS902", ReportDiagnostic.Suppress)),
                configuration: null))
            .ShouldHaveSingleItem().GetMessage().ShouldContain("checked by HS901.");
        (await AnalyzeAllAsync(
                WithSeverities(
                    domain,
                    ("HS004", ReportDiagnostic.Suppress),
                    ("HS005", ReportDiagnostic.Suppress),
                    ("HS006", ReportDiagnostic.Suppress),
                    ("HS901", ReportDiagnostic.Suppress),
                    ("HS902", ReportDiagnostic.Suppress)),
                configuration: null))
            .ShouldHaveSingleItem().GetMessage().ShouldContain("checked by HS010.");
        (await AnalyzeAllAsync(
                WithSeverities(
                    domain,
                    ("HS005", ReportDiagnostic.Suppress),
                    ("HS006", ReportDiagnostic.Suppress),
                    ("HS010", ReportDiagnostic.Suppress),
                    ("HS901", ReportDiagnostic.Suppress),
                    ("HS902", ReportDiagnostic.Suppress)),
                configuration: null))
            .ShouldHaveSingleItem().GetMessage().ShouldContain("checked by HS004.");
        (await AnalyzeAllAsync(
                WithSeverities(
                    domain,
                    ("HS004", ReportDiagnostic.Suppress),
                    ("HS006", ReportDiagnostic.Suppress),
                    ("HS010", ReportDiagnostic.Suppress),
                    ("HS901", ReportDiagnostic.Suppress),
                    ("HS902", ReportDiagnostic.Suppress)),
                configuration: null))
            .ShouldHaveSingleItem().GetMessage().ShouldContain("checked by HS005.");
        (await AnalyzeAllAsync(
                WithSeverities(
                    domain,
                    ("HS004", ReportDiagnostic.Suppress),
                    ("HS005", ReportDiagnostic.Suppress),
                    ("HS010", ReportDiagnostic.Suppress),
                    ("HS901", ReportDiagnostic.Suppress),
                    ("HS902", ReportDiagnostic.Suppress)),
                configuration: null))
            .ShouldHaveSingleItem().GetMessage().ShouldContain("checked by HS006.");
    }

    [Fact]
    public async Task Does_not_apply_to_test_projects()
    {
        var domain = CompileDomain("namespace MyApp.Domain.Bookings; public sealed class BookingSnapshot : IAggregateRoot, IDomainEvent;");

        (await AnalyzeAllAsync(domain, configuration: null, isTestProject: true)).ShouldBeEmpty();
        (await AnalyzeAllAsync(domain, Configuration, isTestProject: true)).ShouldBeEmpty();
    }

    private static CSharpCompilation CompileDomain(params string[] sources) =>
        Compile(
            "MyApp.Domain",
            sources.Select((source, index) => ($"Domain{index}.cs", source))
                .Append(("Contracts.cs", DomainContracts))
                .Append(("Usings.cs", ImplicitUsings)));

    private static CSharpCompilation CompileApplication(MetadataReference domain, params string[] sources) =>
        CompileApplication([domain], sources);

    private static CSharpCompilation CompileApplication(MetadataReference[] references, params string[] sources) =>
        Compile(
            "MyApp",
            sources.Select((source, index) => ($"Application{index}.cs", source))
                .Append(("Messaging.cs", Messaging))
                .Append(("Usings.cs", ImplicitUsings))
                .Append(("EntityFrameworkUsings.cs", EntityFrameworkUsings)),
            [.. references, .. PackageReferences("Microsoft.EntityFrameworkCore")]);

    private static Compilation WithSeverities(Compilation compilation, params (string Id, ReportDiagnostic Severity)[] severities) =>
        compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(
            severities.ToImmutableDictionary(severity => severity.Id, severity => severity.Severity)));

    // Each diagnostic as its id and the source text it points at.
    private static string[] Located(IEnumerable<Diagnostic> diagnostics) =>
        diagnostics
            .Select(diagnostic => $"{diagnostic.Id} {diagnostic.Location.SourceTree!.GetText(TestContext.Current.CancellationToken).ToString(diagnostic.Location.SourceSpan)}")
            .ToArray();
}
