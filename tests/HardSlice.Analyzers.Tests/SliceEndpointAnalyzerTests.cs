using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using static HardSlice.Analyzers.Tests.AnalyzerHarness;

namespace HardSlice.Analyzers.Tests;

// HS015: the endpoint of a slice is mapped in its slice, on the group of its module, by a method its module calls, and
// returns a type of Microsoft.AspNetCore.Http.HttpResults.
public sealed class SliceEndpointAnalyzerTests
{
    private const string WebUsings =
        """
        global using Microsoft.AspNetCore.Builder;
        global using Microsoft.AspNetCore.Http;
        global using Microsoft.AspNetCore.Http.HttpResults;
        global using Microsoft.AspNetCore.Routing;
        """;

    private const string Contracts =
        """
        namespace MyApp.Features.Bookings;

        public sealed record BookingDto(Guid Id);
        """;

    private static Task<ImmutableArray<Diagnostic>> VerifyAsync(params string[] markedSources) =>
        AnalyzerHarness.VerifyAsync(
            new SliceEndpointAnalyzer(),
            SliceEndpointAnalyzer.DiagnosticId,
            [],
            OutputKind.DynamicallyLinkedLibrary,
            [.. markedSources, WebUsings, Contracts]);

    // BookingsModule, which maps the given endpoint classes on its group.
    private static string BookingsModule(params string[] endpoints) =>
        $$"""
        namespace MyApp.Features.Bookings;

        public static class BookingsModule
        {
            public static IEndpointRouteBuilder MapBookingsModule(this IEndpointRouteBuilder endpoints)
            {
                var group = endpoints.MapGroup("/bookings");
                {{string.Join("\n        ", endpoints.Select(endpoint => $"{endpoint}.Map(group);"))}}
                return endpoints;
            }
        }
        """;

    [Fact]
    public async Task Accepts_the_endpoints_of_Bookings_mapped_on_the_group_of_their_module_with_TypedResults()
    {
        await VerifyAsync(
            BookingsModule(
                "MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingEndpoint",
                "MyApp.Features.Bookings.CancelBooking.CancelBookingEndpoint",
                "MyApp.Features.Bookings.GetBooking.GetBookingEndpoint"),
            """
            namespace MyApp.Features.Bookings.ConfirmBooking;

            internal static class ConfirmBookingEndpoint
            {
                public static void Map(RouteGroupBuilder group) =>
                    group.MapPost("/", async (BookingDto booking, CancellationToken cancellationToken) =>
                    {
                        await Task.Delay(0, cancellationToken);
                        return TypedResults.Created($"/bookings/{booking.Id}");
                    });
            }
            """,
            """
            namespace MyApp.Features.Bookings.CancelBooking;

            internal static class CancelBookingEndpoint
            {
                public static void Map(RouteGroupBuilder group) =>
                    group.MapPost("/{id:guid}/cancel", async Task<Results<NoContent, NotFound>> (Guid id) =>
                        await Task.FromResult(id != Guid.Empty) ? TypedResults.NoContent() : TypedResults.NotFound());
            }
            """,
            """
            namespace MyApp.Features.Bookings.GetBooking;

            internal static class GetBookingEndpoint
            {
                public static void Map(RouteGroupBuilder group) =>
                    group.MapGet("/{id:guid}", async Task<Results<Ok<BookingDto>, NotFound>> (Guid id) =>
                        await Task.FromResult<BookingDto?>(new BookingDto(id)) is { } booking ? TypedResults.Ok(booking) : TypedResults.NotFound());
            }
            """);
    }

    // A nested group, a generic method group, a mapping in a local function and a module that calls through a method group.
    [Fact]
    public async Task Accepts_nested_groups_generic_method_groups_and_modules_that_pass_method_groups()
    {
        await VerifyAsync(
            """
            namespace MyApp.Features.Bookings;

            public static class BookingsModule
            {
                public static IEndpointRouteBuilder MapBookingsModule(this IEndpointRouteBuilder endpoints)
                {
                    var group = endpoints.MapGroup("/bookings");
                    Action<RouteGroupBuilder>[] slices = [MyApp.Features.Bookings.RescheduleBooking.RescheduleBookingEndpoint.Map];
                    foreach (var slice in slices)
                        slice(group);
                    MyApp.Features.Bookings.ImportBookings.ImportBookingsEndpoint.Map(group);
                    return endpoints;
                }
            }
            """,
            """
            namespace MyApp.Features.Bookings.RescheduleBooking;

            internal static class RescheduleBookingEndpoint
            {
                public static void Map(RouteGroupBuilder group)
                {
                    var booking = group.MapGroup("/{id:guid}");
                    booking.MapPut("/period", (Guid id) => TypedResults.NoContent());
                    MapHistory();

                    void MapHistory() => booking.MapGroup("/history").MapGet("/", () => TypedResults.Ok(Array.Empty<BookingDto>()));
                }
            }
            """,
            """
            namespace MyApp.Features.Bookings.ImportBookings;

            internal static class ImportBookingsEndpoint
            {
                public static void Map(RouteGroupBuilder group) => group.MapPost("/import", Import<BookingDto>);

                private static Task<Results<Created<T>, Conflict>> Import<T>(T booking) =>
                    Task.FromResult<Results<Created<T>, Conflict>>(TypedResults.Created("/bookings", booking));
            }
            """);
    }

    [Fact]
    public async Task Reports_an_endpoint_of_a_slice_mapped_on_the_application()
    {
        var diagnostic = (await VerifyAsync(
            """
            namespace MyApp.Features.Bookings;

            public static class BookingsModule
            {
                public static WebApplication MapBookingsModule(this WebApplication app)
                {
                    MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingEndpoint.Map(app);
                    return app;
                }
            }
            """,
            """
            namespace MyApp.Features.Bookings.ConfirmBooking;

            internal static class ConfirmBookingEndpoint
            {
                public static void Map(WebApplication app) =>
                    [|app|].MapPost("/bookings", (BookingDto booking) => TypedResults.Created($"/bookings/{booking.Id}"));
            }
            """)).ShouldHaveSingleItem();

        diagnostic.GetMessage().ShouldBe(
            "endpoint 'POST /bookings' in slice 'Bookings/ConfirmBooking' is mapped on 'WebApplication', not on a group. "
            + "A slice endpoint is mapped on the RouteGroupBuilder of its module, which owns the route prefix and the policies of its endpoints. "
            + "Fix: take the 'RouteGroupBuilder group' that 'BookingsModule' creates with MapGroup as a parameter, and map the endpoint on it. "
            + "Doc: 'docs/rules/HS015.md'.");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task Reports_an_IEndpointRouteBuilder_receiver_and_a_group_typed_as_one()
    {
        var diagnostics = await VerifyAsync(
            """
            namespace MyApp.Features.Bookings;

            public static class BookingsModule
            {
                public static IEndpointRouteBuilder MapBookingsModule(this IEndpointRouteBuilder endpoints)
                {
                    MyApp.Features.Bookings.ListBookings.ListBookingsEndpoint.Map(endpoints);
                    return endpoints;
                }
            }
            """,
            """
            namespace MyApp.Features.Bookings.ListBookings;

            internal static class ListBookingsEndpoint
            {
                public static void Map(IEndpointRouteBuilder endpoints)
                {
                    [|endpoints|].MapGet("/bookings", () => TypedResults.Ok(Array.Empty<BookingDto>()));
                    IEndpointRouteBuilder group = endpoints.MapGroup("/archived");
                    [|group|].MapGet("/", () => TypedResults.Ok(Array.Empty<BookingDto>()));
                }
            }
            """);

        diagnostics.Select(diagnostic => diagnostic.GetMessage().Split(". ")[0]).ShouldBe(
        [
            "endpoint 'GET /bookings' in slice 'Bookings/ListBookings' is mapped on 'IEndpointRouteBuilder', not on a group",
            "endpoint 'GET /archived' in slice 'Bookings/ListBookings' is mapped on 'IEndpointRouteBuilder', not on a group"
        ]);
    }

    [Fact]
    public async Task Reports_an_endpoint_mapped_in_the_module_or_at_the_root_of_Features()
    {
        var diagnostics = await VerifyAsync(
            """
            namespace MyApp.Features.Bookings;

            public static class BookingsModule
            {
                public static IEndpointRouteBuilder MapBookingsModule(this IEndpointRouteBuilder endpoints)
                {
                    var group = endpoints.MapGroup("/bookings");
                    group.[|MapGet|]("/{id:guid}", (Guid id) => TypedResults.Ok(new BookingDto(id)));
                    return endpoints;
                }
            }
            """,
            """
            namespace MyApp.Features;

            internal static class FeatureEndpoints
            {
                public static void Map(RouteGroupBuilder group) => group.[|MapGet|]("/version", () => TypedResults.Ok("1.0"));
            }
            """);

        diagnostics.Select(diagnostic => diagnostic.GetMessage()).ShouldBe(
        [
            "endpoint 'GET /bookings/{id:guid}' in 'BookingsModule' is mapped outside a slice. "
                + "An endpoint belongs to the slice of its use case: its module only maps the endpoints of its slices on its group. "
                + "Fix: move the mapping into the 'Map(RouteGroupBuilder group)' method of an internal endpoint class in 'Features/Bookings/<Slice>/', "
                + "and call that method from 'BookingsModule' with the group of the module. Doc: 'docs/rules/HS015.md'.",
            "endpoint 'GET /version' in 'FeatureEndpoints' is mapped outside a slice. "
                + "An endpoint belongs to the slice of its use case: its module only maps the endpoints of its slices on its group. "
                + "Fix: move the mapping into the 'Map(RouteGroupBuilder group)' method of an internal endpoint class in 'Features/<Module>/<Slice>/', "
                + "and call that method from '<Module>Module' with the group of the module. Doc: 'docs/rules/HS015.md'."
        ]);
    }

    [Fact]
    public async Task Reports_an_endpoint_that_returns_IResult_from_Results()
    {
        var diagnostic = (await VerifyAsync(
            BookingsModule("MyApp.Features.Bookings.GetBooking.GetBookingEndpoint"),
            """
            namespace MyApp.Features.Bookings.GetBooking;

            internal static class GetBookingEndpoint
            {
                public static void Map(RouteGroupBuilder group) =>
                    group.MapGet("/{id:guid}", (Guid id) [|=>|] id == Guid.Empty ? Results.NotFound() : Results.Ok(new BookingDto(id)));
            }
            """)).ShouldHaveSingleItem();

        diagnostic.GetMessage().ShouldBe(
            "endpoint 'GET /{id:guid}' in slice 'Bookings/GetBooking' returns 'IResult'. "
            + "A slice endpoint returns TypedResults, so its responses are part of its signature. "
            + "Fix: build every response with TypedResults instead of Results, and declare them all as the return type of the endpoint, "
            + "such as 'Task<Results<Ok<T>, NotFound>>'. Doc: 'docs/rules/HS015.md'.");
    }

    [Fact]
    public async Task Reports_endpoints_that_return_a_DTO_nothing_or_a_custom_result()
    {
        var diagnostics = await VerifyAsync(
            BookingsModule("MyApp.Features.Bookings.ExportBookings.ExportBookingsEndpoint"),
            """
            namespace MyApp.Features.Bookings.ExportBookings;

            internal sealed class CsvResult : IResult
            {
                public Task ExecuteAsync(HttpContext httpContext) => Task.CompletedTask;
            }

            internal static class ExportBookingsEndpoint
            {
                public static void Map(RouteGroupBuilder group)
                {
                    group.MapGet("/{id:guid}", async [|Task<BookingDto>|] (Guid id) => await Task.FromResult(new BookingDto(id)));
                    group.MapGet("/latest", () [|=>|] new BookingDto(Guid.Empty));
                    group.MapPost("/purge", () [|=>|] { });
                    group.MapPost("/touch", async context [|=>|] await context.Response.WriteAsync("touched"));
                    group.MapGet("/csv", [|Csv|]);
                    group.MapGet("/object", [|object|] () => TypedResults.Ok());
                }

                private static CsvResult Csv() => new();
            }
            """);

        diagnostics.Select(diagnostic => diagnostic.GetMessage().Split(". A slice endpoint")[0]).ShouldBe(
        [
            "endpoint 'GET /{id:guid}' in slice 'Bookings/ExportBookings' returns 'BookingDto'",
            "endpoint 'GET /latest' in slice 'Bookings/ExportBookings' returns 'BookingDto'",
            "endpoint 'POST /purge' in slice 'Bookings/ExportBookings' returns 'void'",
            "endpoint 'POST /touch' in slice 'Bookings/ExportBookings' returns 'Task'",
            "endpoint 'GET /csv' in slice 'Bookings/ExportBookings' returns 'CsvResult'",
            "endpoint 'GET /object' in slice 'Bookings/ExportBookings' returns 'object'"
        ]);
        diagnostics[0].GetMessage().ShouldContain(
            "Fix: return 'TypedResults.Ok(...)', typed 'Ok<BookingDto>', or declare 'Results<Ok<BookingDto>, NotFound>' as the return type "
            + "of the endpoint when it has several responses.");
        diagnostics[2].GetMessage().ShouldContain(
            "Fix: return the response built with TypedResults, such as 'TypedResults.NoContent()', and declare its type, "
            + "such as 'Task<NoContent>', as the return type of the endpoint.");
        diagnostics[4].GetMessage().ShouldContain("Fix: build every response with TypedResults instead of Results");
    }

    [Fact]
    public async Task Reports_a_generic_method_group_and_a_typed_delegate_but_not_an_untyped_one()
    {
        var diagnostics = await VerifyAsync(
            BookingsModule("MyApp.Features.Bookings.EchoBooking.EchoBookingEndpoint"),
            """
            namespace MyApp.Features.Bookings.EchoBooking;

            internal static class EchoBookingEndpoint
            {
                public static void Map(RouteGroupBuilder group)
                {
                    group.MapPost("/echo", [|Echo<BookingDto>|]);
                    Func<string> name = () => "echo";
                    group.MapGet("/name", [|name|]);
                    Delegate stored = () => "stored";
                    group.MapGet("/stored", stored);
                }

                private static Task<T> Echo<T>(T booking) => Task.FromResult(booking);
            }
            """);

        diagnostics.Select(diagnostic => diagnostic.GetMessage().Split(". A slice endpoint")[0]).ShouldBe(
        [
            "endpoint 'POST /echo' in slice 'Bookings/EchoBooking' returns 'BookingDto'",
            "endpoint 'GET /name' in slice 'Bookings/EchoBooking' returns 'string'"
        ]);
    }

    [Fact]
    public async Task Reports_an_endpoint_whose_method_the_module_never_calls()
    {
        var diagnostic = (await VerifyAsync(
            BookingsModule("MyApp.Features.Bookings.GetBooking.GetBookingEndpoint"),
            """
            namespace MyApp.Features.Bookings.GetBooking;

            internal static class GetBookingEndpoint
            {
                public static void Map(RouteGroupBuilder group) => group.MapGet("/{id:guid}", (Guid id) => TypedResults.Ok(new BookingDto(id)));
            }
            """,
            """
            namespace MyApp.Features.Bookings.ArchiveBooking;

            internal static class ArchiveBookingEndpoint
            {
                public static void Map(RouteGroupBuilder group) => group.[|MapPost|]("/{id:guid}/archive", (Guid id) => TypedResults.NoContent());
            }
            """)).ShouldHaveSingleItem();

        diagnostic.GetMessage().ShouldBe(
            "endpoint 'POST /{id:guid}/archive' in slice 'Bookings/ArchiveBooking' is mapped by 'ArchiveBookingEndpoint.Map', "
            + "which 'BookingsModule' never calls. "
            + "A slice endpoint is exposed only through its module, which maps every endpoint of its slices. "
            + "Fix: call 'ArchiveBookingEndpoint.Map' with the group of the module from the mapping method of 'BookingsModule'. "
            + "Doc: 'docs/rules/HS015.md'.");
        diagnostic.Descriptor.CustomTags.ShouldContain(WellKnownDiagnosticTags.CompilationEnd);
    }

    // Only the type '<Module>Module' of its own module registers a slice: another module, a helper of the module or a
    // module without that type does not.
    [Fact]
    public async Task Reports_endpoints_called_by_another_module_a_helper_or_in_a_module_without_its_type()
    {
        var diagnostics = await VerifyAsync(
            """
            namespace MyApp.Features.Rooms;

            public static class RoomsModule
            {
                public static void MapRoomsModule(this IEndpointRouteBuilder endpoints) =>
                    MyApp.Features.Bookings.ListBookings.ListBookingsEndpoint.Map(endpoints.MapGroup("/rooms"));
            }
            """,
            """
            namespace MyApp.Features.Bookings;

            public static class BookingsEndpoints
            {
                public static void MapAll(RouteGroupBuilder group) => MyApp.Features.Bookings.CountBookings.CountBookingsEndpoint.Map(group);
            }
            """,
            """
            namespace MyApp.Features.Bookings.ListBookings;

            internal static class ListBookingsEndpoint
            {
                public static void Map(RouteGroupBuilder group) => group.[|MapGet|]("/", () => TypedResults.Ok(Array.Empty<BookingDto>()));
            }
            """,
            """
            namespace MyApp.Features.Bookings.CountBookings;

            internal static class CountBookingsEndpoint
            {
                public static void Map(RouteGroupBuilder group) => group.[|MapGet|]("/count", () => TypedResults.Ok(0));
            }
            """,
            """
            namespace MyApp.Features.Invoices.ListInvoices;

            internal static class ListInvoicesEndpoint
            {
                public static void Map(RouteGroupBuilder group) => group.[|MapGet|]("/", () => TypedResults.Ok(0));
            }
            """);

        diagnostics.Select(diagnostic => diagnostic.GetMessage().Split(". A slice endpoint")[0]).ShouldBe(
        [
            "endpoint 'GET /' in slice 'Bookings/ListBookings' is mapped by 'ListBookingsEndpoint.Map', which 'BookingsModule' never calls",
            "endpoint 'GET /count' in slice 'Bookings/CountBookings' is mapped by 'CountBookingsEndpoint.Map', which 'BookingsModule' never calls",
            "endpoint 'GET /' in slice 'Invoices/ListInvoices' is mapped by 'ListInvoicesEndpoint.Map', which 'InvoicesModule' never calls"
        ]);
    }

    // Endpoints outside Features, such as those of Program, are not slice endpoints; names never declare a mapping.
    [Fact]
    public async Task Ignores_endpoints_outside_Features_and_lookalike_mappings()
    {
        await AnalyzerHarness.VerifyAsync(
            new SliceEndpointAnalyzer(),
            SliceEndpointAnalyzer.DiagnosticId,
            [],
            OutputKind.ConsoleApplication,
            """
            var app = WebApplication.Create(args);
            app.MapGet("/health", () => "healthy");
            MyApp.Diagnostics.VersionEndpoint.Map(app);
            app.Run();
            """,
            """
            namespace MyApp.Diagnostics;

            internal static class VersionEndpoint
            {
                public static void Map(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/version", () => "1.0");
            }
            """,
            """
            namespace MyApp.Features.Bookings.Lookalike;

            internal sealed class Router
            {
                public void MapGet(string pattern, Delegate handler) { }
            }

            internal static class LookalikeEndpoint
            {
                public static void Map(Router router) => router.MapGet("/", () => "lookalike");
            }
            """,
            WebUsings,
            Contracts);
    }

    [Fact]
    public async Task Does_not_apply_to_test_projects()
    {
        var compilation = Compile(
            "MyApp.Tests",
            [
                ("Endpoint.cs",
                    """
                    namespace MyApp.Tests.Features.Bookings.FakeBooking;

                    public static class FakeBookingEndpoint
                    {
                        public static void Map(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/", () => "fake");
                    }
                    """),
                ("Usings.cs", ImplicitUsings),
                ("WebUsings.cs", WebUsings)
            ]);

        var diagnostics = await compilation
            .WithAnalyzers([new SliceEndpointAnalyzer()], Options(configuration: null, isTestProject: true))
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        diagnostics.ShouldBeEmpty();
    }
}
