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
            "'CancelBookingPolicy' in slice 'Bookings/CancelBooking' uses 'ConfirmBookingHandler' from slice 'Bookings/ConfirmBooking'");
    }

    private static CSharpCompilation CompileApplication(params (string Path, string Source)[] extraFiles)
    {
        var domain = Compile("MyApp.Domain", SourceFiles("MyApp.Domain").Append(("Usings.cs", ImplicitUsings)));

        var application = SourceFiles("MyApp")
            .Concat(extraFiles)
            .Append(("Usings.cs", ImplicitUsings))
            .Append(("WebUsings.cs", WebImplicitUsings));

        return Compile("MyApp", application, [domain.ToMetadataReference()], OutputKind.ConsoleApplication);
    }

    private static IEnumerable<(string Path, string Source)> SourceFiles(string project)
    {
        var projectDirectory = Path.Combine(ReferenceDirectory, project);
        var files = Directory.GetFiles(projectDirectory, "*.cs", SearchOption.AllDirectories);
        files.ShouldNotBeEmpty();

        return files.Select(file => (Path.GetRelativePath(projectDirectory, file), File.ReadAllText(file)));
    }
}
