using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Demo.Domain.Bookings;
using Demo.Shared.Messaging;
using Demo.Shared.Persistence;

namespace Demo.Features.Bookings.GetBooking;

public sealed record GetBookingQuery(Guid BookingId) : IQuery<BookingDto?>;

public sealed record BookingDto(Guid Id, Guid RoomId, DateTimeOffset Start, DateTimeOffset End, string Status);

// Reading the bookings of a room is reading bookings too: it lives next to GetBooking.
public sealed record GetRoomBookingsQuery(Guid RoomId) : IQuery<IReadOnlyList<RoomBookingDto>>;

public sealed record RoomBookingDto(Guid Id, DateTimeOffset Start, DateTimeOffset End, string Status);

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

// The bookings of a room in the order of their periods, read as a projection without tracking.
internal sealed class GetRoomBookingsHandler(AppDbContext db) : IRequestHandler<GetRoomBookingsQuery, IReadOnlyList<RoomBookingDto>>
{
    public async Task<IReadOnlyList<RoomBookingDto>> Handle(GetRoomBookingsQuery query, CancellationToken cancellationToken) =>
        await db.Set<Booking>()
            .AsNoTracking()
            .Where(booking => booking.RoomId == query.RoomId)
            .OrderBy(booking => booking.Period.Start)
            .Select(booking => new RoomBookingDto(booking.Id.Value, booking.Period.Start, booking.Period.End, booking.Status.ToString()))
            .ToListAsync(cancellationToken);
}

internal static class GetBookingEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", async Task<Results<Ok<BookingDto>, NotFound>> (Guid id, IMediator mediator, CancellationToken cancellationToken) =>
            await mediator.Send(new GetBookingQuery(id), cancellationToken) is { } booking ? TypedResults.Ok(booking) : TypedResults.NotFound());
        group.MapGet("/rooms/{roomId:guid}", async Task<Ok<IReadOnlyList<RoomBookingDto>>> (Guid roomId, IMediator mediator, CancellationToken cancellationToken) =>
            TypedResults.Ok(await mediator.Send(new GetRoomBookingsQuery(roomId), cancellationToken)));
    }
}
