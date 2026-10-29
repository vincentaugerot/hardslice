using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MyApp.Domain.Bookings;
using MyApp.Shared.Messaging;
using MyApp.Shared.Persistence;

namespace MyApp.Features.Bookings.GetBooking;

public sealed record GetBookingQuery(Guid BookingId) : IQuery<BookingDto?>;

public sealed record BookingDto(Guid Id, Guid RoomId, DateTimeOffset Start, DateTimeOffset End, string Status);

// Reads a projection without tracking: a query never loads the aggregate, never saves, and runs outside any transaction.
internal sealed class GetBookingHandler(AppDbContext db) : IRequestHandler<GetBookingQuery, BookingDto?>
{
    public Task<BookingDto?> Handle(GetBookingQuery query, CancellationToken cancellationToken)
    {
        var id = new BookingId(query.BookingId);
        return db.Set<Booking>()
            .AsNoTracking()
            .Where(booking => booking.Id == id)
            .Select(booking => new BookingDto(
                booking.Id.Value, booking.RoomId, booking.Period.Start, booking.Period.End, booking.Status.ToString()))
            .SingleOrDefaultAsync(cancellationToken);
    }
}

internal static class GetBookingEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/{id:guid}", async Task<Results<Ok<BookingDto>, NotFound>> (Guid id, IMediator mediator, CancellationToken cancellationToken) =>
            await mediator.Send(new GetBookingQuery(id), cancellationToken) is { } booking ? TypedResults.Ok(booking) : TypedResults.NotFound());
}
