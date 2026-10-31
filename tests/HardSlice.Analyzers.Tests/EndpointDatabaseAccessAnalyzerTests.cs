using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using static HardSlice.Analyzers.Tests.AnalyzerHarness;

namespace HardSlice.Analyzers.Tests;

// HS003: an endpoint, a delegate given to a mapping method of ASP.NET Core, never reaches the database.
public sealed class EndpointDatabaseAccessAnalyzerTests
{
    private const string WebUsings =
        """
        global using Microsoft.AspNetCore.Builder;
        global using Microsoft.AspNetCore.Http;
        global using Microsoft.AspNetCore.Routing;
        global using Microsoft.EntityFrameworkCore;
        global using Microsoft.Extensions.DependencyInjection;
        """;

    private const string Persistence =
        """
        namespace MyApp.Shared.Persistence;

        public sealed class Booking
        {
            public Guid Id { get; set; }
        }

        public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
        {
            public DbSet<Booking> Bookings => Set<Booking>();
        }
        """;

    private const string Messaging =
        """
        namespace MyApp.Shared.Messaging;

        public interface IMediator
        {
            Task<TResponse> Send<TResponse>(object request, CancellationToken cancellationToken);
        }
        """;

    private static MetadataReference[] References => [.. PackageReferences("Microsoft.EntityFrameworkCore", "Npgsql")];

    private static Task<ImmutableArray<Diagnostic>> VerifyAsync(params string[] markedSources) =>
        AnalyzerHarness.VerifyAsync(
            new EndpointDatabaseAccessAnalyzer(),
            EndpointDatabaseAccessAnalyzer.DiagnosticId,
            References,
            OutputKind.DynamicallyLinkedLibrary,
            [.. markedSources, WebUsings, Persistence, Messaging]);

    [Fact]
    public async Task Reports_a_DbContext_parameter_of_a_lambda_endpoint_in_a_slice()
    {
        var diagnostic = (await VerifyAsync(
            """
            using MyApp.Shared.Persistence;

            namespace MyApp.Features.Bookings.ConfirmBooking;

            internal static class ConfirmBookingEndpoint
            {
                public static void Map(IEndpointRouteBuilder endpoints)
                {
                    var group = endpoints.MapGroup("/bookings");
                    group.MapPost("/", async (Booking booking, AppDbContext [|db|], CancellationToken cancellationToken) =>
                    {
                        db.Bookings.Add(booking);
                        await db.SaveChangesAsync(cancellationToken);
                        return TypedResults.Created($"/bookings/{booking.Id}");
                    });
                }
            }
            """)).ShouldHaveSingleItem();

        diagnostic.GetMessage().ShouldBe(
            "endpoint 'POST /bookings' in slice 'Bookings/ConfirmBooking' uses 'AppDbContext' (parameter 'db'). "
            + "Endpoints never reach the database. "
            + "Fix: move the data access into the handler of a command and send the command with IMediator. Skill: fix-architecture-violation. Doc: 'docs/rules/HS003.md'.");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task Accepts_endpoints_that_send_requests_with_IMediator_or_use_only_HttpContext()
    {
        await VerifyAsync(
            """
            using MyApp.Shared.Messaging;

            namespace MyApp.Features.Bookings.GetBooking;

            internal static class GetBookingEndpoint
            {
                public static void Map(RouteGroupBuilder group)
                {
                    group.MapGet("/{id:guid}", (Guid id, IMediator mediator, CancellationToken cancellationToken) =>
                        mediator.Send<string>(id, cancellationToken));
                    group.MapGet("/ping", (HttpContext context) => context.Response.WriteAsync("pong"));
                    group.MapPost("/echo", async context => await context.Response.WriteAsync(context.Request.Path));
                }
            }
            """);
    }

    [Fact]
    public async Task Reports_a_static_method_group_that_receives_a_DbContext()
    {
        var diagnostic = (await VerifyAsync(
            """
            using MyApp.Shared.Persistence;

            namespace MyApp.Features.Bookings.ListBookings;

            internal static class ListBookingsEndpoint
            {
                public static void Map(RouteGroupBuilder group) => group.MapGet("/", List);

                private static Task<List<Booking>> List(AppDbContext [|db|]) => db.Bookings.ToListAsync();
            }
            """)).ShouldHaveSingleItem();

        diagnostic.GetMessage().ShouldStartWith("endpoint 'GET /' in slice 'Bookings/ListBookings' uses 'AppDbContext' (parameter 'db').");
        diagnostic.GetMessage().ShouldContain("Fix: move the data access into the handler of a query and send the query with IMediator.");
    }

    [Fact]
    public async Task Reports_a_DbContext_field_read_by_an_instance_method_group()
    {
        var diagnostic = (await VerifyAsync(
            """
            using MyApp.Shared.Persistence;

            namespace MyApp.Features.Bookings.CancelBooking;

            internal sealed class CancelBookingEndpoint(AppDbContext context)
            {
                private readonly AppDbContext _db = context;

                public void Map(IEndpointRouteBuilder endpoints) => endpoints.MapPost("/bookings/{id:guid}/cancel", Cancel);

                private async Task<IResult> Cancel(Guid id)
                {
                    var booking = await [|_db|].Bookings.FindAsync(id);
                    _db.Remove(booking!);
                    return TypedResults.NoContent();
                }
            }
            """)).ShouldHaveSingleItem();

        diagnostic.GetMessage().ShouldStartWith(
            "endpoint 'POST /bookings/{id:guid}/cancel' in slice 'Bookings/CancelBooking' uses 'AppDbContext' (field '_db').");
    }

    [Fact]
    public async Task Reports_a_DbContext_obtained_from_the_request_services()
    {
        var diagnostic = (await VerifyAsync(
            """
            using MyApp.Shared.Persistence;

            namespace MyApp.Features.Bookings.CountBookings;

            internal static class CountBookingsEndpoint
            {
                public static void Map(IEndpointRouteBuilder endpoints) =>
                    endpoints.MapGet("/bookings/count", async context =>
                    {
                        var db = context.RequestServices.[|GetRequiredService|]<AppDbContext>();
                        await context.Response.WriteAsync((await db.Bookings.CountAsync()).ToString());
                    });
            }
            """)).ShouldHaveSingleItem();

        diagnostic.GetMessage().ShouldStartWith(
            "endpoint 'GET /bookings/count' in slice 'Bookings/CountBookings' uses 'AppDbContext' (call 'GetRequiredService').");
    }

    [Fact]
    public async Task Reports_an_injected_NpgsqlDataSource_and_a_created_connection()
    {
        var diagnostics = await VerifyAsync(
            """
            using Npgsql;

            namespace MyApp.Features.Reports.Export;

            internal static class ExportEndpoint
            {
                public static void Map(IEndpointRouteBuilder endpoints)
                {
                    endpoints.MapGet("/export", async (NpgsqlDataSource [|dataSource|]) =>
                    {
                        await using var connection = await dataSource.OpenConnectionAsync();
                        return connection.State.ToString();
                    });
                    endpoints.MapGet("/raw", () => new [|NpgsqlConnection|]("Host=localhost").State.ToString());
                }
            }
            """);

        diagnostics.Select(diagnostic => diagnostic.GetMessage()).ShouldBe(
        [
            "endpoint 'GET /export' in slice 'Reports/Export' uses 'NpgsqlDataSource' (parameter 'dataSource'). Endpoints never reach the database. "
                + "Fix: move the data access into the handler of a query and send the query with IMediator. Skill: fix-architecture-violation. Doc: 'docs/rules/HS003.md'.",
            "endpoint 'GET /raw' in slice 'Reports/Export' uses 'NpgsqlConnection' (object creation). Endpoints never reach the database. "
                + "Fix: move the data access into the handler of a query and send the query with IMediator. Skill: fix-architecture-violation. Doc: 'docs/rules/HS003.md'."
        ]);
    }

    [Fact]
    public async Task Reports_DbSet_and_IDbContextFactory_parameters_and_generic_contexts()
    {
        var diagnostics = await VerifyAsync(
            """
            using MyApp.Shared.Persistence;

            namespace MyApp.Features.Bookings.Generic;

            internal static class GenericEndpoints
            {
                public static void Map(IEndpointRouteBuilder endpoints)
                {
                    endpoints.MapGet("/sets", (DbSet<Booking> [|bookings|]) => bookings.CountAsync());
                    endpoints.MapGet("/factories", async (IDbContextFactory<AppDbContext> [|factory|]) =>
                    {
                        await using var db = await factory.CreateDbContextAsync();
                        return await db.Bookings.CountAsync();
                    });
                    endpoints.MapGet("/generic", Count<AppDbContext>);
                }

                private static Task<int> Count<TContext>(TContext [|db|]) where TContext : DbContext => db.Set<Booking>().CountAsync();
            }
            """);

        diagnostics.Select(diagnostic => diagnostic.GetMessage().Split(". ")[0]).ShouldBe(
        [
            "endpoint 'GET /sets' in slice 'Bookings/Generic' uses 'DbSet<Booking>' (parameter 'bookings')",
            "endpoint 'GET /factories' in slice 'Bookings/Generic' uses 'IDbContextFactory<AppDbContext>' (parameter 'factory')",
            "endpoint 'GET /generic' in slice 'Bookings/Generic' uses 'AppDbContext' (parameter 'db')"
        ]);
    }

    [Fact]
    public async Task Reports_a_context_derived_from_an_intermediate_base_and_one_declared_in_a_referenced_project()
    {
        var persistence = Compile(
            "MyApp.Persistence",
            [
                ("Contexts.cs",
                    """
                    using Microsoft.EntityFrameworkCore;

                    namespace MyApp.Persistence;

                    public abstract class AuditedDbContext(DbContextOptions options) : DbContext(options);

                    public sealed class ReportingDbContext(DbContextOptions<ReportingDbContext> options) : AuditedDbContext(options);
                    """),
                ("Usings.cs", ImplicitUsings)
            ],
            References);

        var diagnostics = await AnalyzerHarness.VerifyAsync(
            new EndpointDatabaseAccessAnalyzer(),
            EndpointDatabaseAccessAnalyzer.DiagnosticId,
            [.. References, Reference(persistence, asMetadata: true)],
            OutputKind.DynamicallyLinkedLibrary,
            """
            using MyApp.Persistence;

            namespace MyApp.Features.Reports.Daily;

            internal static class DailyEndpoint
            {
                public static void Map(IEndpointRouteBuilder endpoints) =>
                    endpoints.MapGet("/daily", (ReportingDbContext [|db|]) => db.Database.ProviderName);
            }
            """,
            WebUsings);

        diagnostics.ShouldHaveSingleItem().GetMessage().ShouldStartWith(
            "endpoint 'GET /daily' in slice 'Reports/Daily' uses 'ReportingDbContext' (parameter 'db').");
    }

    [Fact]
    public async Task Reports_accesses_in_nested_lambdas_local_functions_and_captured_parameters()
    {
        var diagnostics = await VerifyAsync(
            """
            using MyApp.Shared.Persistence;

            namespace MyApp.Features.Bookings.Nested;

            internal static class NestedEndpoints
            {
                public static void Map(IEndpointRouteBuilder endpoints, AppDbContext shared)
                {
                    endpoints.MapGet("/nested", (IServiceProvider services) =>
                    {
                        Func<AppDbContext, int> count = ([|db|]) => db.Bookings.Count();
                        return Lookup();

                        int Lookup() => services.[|GetRequiredService|]<AppDbContext>().Bookings.Count();
                    });
                    endpoints.MapGet("/local", (IServiceProvider services) =>
                    {
                        return Count(null!);

                        static int Count(AppDbContext [|db|]) => db.Bookings.Count();
                    });
                    endpoints.MapGet("/captured", () => [|shared|].Bookings.Count());
                }
            }
            """);

        diagnostics.Select(diagnostic => diagnostic.GetMessage().Split(". ")[0]).ShouldBe(
        [
            "endpoint 'GET /nested' in slice 'Bookings/Nested' uses 'AppDbContext' (parameter 'db')",
            "endpoint 'GET /nested' in slice 'Bookings/Nested' uses 'AppDbContext' (call 'GetRequiredService')",
            "endpoint 'GET /local' in slice 'Bookings/Nested' uses 'AppDbContext' (parameter 'db')",
            "endpoint 'GET /captured' in slice 'Bookings/Nested' uses 'AppDbContext' (parameter 'shared')"
        ]);
    }

    // One diagnostic per endpoint and offending symbol: the members reached through it are part of the same access.
    [Fact]
    public async Task Reports_each_offending_symbol_once_per_endpoint()
    {
        await VerifyAsync(
            """
            using MyApp.Shared.Persistence;

            namespace MyApp.Features.Bookings.Repeat;

            internal static class RepeatEndpoints
            {
                public static void Map(IEndpointRouteBuilder endpoints)
                {
                    endpoints.MapGet("/one", Handle);
                    endpoints.MapGet("/two", Handle);
                }

                private static async Task<int> Handle(AppDbContext [|[|db|]|], IServiceProvider services)
                {
                    var connection = db.Database.GetDbConnection();
                    var again = services.[|[|GetRequiredService|]|]<AppDbContext>();
                    var other = ([|[|AppDbContext|]|])services.GetService(typeof(AppDbContext))!;
                    await db.SaveChangesAsync();
                    return db.Bookings.Count() + again.Bookings.Count() + other.Bookings.Count() + connection.ConnectionTimeout;
                }
            }
            """);
    }

    [Fact]
    public async Task Reports_MapMethods_Map_and_MapFallback_endpoints_and_those_of_Program()
    {
        var diagnostics = await AnalyzerHarness.VerifyAsync(
            new EndpointDatabaseAccessAnalyzer(),
            EndpointDatabaseAccessAnalyzer.DiagnosticId,
            References,
            OutputKind.ConsoleApplication,
            """
            using MyApp.Shared.Persistence;

            var app = WebApplication.Create(args);
            var api = app.MapGroup("/api").MapGroup("v1");
            api.MapMethods("/bookings", ["GET", "HEAD"], (AppDbContext [|db|]) => db.Bookings.Count());
            app.Map("/health", (AppDbContext [|db|]) => db.Database.CanConnectAsync());
            app.MapFallback(async context => await context.RequestServices.[|GetRequiredService|]<AppDbContext>().Bookings.CountAsync());
            app.Run();
            """,
            WebUsings,
            Persistence);

        diagnostics.Select(diagnostic => diagnostic.GetMessage().Split(". Fix: ")[0]).ShouldBe(
        [
            "endpoint 'GET,HEAD /api/v1/bookings' in 'Program' uses 'AppDbContext' (parameter 'db'). Endpoints never reach the database",
            "endpoint '/health' in 'Program' uses 'AppDbContext' (parameter 'db'). Endpoints never reach the database",
            "endpoint 'fallback' in 'Program' uses 'AppDbContext' (call 'GetRequiredService'). Endpoints never reach the database"
        ]);
        diagnostics[1].GetMessage().ShouldContain("Fix: move the data access into the handler of a command or a query and send it with IMediator.");
    }

    // A method group declared in another file is reported on the mapping, where the IDE analyzes it, and points to the parameter.
    [Fact]
    public async Task Reports_a_method_group_of_another_file_on_the_mapping()
    {
        var diagnostic = (await VerifyAsync(
            """
            namespace MyApp.Features.Bookings.ListBookings;

            internal static class ListBookingsEndpoint
            {
                public static void Map(RouteGroupBuilder group) => group.MapGet("/", [|ListBookingsQueries.List|]);
            }
            """,
            """
            using MyApp.Shared.Persistence;

            namespace MyApp.Features.Bookings.ListBookings;

            internal static class ListBookingsQueries
            {
                public static Task<List<Booking>> List(AppDbContext db) => db.Bookings.ToListAsync();
            }
            """)).ShouldHaveSingleItem();

        diagnostic.AdditionalLocations.Select(location => location.SourceTree!.FilePath).ShouldBe(["File0.cs", "File1.cs"]);
    }

    // Only the mapping methods of ASP.NET Core declare an endpoint: names never suffice, and a delegate that cannot be
    // read where it is mapped is not analyzed.
    [Fact]
    public async Task Ignores_lookalike_mappings_handlers_and_delegates_from_variables()
    {
        await VerifyAsync(
            """
            using MyApp.Shared.Persistence;

            namespace MyApp.Features.Bookings.Lookalike;

            internal sealed class Router
            {
                public void MapGet(string pattern, Delegate handler) { }
            }

            internal static class LookalikeEndpoint
            {
                public static void Map(IEndpointRouteBuilder endpoints, Router router, Delegate handler)
                {
                    router.MapGet("/", (AppDbContext db) => db.Bookings.Count());
                    endpoints.MapGet("/variable", handler);
                    Delegate stored = (AppDbContext db) => db.Bookings.Count();
                    endpoints.MapGet("/stored", stored);
                    endpoints.MapGet("/count", (IServiceProvider services) => Count(services));
                    endpoints.MapGet("/name", () => nameof(AppDbContext));
                }

                // Methods the endpoint calls are not followed.
                private static int Count(IServiceProvider services) => services.GetRequiredService<AppDbContext>().Bookings.Count();
            }

            internal sealed class ListBookingsHandler(AppDbContext db)
            {
                public Task<int> Handle() => db.Bookings.CountAsync();
            }
            """);
    }

    [Fact]
    public async Task Does_not_apply_to_test_projects()
    {
        var compilation = Compile(
            "MyApp.Tests",
            [
                ("Endpoint.cs",
                    """
                    using MyApp.Shared.Persistence;

                    namespace MyApp.Tests;

                    public static class FakeEndpoint
                    {
                        public static void Map(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/", (AppDbContext db) => db.Bookings.Count());
                    }
                    """),
                ("Usings.cs", ImplicitUsings),
                ("WebUsings.cs", WebUsings),
                ("Persistence.cs", Persistence)
            ],
            References);

        var diagnostics = await compilation
            .WithAnalyzers([new EndpointDatabaseAccessAnalyzer()], Options(configuration: null, isTestProject: true))
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        diagnostics.ShouldBeEmpty();
    }
}
