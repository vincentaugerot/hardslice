using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using static HardSlice.Analyzers.Tests.AnalyzerHarness;

namespace HardSlice.Analyzers.Tests;

// Runs the HardSlice analyzers on the Bookings reference shipped by the hardslice-api template, as copied into the test output.
public sealed class BookingsReferenceTests
{
    private const string WebImplicitUsings =
        """
        global using System.Net.Http.Json;
        global using Microsoft.AspNetCore.Builder;
        global using Microsoft.AspNetCore.Hosting;
        global using Microsoft.AspNetCore.Http;
        global using Microsoft.AspNetCore.Routing;
        global using Microsoft.Extensions.Configuration;
        global using Microsoft.Extensions.DependencyInjection;
        global using Microsoft.Extensions.Hosting;
        global using Microsoft.Extensions.Logging;
        """;

    private static readonly string ReferenceDirectory = Path.Combine(AppContext.BaseDirectory, "BookingsReference");

    private static readonly string TemplateDocsDirectory = Path.Combine(AppContext.BaseDirectory, "TemplateDocs");

    private static readonly string Configuration = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TemplateConfiguration", "hardslice.json"));

    // The global config of the All mode, the mode of the template, as build/HardSlice.Analyzers.targets gives it to the compiler.
    private static readonly (string Path, string Text)[] AllMode =
    [
        ("/packages/hardslice.analyzers/build/HardSlice.All.globalconfig",
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "AnalysisModes", "HardSlice.All.globalconfig")))
    ];

    // Packages of the Bookings reference, restored for this test project at the template's versions.
    private static readonly string[] PackageAssemblyPrefixes = ["Microsoft.EntityFrameworkCore", "Npgsql", "FluentValidation"];

    [Fact]
    public async Task Reports_no_HS016_on_the_Bookings_reference()
    {
        var diagnostics = await AnalyzeAsync(CompileApplication());

        diagnostics.ShouldBeEmpty();
    }

    // Every HardSlice analyzer, in both projects, with the hardslice.json of the template: its contracts resolve,
    // Booking is an aggregate root and nothing is ambiguous.
    [Fact]
    public async Task Reports_no_HardSlice_diagnostic_on_the_Bookings_reference_with_its_configuration()
    {
        (await AnalyzeAllAsync(CompileDomain(), Configuration)).ShouldBeEmpty();
        (await AnalyzeAllAsync(CompileApplication(), Configuration)).ShouldBeEmpty();
    }

    // The template compiles in the All mode, where HS019 suggests a name: BookingConfirmed is a past fact.
    [Fact]
    public async Task Reports_no_HardSlice_diagnostic_on_the_Bookings_reference_in_the_All_mode()
    {
        (await AnalyzeAllAsync(CompileDomain(), Configuration, AllMode)).ShouldBeEmpty();
        (await AnalyzeAllAsync(CompileApplication(), Configuration, AllMode)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Reports_HS902_when_the_Bookings_reference_maps_a_domain_type_without_contract()
    {
        var diagnostic = (await AnalyzeAllAsync(MapRoomWithoutContract(), Configuration)).ShouldHaveSingleItem();

        diagnostic.Id.ShouldBe(ClassificationAnalyzer.UnclassifiedEntityId);
        diagnostic.GetMessage().ShouldBe(
            "'Room' is mapped as an EF Core entity by 'RoomConfiguration' but implements neither 'IEntity' nor 'IAggregateRoot'. "
            + "HardSlice classifies domain types only through these contracts. "
            + "Fix: implement IAggregateRoot if 'Room' is the root of its aggregate, IEntity otherwise. Doc: 'docs/rules/HS902.md'.");
    }

    [Fact]
    public async Task Reports_HS003_when_a_Bookings_endpoint_reaches_AppDbContext()
    {
        var diagnostic = (await AnalyzeAllAsync(ListBookingsWithDbContext(), Configuration)).ShouldHaveSingleItem();

        diagnostic.Id.ShouldBe(EndpointDatabaseAccessAnalyzer.DiagnosticId);
        diagnostic.GetMessage().ShouldBe(
            "endpoint 'GET /' in slice 'Bookings/ListBookings' uses 'AppDbContext' (parameter 'db'). "
            + "Endpoints never reach the database. "
            + "Fix: move the data access into the handler of a query and send the query with IMediator. Doc: 'docs/rules/HS003.md'.");
    }

    [Fact]
    public async Task Reports_HS015_when_GetBooking_returns_IResult()
    {
        var diagnostic = (await AnalyzeAllAsync(GetBookingWithResults(), Configuration)).ShouldHaveSingleItem();

        diagnostic.Id.ShouldBe(SliceEndpointAnalyzer.DiagnosticId);
        diagnostic.GetMessage().ShouldBe(
            "endpoint 'GET /{id:guid}' in slice 'Bookings/GetBooking' returns 'IResult'. "
            + "A slice endpoint returns TypedResults, so its responses are part of its signature. "
            + "Fix: build every response with TypedResults instead of Results, and declare them all as the return type of the endpoint, "
            + "such as 'Task<Results<Ok<T>, NotFound>>'. Doc: 'docs/rules/HS015.md'.");
    }

    [Fact]
    public async Task Reports_HS015_when_BookingsModule_never_maps_ArchiveBooking()
    {
        var archiveBooking =
            """
            using MyApp.Shared.Messaging;

            namespace MyApp.Features.Bookings.ArchiveBooking;

            public sealed record ArchiveBookingCommand(Guid BookingId) : ICommand<bool>;

            internal sealed class ArchiveBookingHandler : IRequestHandler<ArchiveBookingCommand, bool>
            {
                public Task<bool> Handle(ArchiveBookingCommand command, CancellationToken cancellationToken) => Task.FromResult(true);
            }

            internal static class ArchiveBookingEndpoint
            {
                public static void Map(RouteGroupBuilder group) =>
                    group.MapPost("/{id:guid}/archive", (Guid id) => TypedResults.NoContent());
            }
            """;

        var diagnostic = (await AnalyzeAllAsync(
                CompileApplication(("Features/Bookings/ArchiveBooking/ArchiveBooking.cs", archiveBooking)),
                Configuration))
            .ShouldHaveSingleItem();

        diagnostic.Id.ShouldBe(SliceEndpointAnalyzer.DiagnosticId);
        diagnostic.GetMessage().ShouldBe(
            "endpoint 'POST /{id:guid}/archive' in slice 'Bookings/ArchiveBooking' is mapped by 'ArchiveBookingEndpoint.Map', "
            + "which 'BookingsModule' never calls. "
            + "A slice endpoint is exposed only through its module, which maps every endpoint of its slices. "
            + "Fix: call 'ArchiveBookingEndpoint.Map' with the group of the module from the mapping method of 'BookingsModule'. "
            + "Doc: 'docs/rules/HS015.md'.");
    }

    [Fact]
    public async Task Reports_HS004_when_ArchiveBooking_declares_ArchiveCommand()
    {
        var diagnostic = (await AnalyzeAllAsync(ArchiveBookingWithArchiveCommand(), Configuration)).ShouldHaveSingleItem();

        diagnostic.Id.ShouldBe(SliceStructureAnalyzer.DiagnosticId);
        diagnostic.GetMessage().ShouldBe(
            "slice 'Bookings/ArchiveBooking' declares 'ArchiveCommand'. "
            + "A slice holds one request named after it, its handler and its endpoint. "
            + "Fix: rename the command to 'ArchiveBookingCommand'. Doc: 'docs/rules/HS004.md'.");
    }

    // ConfirmBooking.cs moved to the root of the module, its namespace unchanged: each type of the slice is reported.
    [Fact]
    public async Task Reports_HS004_when_ConfirmBooking_is_declared_outside_its_folder()
    {
        var path = "Features/Bookings/ConfirmBooking/ConfirmBooking.cs";
        var application = CompileApplication(
            CompileDomain(),
            ("Features/Bookings/ConfirmBooking.cs", ReferenceSource(path)),
            (path, "namespace MyApp.Features.Bookings.ConfirmBooking;"));

        var diagnostics = await AnalyzeAllAsync(application, Configuration);

        diagnostics.Select(diagnostic => diagnostic.Id).Distinct().ShouldBe([SliceStructureAnalyzer.DiagnosticId]);
        diagnostics.Select(diagnostic => diagnostic.GetMessage().Split(" is declared in ")[0]).ShouldBe(
        [
            "'ConfirmBookingCommand' of slice 'Bookings/ConfirmBooking'",
            "'ConfirmBookingValidator' of slice 'Bookings/ConfirmBooking'",
            "'ConfirmBookingHandler' of slice 'Bookings/ConfirmBooking'",
            "'ConfirmBookingEndpoint' of slice 'Bookings/ConfirmBooking'"
        ], ignoreOrder: true);
        diagnostics.First(diagnostic => diagnostic.GetMessage().StartsWith("'ConfirmBookingCommand'")).GetMessage().ShouldBe(
            "'ConfirmBookingCommand' of slice 'Bookings/ConfirmBooking' is declared in 'Features/Bookings/ConfirmBooking.cs', "
            + "outside the folder of its slice. The folder and the namespace of a slice hold the same types. "
            + "Fix: move the declaration into a file under 'Features/Bookings/ConfirmBooking/'. Doc: 'docs/rules/HS004.md'.");
    }

    [Fact]
    public async Task Reports_HS011_when_ConfirmBookingHandler_blocks_on_the_bookings_it_loads()
    {
        var diagnostic = (await AnalyzeAllAsync(ConfirmBookingThatBlocks(), Configuration)).ShouldHaveSingleItem();

        diagnostic.Id.ShouldBe(AsyncUsageAnalyzer.DiagnosticId);
        diagnostic.GetMessage().ShouldBe(
            "'ConfirmBookingHandler.Handle' blocks on 'Task<TResult>.Result'. "
            + "Code never blocks on asynchronous work. "
            + "Fix: make the caller async and await the task. Doc: 'docs/rules/HS011.md'.");
    }

    [Fact]
    public async Task Reports_HS010_when_ConfirmBookingHandler_reads_the_system_clock()
    {
        var diagnostic = (await AnalyzeAllAsync(ConfirmBookingThatReadsTheClock(), Configuration)).ShouldHaveSingleItem();

        diagnostic.Id.ShouldBe(SystemClockAnalyzer.DiagnosticId);
        diagnostic.GetMessage().ShouldBe(
            "'ConfirmBookingHandler.Handle' in slice 'Bookings/ConfirmBooking' reads 'DateTimeOffset.UtcNow'. "
            + "The domain and the slices never read the system clock. "
            + "Fix: inject TimeProvider and call GetUtcNow(), or pass the instant to the domain as a parameter. Doc: 'docs/rules/HS010.md'.");
    }

    [Fact]
    public async Task Reports_HS005_when_ConfirmBookingHandler_returns_a_DTO_that_holds_the_booking()
    {
        var diagnostic = (await AnalyzeAllAsync(ConfirmBookingThatReturnsTheBooking(), Configuration)).ShouldHaveSingleItem();

        diagnostic.Id.ShouldBe(CommandResponseAnalyzer.DiagnosticId);
        diagnostic.GetMessage().ShouldBe(
            "handler 'ConfirmBookingHandler' of command 'ConfirmBookingCommand' returns 'ConfirmationDto', whose property 'Booking' holds the entity 'Booking'. "
            + "Command handlers return an identifier, a DTO of values or nothing. "
            + "Fix: return 'BookingId', or a DTO built from the entity's values. Doc: 'docs/rules/HS005.md'.");
    }

    [Fact]
    public async Task Reports_HS006_when_GetBookingHandler_maps_the_booking_on_the_client_without_AsNoTracking()
    {
        var diagnostic = (await AnalyzeAllAsync(GetBookingThatTracksTheBooking(), Configuration)).ShouldHaveSingleItem();

        diagnostic.Id.ShouldBe(QueryHandlerTrackingAnalyzer.DiagnosticId);
        diagnostic.GetMessage().ShouldBe(
            "query handler 'GetBookingHandler' reads 'Booking' from 'Set<Booking>()' without a local guarantee that it is not tracked. "
            + "Query handlers read without tracking and never write. "
            + "Fix: add AsNoTracking() where the query starts, and project to a DTO. Doc: 'docs/rules/HS006.md'.");
    }

    [Fact]
    public async Task Reports_HS007_when_Booking_lets_its_status_be_set()
    {
        var diagnostic = (await AnalyzeAllAsync(BookingWithAStatusSetter(), Configuration)).ShouldHaveSingleItem();

        diagnostic.Id.ShouldBe(EntityStateAnalyzer.DiagnosticId);
        diagnostic.GetMessage().ShouldBe(
            "entity 'Booking' exposes 'Status' with a public setter. "
            + "The state of an entity changes only through its methods, which protect its invariants. "
            + "Fix: make the setter private and change the state in a domain method, such as 'Booking.Cancel()'. Doc: 'docs/rules/HS007.md'.");
    }

    [Fact]
    public async Task Reports_HS008_when_a_line_of_Booking_holds_its_room()
    {
        var diagnostic = (await AnalyzeAllAsync(BookingWhoseLinesHoldTheirRoom(), Configuration)).ShouldHaveSingleItem();

        diagnostic.Id.ShouldBe(AggregateReferenceAnalyzer.DiagnosticId);
        diagnostic.GetMessage().ShouldBe(
            "entity 'BookingLine' of aggregate 'Booking' references aggregate 'Room' through property 'Room'. "
            + "An aggregate references another aggregate only by its identifier. "
            + "Fix: replace the property with 'RoomId' and load 'Room' in the handler that needs it. Doc: 'docs/rules/HS008.md'.");
    }

    [Fact]
    public async Task Reports_HS009_when_BookingConfirmed_is_a_class()
    {
        var diagnostic = (await AnalyzeAllAsync(BookingConfirmedAsAClass(), Configuration)).ShouldHaveSingleItem();

        diagnostic.Id.ShouldBe(DomainEventRecordAnalyzer.DiagnosticId);
        diagnostic.GetMessage().ShouldBe(
            "domain event 'BookingConfirmed' is a class. Domain events are records. "
            + "Fix: declare it as 'public sealed record BookingConfirmed(...) : IDomainEvent'. Doc: 'docs/rules/HS009.md'.");
    }

    // A suggestion of the All mode only: the Recommended mode, the default, does not report it.
    [Fact]
    public async Task Suggests_HS019_in_the_All_mode_when_BookingConfirmed_is_named_ConfirmBooking()
    {
        (await AnalyzeAllAsync(BookingConfirmedNamedConfirmBooking(), Configuration)).ShouldBeEmpty();

        var diagnostic = (await AnalyzeAllAsync(BookingConfirmedNamedConfirmBooking(), Configuration, AllMode)).ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(DomainEventNameAnalyzer.DiagnosticId);
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Info);
        diagnostic.GetMessage().ShouldBe(
            "domain event 'ConfirmBooking' is not named as a past fact. Domain events are named in the past tense. "
            + "Fix: rename it, for example to 'BookingConfirmed'. Doc: 'docs/rules/HS019.md'.");
    }

    [Fact]
    public async Task Reports_HS000_when_CancelBooking_suppresses_HS016_without_justification()
    {
        var diagnostic = (await AnalyzeAllAsync(CancelBookingThatSuppressesHS016(), Configuration)).ShouldHaveSingleItem();

        diagnostic.Id.ShouldBe(SuppressionJustificationAnalyzer.DiagnosticId);
        diagnostic.GetMessage().ShouldBe(
            "'#pragma warning disable HS016' in 'CancelBooking.cs' has no justification. "
            + "A HardSlice rule is suppressed only with a reason a reviewer can read. "
            + "Fix: remove the suppression and fix the code; if a maintainer approved it, add '// hardslice-justification: <reason>' on the line above. "
            + "Doc: 'docs/rules/HS000.md'.");
    }

    [Fact]
    public async Task Reports_no_HS000_when_CancelBooking_justifies_its_suppression()
    {
        var path = "Features/Bookings/CancelBooking/CancelBooking.cs";
        var cancelBooking = "// hardslice-justification: approved by the maintainer in review #42\n#pragma warning disable HS016\n" + ReferenceSource(path);

        (await AnalyzeAllAsync(CompileApplication((path, cancelBooking)), Configuration)).ShouldBeEmpty();
    }

    // The sheet each diagnostic announces exists, quotes the diagnostic and has the sections of every rule sheet.
    [Theory]
    [InlineData(SuppressionJustificationAnalyzer.DiagnosticId)]
    [InlineData(EndpointDatabaseAccessAnalyzer.DiagnosticId)]
    [InlineData(SliceStructureAnalyzer.DiagnosticId)]
    [InlineData(CommandResponseAnalyzer.DiagnosticId)]
    [InlineData(QueryHandlerTrackingAnalyzer.DiagnosticId)]
    [InlineData(EntityStateAnalyzer.DiagnosticId)]
    [InlineData(AggregateReferenceAnalyzer.DiagnosticId)]
    [InlineData(DomainEventRecordAnalyzer.DiagnosticId)]
    [InlineData(SystemClockAnalyzer.DiagnosticId)]
    [InlineData(AsyncUsageAnalyzer.DiagnosticId)]
    [InlineData(SliceEndpointAnalyzer.DiagnosticId)]
    [InlineData(DomainEventNameAnalyzer.DiagnosticId)]
    [InlineData(ClassificationAnalyzer.ConfigurationId)]
    [InlineData(ClassificationAnalyzer.AmbiguityId)]
    [InlineData(ClassificationAnalyzer.UnclassifiedEntityId)]
    public async Task Points_to_a_rule_sheet_that_quotes_the_diagnostic(string ruleId)
    {
        var diagnostics = ruleId switch
        {
            SuppressionJustificationAnalyzer.DiagnosticId => await AnalyzeAllAsync(CancelBookingThatSuppressesHS016(), Configuration),
            EndpointDatabaseAccessAnalyzer.DiagnosticId => await AnalyzeAllAsync(ListBookingsWithDbContext(), Configuration),
            SliceStructureAnalyzer.DiagnosticId => await AnalyzeAllAsync(ArchiveBookingWithArchiveCommand(), Configuration),
            CommandResponseAnalyzer.DiagnosticId => await AnalyzeAllAsync(ConfirmBookingThatReturnsTheBooking(), Configuration),
            QueryHandlerTrackingAnalyzer.DiagnosticId => await AnalyzeAllAsync(GetBookingThatTracksTheBooking(), Configuration),
            EntityStateAnalyzer.DiagnosticId => await AnalyzeAllAsync(BookingWithAStatusSetter(), Configuration),
            AggregateReferenceAnalyzer.DiagnosticId => await AnalyzeAllAsync(BookingWhoseLinesHoldTheirRoom(), Configuration),
            DomainEventRecordAnalyzer.DiagnosticId => await AnalyzeAllAsync(BookingConfirmedAsAClass(), Configuration),
            DomainEventNameAnalyzer.DiagnosticId => await AnalyzeAllAsync(BookingConfirmedNamedConfirmBooking(), Configuration, AllMode),
            SystemClockAnalyzer.DiagnosticId => await AnalyzeAllAsync(ConfirmBookingThatReadsTheClock(), Configuration),
            AsyncUsageAnalyzer.DiagnosticId => await AnalyzeAllAsync(ConfirmBookingThatBlocks(), Configuration),
            SliceEndpointAnalyzer.DiagnosticId => await AnalyzeAllAsync(GetBookingWithResults(), Configuration),
            ClassificationAnalyzer.ConfigurationId => await AnalyzeAllAsync(
                CompileDomain(),
                Configuration.Replace("\"MyApp.Domain.IEntity, MyApp.Domain\"", "\"MyApp.Domain.Entity, MyApp.Domain\"")),
            ClassificationAnalyzer.AmbiguityId => await AnalyzeAllAsync(
                CompileDomain(("Bookings/BookingSnapshot.cs", "namespace MyApp.Domain.Bookings; public sealed class BookingSnapshot : IAggregateRoot, IDomainEvent { public IReadOnlyList<IDomainEvent> DequeueDomainEvents() => []; }")),
                Configuration),
            _ => await AnalyzeAllAsync(MapRoomWithoutContract(), Configuration)
        };
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(ruleId);

        var match = Regex.Match(diagnostic.GetMessage(), "Doc: 'docs/(?<path>[^']+)'\\.$");
        match.Success.ShouldBeTrue(diagnostic.GetMessage());
        var docPath = Path.Combine(TemplateDocsDirectory, match.Groups["path"].Value);
        File.Exists(docPath).ShouldBeTrue(docPath);

        var doc = File.ReadAllText(docPath);
        MarkdownSection(doc, "## Diagnostic").ShouldContain($"{ruleId}: {diagnostic.GetMessage()}");
        foreach (var section in new[] { "## Rule", "## Why", "## Bad example", "## Good example", "## How to fix", "## What the rule does not detect" })
            MarkdownSection(doc, section).Trim().ShouldNotBeEmpty(section);

        var forbiddenWorkarounds = MarkdownSection(doc, "## Forbidden workarounds");
        forbiddenWorkarounds.ShouldContain($"not valid fixes for {ruleId}");
        forbiddenWorkarounds.ShouldContain($"#pragma warning disable {ruleId}");
        forbiddenWorkarounds.ShouldContain("SuppressMessage");
        forbiddenWorkarounds.ShouldContain($"dotnet_diagnostic.{ruleId}.severity");
        forbiddenWorkarounds.ShouldContain("NoWarn");
    }

    [Fact]
    public async Task Reports_HS016_when_CancelBooking_uses_a_ConfirmBooking_type()
    {
        var violation =
            """
            namespace MyApp.Features.Bookings.CancelBooking;

            internal sealed class CancelBookingPolicy(MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingHandler handler);
            """;

        var diagnostics = await AnalyzeAsync(CompileApplication(("Features/Bookings/CancelBooking/CancelBookingPolicy.cs", violation)));

        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(SliceReferenceAnalyzer.DiagnosticId);
        diagnostic.GetMessage().ShouldBe(
            "'CancelBookingPolicy' in slice 'Bookings/CancelBooking' uses 'ConfirmBookingHandler' from slice 'Bookings/ConfirmBooking'. "
            + "Slices never reference each other. "
            + "Fix: move the shared rule into a domain aggregate, raise a domain event, or use the public contract of module 'Bookings'. "
            + "Doc: 'docs/rules/HS016.md'.");
    }

    [Fact]
    public async Task Points_to_the_HS016_rule_doc_shipped_with_the_template()
    {
        var confirmBooking =
            """
            namespace MyApp.Features.Bookings.ConfirmBooking;

            public sealed class ConfirmBookingHandler;
            """;
        var cancelBooking =
            """
            namespace MyApp.Features.Bookings.CancelBooking;

            internal sealed class CancelBookingHandler(
                MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingHandler handler);
            """;
        var compilation = Compile(
            "MyApp",
            [
                ("Features/Bookings/ConfirmBooking/ConfirmBooking.cs", confirmBooking),
                ("Features/Bookings/CancelBooking/CancelBooking.cs", cancelBooking),
                ("Usings.cs", ImplicitUsings)
            ]);
        var diagnostic = (await AnalyzeAsync(compilation))
            .ShouldHaveSingleItem();

        // The doc path is relative to the generated solution root.
        var match = Regex.Match(diagnostic.GetMessage(), "Doc: 'docs/(?<path>[^']+)'\\.$");
        match.Success.ShouldBeTrue(diagnostic.GetMessage());
        var docPath = Path.Combine(TemplateDocsDirectory, match.Groups["path"].Value);
        File.Exists(docPath).ShouldBeTrue(docPath);

        var doc = File.ReadAllText(docPath);
        MarkdownSection(doc, "## Diagnostic")
            .ShouldContain($"{diagnostic.Id}: {diagnostic.GetMessage()}");
        MarkdownSection(doc, "## How to fix").ShouldNotBeEmpty();

        var forbiddenWorkarounds = MarkdownSection(doc, "## Forbidden workarounds");
        forbiddenWorkarounds.ShouldContain("not valid fixes for HS016");
        forbiddenWorkarounds.ShouldContain("explicit maintainer decision");
        forbiddenWorkarounds.ShouldContain("HS000 justification");
        forbiddenWorkarounds.ShouldContain("#pragma warning disable HS016");
        forbiddenWorkarounds.ShouldContain("SuppressMessage");
        forbiddenWorkarounds.ShouldContain("dotnet_diagnostic.HS016.severity");
        forbiddenWorkarounds.ShouldContain("NoWarn");
    }

    private static string MarkdownSection(string document, string heading)
    {
        var headingStart = document.IndexOf(heading, StringComparison.Ordinal);
        headingStart.ShouldNotBe(-1, $"Missing Markdown heading '{heading}'.");

        var contentStart = headingStart + heading.Length;
        var nextHeading = document.IndexOf("\n## ", contentStart, StringComparison.Ordinal);
        return nextHeading < 0
            ? document.Substring(contentStart)
            : document.Substring(contentStart, nextHeading - contentStart);
    }

    // ListBookings, a slice mapped by BookingsModule whose endpoint reads the bookings through AppDbContext instead of
    // sending its query.
    private static CSharpCompilation ListBookingsWithDbContext()
    {
        var listBookings =
            """
            using Microsoft.EntityFrameworkCore;
            using MyApp.Domain.Bookings;
            using MyApp.Shared.Messaging;
            using MyApp.Shared.Persistence;

            namespace MyApp.Features.Bookings.ListBookings;

            public sealed record ListBookingsQuery : IQuery<List<Guid>>;

            internal sealed class ListBookingsHandler(AppDbContext db) : IRequestHandler<ListBookingsQuery, List<Guid>>
            {
                public Task<List<Guid>> Handle(ListBookingsQuery query, CancellationToken cancellationToken) =>
                    db.Set<Booking>().AsNoTracking().Select(booking => booking.Id.Value).ToListAsync(cancellationToken);
            }

            internal static class ListBookingsEndpoint
            {
                public static void Map(RouteGroupBuilder group) =>
                    group.MapGet("/", async (AppDbContext db, CancellationToken cancellationToken) =>
                        TypedResults.Ok(await db.Set<Booking>().AsNoTracking().Select(booking => booking.Id.Value).ToListAsync(cancellationToken)));
            }
            """;

        return CompileApplication(
            ("Features/Bookings/ListBookings/ListBookings.cs", listBookings),
            BookingsModuleMapping("MyApp.Features.Bookings.ListBookings.ListBookingsEndpoint"));
    }

    // ArchiveBooking, a slice mapped by BookingsModule whose command is not named after it.
    private static CSharpCompilation ArchiveBookingWithArchiveCommand()
    {
        var archiveBooking =
            """
            using Microsoft.AspNetCore.Http.HttpResults;
            using MyApp.Shared.Messaging;

            namespace MyApp.Features.Bookings.ArchiveBooking;

            public sealed record ArchiveCommand(Guid BookingId) : ICommand<bool>;

            internal sealed class ArchiveBookingHandler : IRequestHandler<ArchiveCommand, bool>
            {
                public Task<bool> Handle(ArchiveCommand command, CancellationToken cancellationToken) => Task.FromResult(true);
            }

            internal static class ArchiveBookingEndpoint
            {
                public static void Map(RouteGroupBuilder group) =>
                    group.MapPost("/{id:guid}/archive", async Task<Results<NoContent, NotFound>> (Guid id, IMediator mediator, CancellationToken cancellationToken) =>
                        await mediator.Send(new ArchiveCommand(id), cancellationToken) ? TypedResults.NoContent() : TypedResults.NotFound());
            }
            """;

        return CompileApplication(
            ("Features/Bookings/ArchiveBooking/ArchiveBooking.cs", archiveBooking),
            BookingsModuleMapping("MyApp.Features.Bookings.ArchiveBooking.ArchiveBookingEndpoint"));
    }

    // GetBooking, whose endpoint answers with Results instead of TypedResults.
    private static CSharpCompilation GetBookingWithResults()
    {
        var path = "Features/Bookings/GetBooking/GetBooking.cs";
        var getBooking = ReferenceSource(path);
        var typedEndpoint =
            "async Task<Results<Ok<BookingDto>, NotFound>> (Guid id, IMediator mediator, CancellationToken cancellationToken) =>\n"
            + "            await mediator.Send(new GetBookingQuery(id), cancellationToken) is { } booking ? TypedResults.Ok(booking) : TypedResults.NotFound());";
        getBooking.ShouldContain(typedEndpoint);

        return CompileApplication((path, getBooking.Replace(
            typedEndpoint,
            "async (Guid id, IMediator mediator, CancellationToken cancellationToken) =>\n"
            + "            await mediator.Send(new GetBookingQuery(id), cancellationToken) is { } booking ? Results.Ok(booking) : Results.NotFound());")));
    }

    // GetBooking, whose handler drops AsNoTracking and maps the booking with a method of its own, which EF Core runs on the
    // client: the booking is materialized and tracked.
    private static CSharpCompilation GetBookingThatTracksTheBooking()
    {
        var path = "Features/Bookings/GetBooking/GetBooking.cs";
        var getBooking = ReferenceSource(path);
        var noTracking = "            .AsNoTracking()\n";
        var projection =
            ".Select(booking => new BookingDto(\n"
            + "                booking.Id.Value, booking.RoomId, booking.Period.Start, booking.Period.End, booking.Status.ToString()))";
        var handlerEnd = "            .SingleOrDefaultAsync(cancellationToken);\n    }\n";
        foreach (var text in new[] { noTracking, projection, handlerEnd })
            getBooking.ShouldContain(text);

        return CompileApplication((path, getBooking
            .Replace(noTracking, "")
            .Replace(projection, ".Select(booking => ToDto(booking))")
            .Replace(
                handlerEnd,
                handlerEnd + "\n    private static BookingDto ToDto(Booking booking) =>\n"
                + "        new(booking.Id.Value, booking.RoomId, booking.Period.Start, booking.Period.End, booking.Status.ToString());\n")));
    }

    // The status of Booking gets a public setter: any code can cancel a booking without Booking.Cancel().
    private static CSharpCompilation BookingWithAStatusSetter()
    {
        var path = "Bookings/Booking.cs";
        var status = "public BookingStatus Status => _status;";
        var booking = File.ReadAllText(Path.Combine(ReferenceDirectory, "MyApp.Domain", path)).ReplaceLineEndings("\n");
        booking.ShouldContain(status);

        return CompileDomain((path, booking.Replace(status, "public BookingStatus Status { get => _status; set => _status = value; }")));
    }

    // Booking gets lines, and each line holds the Room aggregate instead of its identifier.
    private static CSharpCompilation BookingWhoseLinesHoldTheirRoom()
    {
        var path = "Bookings/Booking.cs";
        var status = "private BookingStatus _status;";
        var booking = File.ReadAllText(Path.Combine(ReferenceDirectory, "MyApp.Domain", path)).ReplaceLineEndings("\n");
        booking.ShouldContain(status);

        return CompileDomain(
            (path, booking.Replace(status, status + "\n    private readonly List<BookingLine> _lines = [];")),
            ("Bookings/BookingLine.cs", "namespace MyApp.Domain.Bookings; public sealed class BookingLine : IEntity { public Room Room { get; private set; } = null!; }"),
            ("Bookings/Room.cs", "namespace MyApp.Domain.Bookings; public sealed class Room : IAggregateRoot { public Guid Id { get; } public IReadOnlyList<IDomainEvent> DequeueDomainEvents() => []; }"));
    }

    // BookingConfirmed becomes a sealed class with the same constructor and properties.
    private static CSharpCompilation BookingConfirmedAsAClass()
    {
        var path = "Bookings/Events/BookingConfirmed.cs";
        var record = "public sealed record BookingConfirmed(BookingId BookingId, Guid RoomId, BookingPeriod Period) : IDomainEvent;";
        var bookingConfirmed = File.ReadAllText(Path.Combine(ReferenceDirectory, "MyApp.Domain", path)).ReplaceLineEndings("\n");
        bookingConfirmed.ShouldContain(record);

        return CompileDomain((path, bookingConfirmed.Replace(
            record,
            """
            public sealed class BookingConfirmed(BookingId bookingId, Guid roomId, BookingPeriod period) : IDomainEvent
            {
                public BookingId BookingId { get; } = bookingId;

                public Guid RoomId { get; } = roomId;

                public BookingPeriod Period { get; } = period;
            }
            """)));
    }

    // BookingConfirmed is renamed after the command that raises it.
    private static CSharpCompilation BookingConfirmedNamedConfirmBooking()
    {
        string[] paths = ["Bookings/Events/BookingConfirmed.cs", "Bookings/Booking.cs"];
        return CompileDomain(paths
            .Select(path => File.ReadAllText(Path.Combine(ReferenceDirectory, "MyApp.Domain", path)).ReplaceLineEndings("\n"))
            .Select((source, index) =>
            {
                source.ShouldContain("BookingConfirmed(");
                return (paths[index], source.Replace("BookingConfirmed(", "ConfirmBooking("));
            })
            .ToArray());
    }

    // ConfirmBooking, whose handler compares the start of the booking with the system clock.
    private static CSharpCompilation ConfirmBookingThatReadsTheClock()
    {
        var path = "Features/Bookings/ConfirmBooking/ConfirmBooking.cs";
        var confirmBooking = ReferenceSource(path);
        var period = "var period = new BookingPeriod(command.Start, command.End);";
        confirmBooking.ShouldContain(period);

        return CompileApplication((path, confirmBooking.Replace(
            period,
            period + "\n        if (period.Start < DateTimeOffset.UtcNow)\n            throw new ArgumentException(\"A booking starts in the future.\");")));
    }

    // CancelBooking, which suppresses HS016 without justification.
    private static CSharpCompilation CancelBookingThatSuppressesHS016()
    {
        var path = "Features/Bookings/CancelBooking/CancelBooking.cs";
        return CompileApplication((path, "#pragma warning disable HS016\n" + ReferenceSource(path)));
    }

    // ConfirmBooking, whose handler returns the booking it confirms in a DTO instead of its identifier.
    private static CSharpCompilation ConfirmBookingThatReturnsTheBooking()
    {
        var path = "Features/Bookings/ConfirmBooking/ConfirmBooking.cs";
        var confirmBooking = ReferenceSource(path);
        string[] replaced = ["ICommand<BookingId>;", "IRequestHandler<ConfirmBookingCommand, BookingId>", "public async Task<BookingId> Handle", "return booking.Id;"];
        foreach (var text in replaced)
            confirmBooking.ShouldContain(text);

        return CompileApplication((path, confirmBooking
            .Replace("ICommand<BookingId>;", "ICommand<ConfirmationDto>;\n\npublic sealed record ConfirmationDto(Guid RoomId, Booking Booking);")
            .Replace("IRequestHandler<ConfirmBookingCommand, BookingId>", "IRequestHandler<ConfirmBookingCommand, ConfirmationDto>")
            .Replace("public async Task<BookingId> Handle", "public async Task<ConfirmationDto> Handle")
            .Replace("return booking.Id;", "return new ConfirmationDto(command.RoomId, booking);")),
            BookingsModuleRegistration("IRequestHandler<ConfirmBookingCommand, BookingId>", "IRequestHandler<ConfirmBookingCommand, ConfirmationDto>"));
    }

    // ConfirmBooking, whose handler blocks on the bookings it loads instead of awaiting them.
    private static CSharpCompilation ConfirmBookingThatBlocks()
    {
        var path = "Features/Bookings/ConfirmBooking/ConfirmBooking.cs";
        var confirmBooking = ReferenceSource(path);
        var load = "var existingBookings = await db.Set<Booking>()";
        var loaded = ".ToListAsync(cancellationToken);";
        confirmBooking.ShouldContain(load);
        confirmBooking.ShouldContain(loaded);

        return CompileApplication((path, confirmBooking
            .Replace(load, "var existingBookings = db.Set<Booking>()")
            .Replace(loaded, ".ToListAsync(cancellationToken).Result;")));
    }

    // BookingsModule, which registers a handler under another contract.
    private static (string Path, string Source) BookingsModuleRegistration(string registered, string replacement)
    {
        var path = "Features/Bookings/BookingsModule.cs";
        var module = ReferenceSource(path);
        module.ShouldContain(registered);

        return (path, module.Replace(registered, replacement));
    }

    // BookingsModule, which also maps the endpoint class of another slice on its group.
    private static (string Path, string Source) BookingsModuleMapping(string endpoint)
    {
        var path = "Features/Bookings/BookingsModule.cs";
        var module = ReferenceSource(path);
        module.ShouldContain("GetBookingEndpoint.Map(group);");

        return (path, module.Replace("GetBookingEndpoint.Map(group);", $"GetBookingEndpoint.Map(group);\n        {endpoint}.Map(group);"));
    }

    private static string ReferenceSource(string path) =>
        File.ReadAllText(Path.Combine(ReferenceDirectory, "MyApp", path)).ReplaceLineEndings("\n");

    // Room, a domain type with no contract, mapped by an IEntityTypeConfiguration of its module.
    private static CSharpCompilation MapRoomWithoutContract()
    {
        var room =
            """
            namespace MyApp.Domain.Bookings;

            public sealed class Room
            {
                public Guid Id { get; private set; }
            }
            """;
        var roomConfiguration =
            """
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;
            using MyApp.Domain.Bookings;

            namespace MyApp.Features.Bookings;

            internal sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
            {
                public void Configure(EntityTypeBuilder<Room> builder) => builder.HasKey(room => room.Id);
            }
            """;

        return CompileApplication(CompileDomain(("Bookings/Room.cs", room)), ("Features/Bookings/RoomConfiguration.cs", roomConfiguration));
    }

    // An extra file replaces the file of the reference at the same path.
    private static CSharpCompilation CompileDomain(params (string Path, string Source)[] extraFiles) =>
        Compile(
            "MyApp.Domain",
            SourceFiles("MyApp.Domain")
                .Where(file => extraFiles.All(extra => extra.Path != file.Path))
                .Concat(extraFiles)
                .Append(("Usings.cs", ImplicitUsings)));

    private static CSharpCompilation CompileApplication(params (string Path, string Source)[] extraFiles) =>
        CompileApplication(CompileDomain(), extraFiles);

    private static CSharpCompilation CompileApplication(CSharpCompilation domain, params (string Path, string Source)[] extraFiles)
    {
        // An extra file replaces the file of the reference at the same path.
        var application = SourceFiles("MyApp")
            .Where(file => extraFiles.All(extra => extra.Path != file.Path))
            .Concat(extraFiles)
            .Append(("Usings.cs", ImplicitUsings))
            .Append(("WebUsings.cs", WebImplicitUsings));

        return Compile(
            "MyApp",
            application,
            [domain.ToMetadataReference(), .. PackageReferences(PackageAssemblyPrefixes)],
            OutputKind.ConsoleApplication);
    }

    private static IEnumerable<(string Path, string Source)> SourceFiles(string project)
    {
        var projectDirectory = Path.Combine(ReferenceDirectory, project);
        var files = Directory.GetFiles(projectDirectory, "*.cs", SearchOption.AllDirectories);
        files.ShouldNotBeEmpty();

        return files.Select(file => (Path.GetRelativePath(projectDirectory, file).Replace('\\', '/'), File.ReadAllText(file)));
    }
}
