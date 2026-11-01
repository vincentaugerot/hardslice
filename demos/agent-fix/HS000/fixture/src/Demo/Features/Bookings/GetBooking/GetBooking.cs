using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http.HttpResults;
using Demo.Domain.Bookings;
using Demo.Shared.Messaging;
using Demo.Shared.Persistence;

namespace Demo.Features.Bookings.GetBooking;

public sealed record GetBookingQuery(Guid BookingId) : IQuery<BookingDto?>;

public sealed record BookingDto(Guid Id, Guid RoomId, DateTimeOffset Start, DateTimeOffset End, string Status);

// FindAsync reads the booking from the context first when it is already loaded: simpler than a projection.
[SuppressMessage("HardSlice.Architecture", "HS006")]
internal sealed class GetBookingHandler(AppDbContext db) : IRequestHandler<GetBookingQuery, BookingDto?>
{
    public async Task<BookingDto?> Handle(GetBookingQuery query, CancellationToken cancellationToken)
    {
        var booking = await db.Set<Booking>().FindAsync([new BookingId(query.BookingId)], cancellationToken);
        return booking is null
            ? null
            : new BookingDto(booking.Id.Value, booking.RoomId, booking.Period.Start, booking.Period.End, booking.Status.ToString());
    }
}

internal static class GetBookingEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/{id:guid}", async Task<Results<Ok<BookingDto>, NotFound>> (Guid id, IMediator mediator, CancellationToken cancellationToken) =>
            await mediator.Send(new GetBookingQuery(id), cancellationToken) is { } booking ? TypedResults.Ok(booking) : TypedResults.NotFound());
}
