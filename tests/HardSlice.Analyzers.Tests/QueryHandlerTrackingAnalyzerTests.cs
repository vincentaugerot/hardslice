using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using static HardSlice.Analyzers.Tests.AnalyzerHarness;

namespace HardSlice.Analyzers.Tests;

// HS006: a query handler reads without tracking and never writes.
public sealed class QueryHandlerTrackingAnalyzerTests
{
    private const string Rule = "Query handlers read without tracking and never write.";

    private const string ReadFix = "Fix: add AsNoTracking() where the query starts, and project to a DTO. Doc: 'docs/rules/HS006.md'.";

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

    // The Booking aggregate, its child entity BookingLine, a value object and a read model that is not an entity.
    private const string Domain =
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

            public sealed class Booking : IAggregateRoot
            {
                private readonly List<BookingLine> _lines = [];

                public BookingId Id { get; private set; } = new(Guid.NewGuid());

                public Guid RoomId { get; private set; }

                public bool IsCancelled { get; private set; }

                public IReadOnlyList<BookingLine> Lines => _lines;

                public string Describe() => $"{RoomId}";
            }

            public sealed class BookingLine : IEntity
            {
                public Guid Id { get; private set; } = Guid.NewGuid();

                public string Label { get; private set; } = "";
            }

            public sealed class OccupancyView
            {
                public Guid RoomId { get; set; }

                public int Bookings { get; set; }
            }
        }
        """;

    private const string Messaging =
        """
        namespace MyApp.Shared.Messaging
        {
            public interface IRequest<TResponse>;

            public interface ICommand<TResponse> : IRequest<TResponse>;

            public interface IQuery<TResponse> : IRequest<TResponse>;

            public interface IRequestHandler<in TRequest, TResponse> where TRequest : IRequest<TResponse>
            {
                Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
            }

            public sealed record Result<T>(T? Value, string? Error);
        }
        """;

    private const string Persistence =
        """
        using Microsoft.EntityFrameworkCore;
        using MyApp.Domain.Bookings;

        namespace MyApp.Shared.Persistence
        {
            public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
            {
                public DbSet<Booking> Bookings => Set<Booking>();

                public DbSet<OccupancyView> Occupancy => Set<OccupancyView>();
            }
        }
        """;

    // The usings of each handler: the domain, the messaging, the context and EF Core.
    private const string HandlerUsings =
        """
        global using Microsoft.EntityFrameworkCore;
        global using MyApp.Domain.Bookings;
        global using MyApp.Shared.Messaging;
        global using MyApp.Shared.Persistence;
        """;

    // The DTOs and queries the handlers below share.
    private const string Queries =
        """
        namespace MyApp.Features.Bookings.ListBookings
        {
            public sealed record BookingDto(Guid Id, Guid RoomId);

            public sealed record DescribedDto(string Description);

            public sealed record LinesDto(BookingId Id, IReadOnlyList<BookingLine> Lines);

            public sealed record ListBookingsQuery(bool ReadOnly) : IQuery<IReadOnlyList<BookingDto>>;

            public sealed record ListBookingsCommand : ICommand<int>;
        }
        """;

    private static readonly MetadataReference[] EntityFramework = [.. PackageReferences("Microsoft.EntityFrameworkCore")];

    private static CSharpCompilation CompileDomain() =>
        Compile("MyApp.Domain", [("Domain.cs", Domain), ("Usings.cs", ImplicitUsings)]);

    // The application, with the messaging of the template, its context and EF Core, referencing the domain as a project.
    private static Task<ImmutableArray<Diagnostic>> VerifyAsync(params string[] markedSources) =>
        VerifyAsync([Reference(CompileDomain(), asMetadata: false)], markedSources);

    private static Task<ImmutableArray<Diagnostic>> VerifyAsync(MetadataReference[] references, params string[] markedSources) =>
        AnalyzerHarness.VerifyAsync(
            new QueryHandlerTrackingAnalyzer(),
            QueryHandlerTrackingAnalyzer.DiagnosticId,
            "MyApp",
            Configuration,
            [.. references, .. EntityFramework],
            OutputKind.DynamicallyLinkedLibrary,
            [.. markedSources, Messaging, Persistence, HandlerUsings, Queries]);

    // The first sentence of each message, without its rule and its fix.
    private static IEnumerable<string> Subjects(ImmutableArray<Diagnostic> diagnostics) =>
        diagnostics.Select(diagnostic => diagnostic.GetMessage().Split(". ")[0]);

    [Fact]
    public async Task Reports_a_query_handler_that_reads_the_aggregate_without_AsNoTracking()
    {
        var diagnostics = await VerifyAsync(
            """
            namespace MyApp.Features.Bookings.GetBooking;

            public sealed record GetBookingQuery(Guid BookingId) : IQuery<Booking?>;

            internal sealed class GetBookingHandler(AppDbContext db) : IRequestHandler<GetBookingQuery, Booking?>
            {
                public Task<Booking?> Handle(GetBookingQuery query, CancellationToken cancellationToken) =>
                    db.Set<Booking>().Where(booking => booking.Id == new BookingId(query.BookingId)).[|SingleOrDefaultAsync|](cancellationToken);
            }
            """);

        diagnostics.ShouldHaveSingleItem().GetMessage().ShouldBe(
            "query handler 'GetBookingHandler' reads 'Booking' from 'Set<Booking>()' without a local guarantee that it is not tracked. "
            + Rule + " " + ReadFix);
    }

    [Fact]
    public async Task Reports_the_writes_of_EF_Core()
    {
        var diagnostics = await VerifyAsync(
            """
            namespace MyApp.Features.Bookings.ListBookings;

            internal sealed class ListBookingsHandler(AppDbContext db) : IRequestHandler<ListBookingsQuery, IReadOnlyList<BookingDto>>
            {
                public async Task<IReadOnlyList<BookingDto>> Handle(ListBookingsQuery query, CancellationToken cancellationToken)
                {
                    await db.Bookings.Where(booking => booking.IsCancelled).[|ExecuteDeleteAsync|](cancellationToken);
                    db.Bookings.Where(booking => booking.IsCancelled).[|ExecuteUpdate|](setters => setters.SetProperty(booking => booking.RoomId, Guid.Empty));
                    await db.Database.[|ExecuteSqlRawAsync|]("DELETE FROM bookings", cancellationToken);
                    db.Database.[|ExecuteSql|]($"DELETE FROM bookings");
                    db.[|Add|](new Booking());
                    await db.Bookings.[|AddAsync|](new Booking(), cancellationToken);
                    db.[|AttachRange|](new Booking());
                    db.Bookings.[|Update|](new Booking());
                    db.Bookings.[|RemoveRange|](new Booking());
                    db.[|SaveChanges|]();
                    await db.[|SaveChangesAsync|](cancellationToken);
                    return [];
                }
            }
            """);

        diagnostics[0].GetMessage().ShouldBe(
            "query handler 'ListBookingsHandler' writes through 'ExecuteDeleteAsync'. "
            + Rule + " Fix: move the write into a command slice. Doc: 'docs/rules/HS006.md'.");
    }

    [Fact]
    public async Task Reports_tracking_turned_back_on_or_reconfigured_and_Find()
    {
        var diagnostics = await VerifyAsync(
            """
            namespace MyApp.Features.Bookings.ListBookings;

            internal sealed class ListBookingsHandler(AppDbContext db) : IRequestHandler<ListBookingsQuery, IReadOnlyList<BookingDto>>
            {
                public async Task<IReadOnlyList<BookingDto>> Handle(ListBookingsQuery query, CancellationToken cancellationToken)
                {
                    db.ChangeTracker.[|QueryTrackingBehavior|] = QueryTrackingBehavior.NoTracking;
                    var found = await db.Bookings.[|FindAsync|]([new BookingId(Guid.Empty)], cancellationToken);
                    var other = db.[|Find|]<Booking>(new BookingId(Guid.Empty));
                    var tracked = await db.Bookings.AsNoTracking().[|AsTracking|]().ToListAsync(cancellationToken);
                    return [];
                }
            }
            """);

        Subjects(diagnostics).ShouldBe(
        [
            "query handler 'ListBookingsHandler' reconfigures tracking through 'ChangeTracker.QueryTrackingBehavior'",
            "query handler 'ListBookingsHandler' reads with 'FindAsync', which always tracks the entity it returns",
            "query handler 'ListBookingsHandler' reads with 'Find', which always tracks the entity it returns",
            "query handler 'ListBookingsHandler' turns tracking back on with 'AsTracking'"
        ]);
        diagnostics[0].GetMessage().ShouldEndWith(
            "Fix: remove the assignment, which changes the context for every component of the scope, and start each query with AsNoTracking(). "
            + "Doc: 'docs/rules/HS006.md'.");
        diagnostics[3].GetMessage().ShouldEndWith("Fix: remove AsTracking(): the query starts with AsNoTracking(). Doc: 'docs/rules/HS006.md'.");
    }

    [Fact]
    public async Task Reports_the_reads_that_materialize_an_entity_without_a_local_guarantee()
    {
        var diagnostics = await VerifyAsync(
            """
            namespace MyApp.Features.Bookings.ListBookings;

            internal sealed class ListBookingsHandler(AppDbContext db) : IRequestHandler<ListBookingsQuery, IReadOnlyList<BookingDto>>
            {
                public async Task<IReadOnlyList<BookingDto>> Handle(ListBookingsQuery query, CancellationToken cancellationToken)
                {
                    var all = await db.Set<Booking>().[|ToListAsync|](cancellationToken);
                    var anonymous = await db.Bookings.Select(booking => new { booking, booking.Id }).[|ToListAsync|](cancellationToken);
                    var described = db.Bookings.Select(booking => Describe(booking)).[|ToList|]();
                    var built = db.Bookings.Select(booking => new DescribedDto(booking.Describe())).[|ToArray|]();
                    var boxed = db.Bookings.Select(booking => (object)booking).[|First|]();
                    var dtos = db.Bookings.Select(booking => new LinesDto(booking.Id, booking.Lines)).[|ToList|]();
                    var lines = db.Bookings.SelectMany(booking => booking.Lines).[|ToHashSet|]();
                    var raw = db.Bookings.FromSqlRaw("SELECT * FROM bookings").[|ToList|]();
                    var byId = await db.Bookings.[|ToDictionaryAsync|](booking => booking.Id, cancellationToken);
                    var enumerated = db.Bookings.Where(booking => !booking.IsCancelled).[|AsEnumerable|]().Select(booking => booking.Id).ToList();
                    db.Bookings.[|Load|]();
                    foreach (var booking in [|db.Bookings|])
                        _ = booking;
                    await foreach (var booking in [|db.Bookings|])
                        _ = booking;

                    IQueryable<Booking> conditional = db.Set<Booking>();
                    if (query.ReadOnly)
                        conditional = conditional.AsNoTracking();
                    return (await conditional.[|ToListAsync|](cancellationToken)).Select(booking => new BookingDto(booking.Id.Value, booking.RoomId)).ToList();
                }

                private static string Describe(Booking booking) => booking.Describe();
            }
            """);

        Subjects(diagnostics).ShouldBe(
        [
            "query handler 'ListBookingsHandler' reads 'Booking' from 'Set<Booking>()' without a local guarantee that it is not tracked",
            "query handler 'ListBookingsHandler' reads 'Booking' from 'AppDbContext.Bookings' without a local guarantee that it is not tracked",
            "query handler 'ListBookingsHandler' reads 'Booking' from 'AppDbContext.Bookings' without a local guarantee that it is not tracked",
            "query handler 'ListBookingsHandler' reads 'Booking' from 'AppDbContext.Bookings' without a local guarantee that it is not tracked",
            "query handler 'ListBookingsHandler' reads 'Booking' from 'AppDbContext.Bookings' without a local guarantee that it is not tracked",
            "query handler 'ListBookingsHandler' reads 'BookingLine' from 'AppDbContext.Bookings' without a local guarantee that it is not tracked",
            "query handler 'ListBookingsHandler' reads 'BookingLine' from 'AppDbContext.Bookings' without a local guarantee that it is not tracked",
            "query handler 'ListBookingsHandler' reads 'Booking' from 'FromSqlRaw(...)' without a local guarantee that it is not tracked",
            "query handler 'ListBookingsHandler' reads 'Booking' from 'AppDbContext.Bookings' without a local guarantee that it is not tracked",
            "query handler 'ListBookingsHandler' reads 'Booking' from 'AppDbContext.Bookings' without a local guarantee that it is not tracked",
            "query handler 'ListBookingsHandler' reads 'Booking' from 'AppDbContext.Bookings' without a local guarantee that it is not tracked",
            "query handler 'ListBookingsHandler' reads 'Booking' from 'AppDbContext.Bookings' without a local guarantee that it is not tracked",
            "query handler 'ListBookingsHandler' reads 'Booking' from 'AppDbContext.Bookings' without a local guarantee that it is not tracked",
            "query handler 'ListBookingsHandler' reads 'Booking' from 'Set<Booking>()' without a local guarantee that it is not tracked"
        ]);
    }

    [Fact]
    public async Task Accepts_reads_guaranteed_without_tracking_and_reads_of_values()
    {
        await VerifyAsync(
            """
            namespace MyApp.Features.Bookings.ListBookings;

            internal sealed class ListBookingsHandler(AppDbContext db) : IRequestHandler<ListBookingsQuery, IReadOnlyList<BookingDto>>
            {
                public async Task<IReadOnlyList<BookingDto>> Handle(ListBookingsQuery query, CancellationToken cancellationToken)
                {
                    var ids = await db.Set<Booking>().Select(booking => booking.Id).ToListAsync(cancellationToken);
                    var scalars = await db.Bookings.Select(booking => new BookingDto(booking.Id.Value, booking.RoomId)).ToListAsync(cancellationToken);
                    var counts = db.Bookings.Select(booking => new { booking.RoomId, Lines = booking.Lines.Count(), Labels = booking.Lines.Select(line => line.Label).ToList() }).ToList();
                    var shadow = db.Bookings.Select(booking => EF.Property<string>(booking, "Note")).ToList();
                    var count = await db.Bookings.CountAsync(cancellationToken);
                    var any = db.Bookings.Any(booking => booking.IsCancelled);
                    var resolved = await db.Bookings.AsNoTrackingWithIdentityResolution().Include(booking => booking.Lines).ToListAsync(cancellationToken);
                    var untracked = await db.Bookings.AsNoTracking().Select(booking => new { booking }).ToListAsync(cancellationToken);
                    var occupancy = await db.Occupancy.ToListAsync(cancellationToken);
                    var envelopes = db.Bookings.Select(booking => new Result<BookingDto>(new BookingDto(booking.Id.Value, booking.RoomId), null)).ToList();
                    var subqueries = await db.Bookings.AsNoTracking()
                        .Select(booking => new { booking.Id, Lines = db.Set<BookingLine>().Where(line => line.Label != "").ToList() })
                        .ToListAsync(cancellationToken);

                    var filtered = db.Set<Booking>().AsNoTracking();
                    if (query.ReadOnly)
                        filtered = filtered.Where(booking => booking.IsCancelled);
                    foreach (var booking in filtered)
                        _ = booking;

                    IQueryable<Booking> chained = db.Bookings.AsNoTracking();
                    var next = chained.Where(booking => booking.IsCancelled);
                    return await next.Select(booking => new BookingDto(booking.Id.Value, booking.RoomId)).Concat(next.Select(booking => new BookingDto(booking.Id.Value, Guid.Empty))).ToListAsync(cancellationToken);
                }
            }
            """);
    }

    // A form the rule cannot analyze is neither reported nor declared compliant.
    [Fact]
    public async Task Does_not_analyze_queries_it_cannot_follow()
    {
        await VerifyAsync(
            """
            namespace MyApp.Features.Bookings.ListBookings;

            internal sealed class ListBookingsHandler(AppDbContext db, IQueryable<Booking> injected) : IRequestHandler<ListBookingsQuery, IReadOnlyList<BookingDto>>
            {
                private static readonly Func<AppDbContext, IAsyncEnumerable<Booking>> Compiled = EF.CompileAsyncQuery((AppDbContext context) => context.Bookings);

                private readonly IQueryable<Booking> _bookings = db.Bookings;

                public async Task<IReadOnlyList<BookingDto>> Handle(ListBookingsQuery query, CancellationToken cancellationToken)
                {
                    var fromField = await _bookings.ToListAsync(cancellationToken);
                    var fromParameter = await injected.ToListAsync(cancellationToken);
                    var fromHelper = await Cancelled().ToListAsync(cancellationToken);
                    await foreach (var booking in Compiled(db))
                        _ = booking;

                    var reassigned = db.Bookings.AsNoTracking();
                    if (query.ReadOnly)
                        reassigned = Cancelled();
                    var fromReassigned = await reassigned.ToListAsync(cancellationToken);

                    IQueryable<Booking> declaredOnly;
                    declaredOnly = db.Bookings;
                    var fromDeclared = await declaredOnly.ToListAsync(cancellationToken);

                    await Persist(db, cancellationToken);
                    return [];
                }

                private IQueryable<Booking> Cancelled() => db.Bookings.AsNoTracking().Where(booking => booking.IsCancelled);

                private static Task Persist(AppDbContext context, CancellationToken cancellationToken) => Task.CompletedTask;
            }

            // A command handler writes: the rule is about queries.
            internal sealed class SaveBookingsHandler(AppDbContext db) : IRequestHandler<ListBookingsCommand, int>
            {
                public async Task<int> Handle(ListBookingsCommand command, CancellationToken cancellationToken)
                {
                    var bookings = await db.Bookings.ToListAsync(cancellationToken);
                    return await db.SaveChangesAsync(cancellationToken);
                }
            }
            """);
    }

    // Lambdas, local functions, other methods, the constructor and the initializers of the handler.
    [Fact]
    public async Task Examines_all_the_code_of_the_handler()
    {
        await VerifyAsync(
            """
            namespace MyApp.Features.Bookings.ListBookings;

            internal sealed class ListBookingsHandler : IRequestHandler<ListBookingsQuery, IReadOnlyList<BookingDto>>
            {
                private readonly AppDbContext _db;

                private readonly Lazy<List<Booking>> _all;

                public ListBookingsHandler(AppDbContext db)
                {
                    _db = db;
                    _all = new(() => db.Bookings.[|ToList|]());
                    db.ChangeTracker.[|QueryTrackingBehavior|] = QueryTrackingBehavior.TrackAll;
                }

                public async Task<IReadOnlyList<BookingDto>> Handle(ListBookingsQuery query, CancellationToken cancellationToken)
                {
                    Func<Task<int>> save = () => _db.[|SaveChangesAsync|](cancellationToken);
                    await Touch();
                    return [];

                    async Task Touch() => await _db.Bookings.[|FirstAsync|](cancellationToken);
                }

                private void Remove(Booking booking) => _db.[|Remove|](booking);

                private sealed class Writer(AppDbContext db)
                {
                    public int Save() => db.[|SaveChanges|]();
                }
            }

            internal sealed class CountHandler(AppDbContext db) : IRequestHandler<ListBookingsQuery, IReadOnlyList<BookingDto>>
            {
                private readonly Task<int> _saved = db.[|SaveChangesAsync|]();

                public Task<IReadOnlyList<BookingDto>> Handle(ListBookingsQuery query, CancellationToken cancellationToken) =>
                    Task.FromResult<IReadOnlyList<BookingDto>>([]);
            }
            """);
    }

    [Fact]
    public async Task Reports_inherited_and_generic_handlers()
    {
        var diagnostics = await VerifyAsync(
            """
            using MyApp.Domain;

            namespace MyApp.Features.Bookings.ListBookings;

            internal abstract class ReadHandler<TQuery, TEntity>(AppDbContext db) : IRequestHandler<TQuery, IReadOnlyList<TEntity>>
                where TQuery : IQuery<IReadOnlyList<TEntity>>
                where TEntity : class, IEntity
            {
                public async Task<IReadOnlyList<TEntity>> Handle(TQuery query, CancellationToken cancellationToken) =>
                    await db.Set<TEntity>().[|ToListAsync|](cancellationToken);
            }

            public sealed record AllBookingsQuery : IQuery<IReadOnlyList<Booking>>;

            internal sealed class AllBookingsHandler(AppDbContext db) : ReadHandler<AllBookingsQuery, Booking>(db)
            {
                public Task Purge() => db.Bookings.[|ExecuteDeleteAsync|]();
            }
            """);

        Subjects(diagnostics).ShouldBe(
        [
            "query handler 'ReadHandler<TQuery, TEntity>' reads 'TEntity' from 'Set<TEntity>()' without a local guarantee that it is not tracked",
            "query handler 'AllBookingsHandler' writes through 'ExecuteDeleteAsync'"
        ]);
    }

    // The domain and the context compiled on their own, read from metadata: 'DbSet<T>' of a context of a referenced project.
    [Fact]
    public async Task Reads_the_domain_and_a_context_of_other_assemblies_from_metadata()
    {
        var domain = Reference(CompileDomain(), asMetadata: true);
        var persistence = Compile(
            "MyApp.Persistence",
            [
                ("Context.cs",
                    """
                    using Microsoft.EntityFrameworkCore;
                    using MyApp.Domain.Bookings;

                    namespace MyApp.Persistence;

                    public sealed class BookingsContext(DbContextOptions<BookingsContext> options) : DbContext(options)
                    {
                        public DbSet<Booking> Bookings => Set<Booking>();

                        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => base.SaveChangesAsync(cancellationToken);
                    }
                    """),
                ("Usings.cs", ImplicitUsings)
            ],
            [domain, .. EntityFramework]);

        var diagnostics = await VerifyAsync(
            [domain, Reference(persistence, asMetadata: true)],
            """
            using MyApp.Persistence;

            namespace MyApp.Features.Bookings.ListBookings;

            internal sealed class ListBookingsHandler(BookingsContext context) : IRequestHandler<ListBookingsQuery, IReadOnlyList<BookingDto>>
            {
                public async Task<IReadOnlyList<BookingDto>> Handle(ListBookingsQuery query, CancellationToken cancellationToken)
                {
                    var lines = await context.Bookings.Where(booking => !booking.IsCancelled).Select(booking => booking.Lines).[|ToListAsync|](cancellationToken);
                    await context.[|SaveChangesAsync|](cancellationToken);
                    return [];
                }
            }
            """);

        Subjects(diagnostics).ShouldBe(
        [
            "query handler 'ListBookingsHandler' reads 'BookingLine' from 'BookingsContext.Bookings' without a local guarantee that it is not tracked",
            "query handler 'ListBookingsHandler' writes through 'SaveChangesAsync'"
        ]);
    }

    // The contracts are invisible from the domain, and from an application without hardslice.json: HS900 reports the latter.
    // Without EF Core, nothing is tracked.
    [Fact]
    public async Task Does_nothing_without_the_contracts_or_EF_Core()
    {
        var handler =
            """
            namespace MyApp.Features.Bookings.ListBookings;

            internal sealed class ListBookingsHandler(AppDbContext db) : IRequestHandler<ListBookingsQuery, IReadOnlyList<BookingDto>>
            {
                public async Task<IReadOnlyList<BookingDto>> Handle(ListBookingsQuery query, CancellationToken cancellationToken)
                {
                    await db.SaveChangesAsync(cancellationToken);
                    return [];
                }
            }
            """;
        var application = Compile(
            "MyApp",
            [("Handler.cs", handler), ("Messaging.cs", Messaging), ("Persistence.cs", Persistence), ("HandlerUsings.cs", HandlerUsings), ("Queries.cs", Queries), ("Usings.cs", ImplicitUsings)],
            [Reference(CompileDomain(), asMetadata: true), .. EntityFramework]);

        (await application.WithAnalyzers([new QueryHandlerTrackingAnalyzer()], Options(configuration: null))
                .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken))
            .ShouldBeEmpty();
        (await CompileDomain().WithAnalyzers([new QueryHandlerTrackingAnalyzer()], Options(Configuration))
                .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken))
            .ShouldBeEmpty();

        var withoutEntityFramework = Compile(
            "MyApp",
            [
                ("Handler.cs",
                    """
                    using MyApp.Domain.Bookings;
                    using MyApp.Shared.Messaging;

                    namespace MyApp.Features.Bookings.ListBookings;

                    public sealed record ListBookingsQuery : IQuery<List<Booking>>;

                    internal sealed class ListBookingsHandler(IQueryable<Booking> bookings) : IRequestHandler<ListBookingsQuery, List<Booking>>
                    {
                        public Task<List<Booking>> Handle(ListBookingsQuery query, CancellationToken cancellationToken) => Task.FromResult(bookings.ToList());
                    }
                    """),
                ("Messaging.cs", Messaging),
                ("Usings.cs", ImplicitUsings)
            ],
            [Reference(CompileDomain(), asMetadata: true)]);
        (await withoutEntityFramework.WithAnalyzers([new QueryHandlerTrackingAnalyzer()], Options(Configuration))
                .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken))
            .ShouldBeEmpty();
    }

    // An auto-generated header does not exempt a handler from the rule.
    [Fact]
    public async Task Reports_generated_code()
    {
        await VerifyAsync(
            """
            // <auto-generated/>
            namespace MyApp.Features.Bookings.ListBookings;

            internal sealed class ListBookingsHandler(AppDbContext db) : IRequestHandler<ListBookingsQuery, IReadOnlyList<BookingDto>>
            {
                public async Task<IReadOnlyList<BookingDto>> Handle(ListBookingsQuery query, CancellationToken cancellationToken)
                {
                    await db.[|SaveChangesAsync|](cancellationToken);
                    return [];
                }
            }
            """);
    }

    [Fact]
    public async Task Does_not_apply_to_test_projects()
    {
        var compilation = Compile(
            "MyApp",
            [
                ("Handler.cs",
                    """
                    namespace MyApp.Tests;

                    internal sealed class FakeHandler(AppDbContext db) : IRequestHandler<MyApp.Features.Bookings.ListBookings.ListBookingsQuery, IReadOnlyList<MyApp.Features.Bookings.ListBookings.BookingDto>>
                    {
                        public async Task<IReadOnlyList<MyApp.Features.Bookings.ListBookings.BookingDto>> Handle(MyApp.Features.Bookings.ListBookings.ListBookingsQuery query, CancellationToken cancellationToken)
                        {
                            await db.SaveChangesAsync(cancellationToken);
                            return [];
                        }
                    }
                    """),
                ("Messaging.cs", Messaging),
                ("Persistence.cs", Persistence),
                ("HandlerUsings.cs", HandlerUsings),
                ("Queries.cs", Queries),
                ("Usings.cs", ImplicitUsings)
            ],
            [Reference(CompileDomain(), asMetadata: true), .. EntityFramework]);

        var diagnostics = await compilation
            .WithAnalyzers([new QueryHandlerTrackingAnalyzer()], Options(Configuration, isTestProject: true))
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        diagnostics.ShouldBeEmpty();
    }
}
