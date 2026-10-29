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

    [Fact]
    public async Task Reports_no_HS016_on_the_Bookings_reference()
    {
        var diagnostics = await AnalyzeAsync(CompileApplication());

        diagnostics.ShouldBeEmpty();
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

    private static CSharpCompilation CompileApplication(params (string Path, string Source)[] extraFiles)
    {
        var domain = Compile("MyApp.Domain", SourceFiles("MyApp.Domain").Append(("Usings.cs", ImplicitUsings)));

        var application = SourceFiles("MyApp")
            .Concat(extraFiles)
            .Append(("Usings.cs", ImplicitUsings))
            .Append(("WebUsings.cs", WebImplicitUsings));

        return Compile("MyApp", application, [domain.ToMetadataReference(), .. PersistenceReferences()], OutputKind.ConsoleApplication);
    }

    // EF Core and Npgsql, as restored for this test project at the template's versions.
    private static IEnumerable<MetadataReference> PersistenceReferences() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => Path.GetFileName(path) is var name
                && (name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) || name.StartsWith("Npgsql", StringComparison.Ordinal)))
            .Select(path => MetadataReference.CreateFromFile(path));

    private static IEnumerable<(string Path, string Source)> SourceFiles(string project)
    {
        var projectDirectory = Path.Combine(ReferenceDirectory, project);
        var files = Directory.GetFiles(projectDirectory, "*.cs", SearchOption.AllDirectories);
        files.ShouldNotBeEmpty();

        return files.Select(file => (Path.GetRelativePath(projectDirectory, file), File.ReadAllText(file)));
    }
}
