using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using static HardSlice.Analyzers.Tests.AnalyzerHarness;

namespace HardSlice.Analyzers.Tests;

// Runs HS016 on the Bookings reference shipped by the hardslice-api template, as copied into the test output.
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

    // The sheet each configuration diagnostic announces exists, quotes the diagnostic and has the sections of every rule sheet.
    [Theory]
    [InlineData(ClassificationAnalyzer.ConfigurationId)]
    [InlineData(ClassificationAnalyzer.AmbiguityId)]
    [InlineData(ClassificationAnalyzer.UnclassifiedEntityId)]
    public async Task Points_to_a_rule_sheet_that_quotes_the_configuration_diagnostic(string ruleId)
    {
        var diagnostics = ruleId switch
        {
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
        var application = SourceFiles("MyApp")
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

        return files.Select(file => (Path.GetRelativePath(projectDirectory, file), File.ReadAllText(file)));
    }
}
