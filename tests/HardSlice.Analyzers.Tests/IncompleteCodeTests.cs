using static HardSlice.Analyzers.Tests.AnalyzerHarness;

namespace HardSlice.Analyzers.Tests;

// The IDE runs the analyzers on every keystroke, on code that does not compile yet. An exception of an analyzer is
// reported as AD0001 and stops it for the compilation; a stack overflow kills the compiler or the analysis host.
// Each extract is added to the Bookings reference, analyzed by every analyzer in the All mode of the template.
public sealed class IncompleteCodeTests
{
    private const string Usings =
        """
        using Microsoft.AspNetCore.Http.HttpResults;
        using Microsoft.EntityFrameworkCore;
        using MyApp.Domain.Bookings;
        using MyApp.Shared.Messaging;
        using MyApp.Shared.Persistence;

        """;

    private static readonly Dictionary<string, string> Extracts = new()
    {
        // A local read in its own initializer (CS0165): HS006 followed it back to itself until the stack overflowed.
        ["self-referencing local"] =
            """
            namespace MyApp.Features.Bookings.CountBookings;

            public sealed record CountBookingsQuery : IQuery<int>;

            internal sealed class CountBookingsHandler(AppDbContext db) : IRequestHandler<CountBookingsQuery, int>
            {
                public Task<int> Handle(CountBookingsQuery request, CancellationToken cancellationToken)
                {
                    IQueryable<Booking> query = query.Where(booking => booking.RoomId != Guid.Empty);
                    return Task.FromResult(query.ToList().Count);
                }
            }
            """,
        // Two locals initialized from each other (CS0841).
        ["locals initialized from each other"] =
            """
            namespace MyApp.Features.Bookings.CountBookings;

            public sealed record CountBookingsQuery : IQuery<int>;

            internal sealed class CountBookingsHandler(AppDbContext db) : IRequestHandler<CountBookingsQuery, int>
            {
                public Task<int> Handle(CountBookingsQuery request, CancellationToken cancellationToken)
                {
                    var first = second.Where(booking => booking.RoomId != Guid.Empty);
                    var second = first.Where(booking => booking.RoomId != Guid.Empty);
                    return Task.FromResult(first.Count() + db.Set<Booking>().AsNoTracking().Count());
                }
            }
            """,
        ["unfinished generic"] =
            """
            namespace MyApp.Features.Bookings.CountBookings;

            public sealed record CountBookingsQuery : IQuery<

            internal sealed class CountBookingsHandler : IRequestHandler<CountBookingsQuery,
            {
            }
            """,
        ["truncated record"] =
            """
            namespace MyApp.Features.Bookings.ArchiveBooking;

            public sealed record ArchiveBookingCommand(Guid BookingId, Guid
            """,
        ["unfinished lambda"] =
            """
            namespace MyApp.Features.Bookings.ArchiveBooking;

            internal static class ArchiveBookingEndpoint
            {
                public static void Map(RouteGroupBuilder group) =>
                    group.MapPost("/{id:guid}/archive", async (Guid id, IMediator mediator) =>
            }
            """,
        ["MapGet without arguments"] =
            """
            namespace MyApp.Features.Bookings.ArchiveBooking;

            internal static class ArchiveBookingEndpoint
            {
                public static void Map(RouteGroupBuilder group) => group.MapGet();
            }
            """,
        ["partial async method"] =
            """
            namespace MyApp.Features.Bookings.ArchiveBooking;

            public sealed record ArchiveBookingCommand(Guid BookingId) : ICommand<bool>;

            internal sealed partial class ArchiveBookingHandler(AppDbContext db) : IRequestHandler<ArchiveBookingCommand, bool>
            {
                public partial async Task<bool> Handle(ArchiveBookingCommand command, CancellationToken cancellationToken);

                public async partial Task<bool> Handle(ArchiveBookingCommand command, CancellationToken cancellationToken)
                {
                    var booking = await db.Set<Booking>().FindAsync([command.BookingId], cancellationToken);
                    return booking.Result is not null;
                }
            }
            """,
        ["unknown types"] =
            """
            namespace MyApp.Features.Bookings.ArchiveBooking;

            public sealed record ArchiveBookingCommand(Guid BookingId) : ICommand<Archive>;

            internal sealed class ArchiveBookingHandler(AppDbContext db) : IRequestHandler<ArchiveBookingCommand, Archive>
            {
                public async Task<Archive> Handle(ArchiveBookingCommand command, CancellationToken cancellationToken) =>
                    await db.Set<Archive>().Where(archive => archive.Missing).SingleAsync(cancellationToken);
            }

            internal static class ArchiveBookingEndpoint
            {
                public static void Map(RouteGroupBuilder group) =>
                    group.MapPost("/{id:guid}/archive", async Task<Ok<Archive>> (Guid id, IMediator mediator) => TypedResults.Ok(await mediator.Send(new ArchiveBookingCommand(id))));
            }
            """,
        ["recursive constraint"] =
            """
            namespace MyApp.Features.Bookings.ArchiveBooking;

            public abstract class Node<T> where T : Node<T>, IQuery<T>;

            public sealed record ArchiveBookingQuery : Node<ArchiveBookingQuery>, IQuery<ArchiveBookingQuery>;
            """
    };

    public static readonly TheoryData<string> Names = [.. Extracts.Keys];

    [Theory]
    [MemberData(nameof(Names))]
    public async Task No_analyzer_throws_on_code_being_typed(string name)
    {
        var compilation = BookingsReferenceTests.CompileApplicationWithErrors(("Features/Bookings/Typing/Typing.cs", Usings + Extracts[name]));
        compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .ShouldContain(diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error, $"The extract '{name}' compiles: it tests nothing.");

        var (diagnostics, exceptions) = await AnalyzeCollectingExceptionsAsync(compilation, BookingsReferenceTests.Configuration, BookingsReferenceTests.AllMode);

        exceptions.Select(exception => exception.ToString()).ShouldBeEmpty();
        diagnostics.Where(diagnostic => diagnostic.Id.StartsWith("AD", StringComparison.Ordinal)).Select(diagnostic => diagnostic.ToString()).ShouldBeEmpty();
    }
}
