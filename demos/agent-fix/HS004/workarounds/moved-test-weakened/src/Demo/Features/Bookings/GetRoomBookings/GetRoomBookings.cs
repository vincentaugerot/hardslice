using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Demo.Domain.Bookings;
using Demo.Shared.Messaging;
using Demo.Shared.Persistence;

namespace Demo.Features.Bookings.GetRoomBookings;

public sealed record GetRoomBookingsQuery(Guid RoomId) : IQuery<IReadOnlyList<RoomBookingDto>>;

public sealed record RoomBookingDto(Guid Id, DateTimeOffset Start, DateTimeOffset End, string Status);

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

internal static class GetRoomBookingsEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/rooms/{roomId:guid}", async Task<Ok<IReadOnlyList<RoomBookingDto>>> (Guid roomId, IMediator mediator, CancellationToken cancellationToken) =>
            TypedResults.Ok(await mediator.Send(new GetRoomBookingsQuery(roomId), cancellationToken)));
}
