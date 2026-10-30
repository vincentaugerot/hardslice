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
            namespace MyApp.Features.Bookings.ArchiveBooking;

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

    // The sheet each diagnostic announces exists, quotes the diagnostic and has the sections of every rule sheet.
    [Theory]
    [InlineData(EndpointDatabaseAccessAnalyzer.DiagnosticId)]
    [InlineData(SystemClockAnalyzer.DiagnosticId)]
    [InlineData(AsyncUsageAnalyzer.DiagnosticId)]
    [InlineData(SliceEndpointAnalyzer.DiagnosticId)]
    [InlineData(ClassificationAnalyzer.ConfigurationId)]
    [InlineData(ClassificationAnalyzer.AmbiguityId)]
    [InlineData(ClassificationAnalyzer.UnclassifiedEntityId)]
    public async Task Points_to_a_rule_sheet_that_quotes_the_diagnostic(string ruleId)
    {
        var diagnostics = ruleId switch
        {
            EndpointDatabaseAccessAnalyzer.DiagnosticId => await AnalyzeAllAsync(ListBookingsWithDbContext(), Configuration),
            SystemClockAnalyzer.DiagnosticId => await AnalyzeAllAsync(ConfirmBookingThatReadsTheClock(), Configuration),
            AsyncUsageAnalyzer.DiagnosticId => await AnalyzeAllAsync(ConfirmBookingThatBlocks(), Configuration),
            SliceEndpointAnalyzer.DiagnosticId => await AnalyzeAllAsync(GetBookingWithResults(), Configuration),
            ClassificationAnalyzer.ConfigurationId => await AnalyzeAllAsync(
                CompileDomain(),
                Configuration.Replace("\"MyApp.Domain.IEntity, MyApp.Domain\"", "\"MyApp.Domain.Entity, MyApp.Domain\"")),
            ClassificationAnalyzer.AmbiguityId => await AnalyzeAllAsync(
                CompileDomain(("Bookings/BookingSnapshot.cs", "namespace MyApp.Domain.Bookings; public sealed class BookingSnapshot : IAggregateRoot, IDomainEvent;")),
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
    // sending a query.
    private static CSharpCompilation ListBookingsWithDbContext()
    {
        var listBookings =
            """
            using Microsoft.EntityFrameworkCore;
            using MyApp.Domain.Bookings;
            using MyApp.Shared.Persistence;

            namespace MyApp.Features.Bookings.ListBookings;

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

    private static CSharpCompilation CompileDomain(params (string Path, string Source)[] extraFiles) =>
        Compile("MyApp.Domain", SourceFiles("MyApp.Domain").Concat(extraFiles).Append(("Usings.cs", ImplicitUsings)));

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
