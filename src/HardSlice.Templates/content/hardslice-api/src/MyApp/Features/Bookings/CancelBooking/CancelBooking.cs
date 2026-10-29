using Microsoft.AspNetCore.Http.HttpResults;
using MyApp.Domain.Bookings;
using MyApp.Shared.Persistence;

namespace MyApp.Features.Bookings.CancelBooking;

public sealed record CancelBookingCommand(Guid BookingId);

internal sealed class CancelBookingHandler(AppDbContext db)
{
    public async Task<bool> Handle(CancelBookingCommand command, CancellationToken cancellationToken)
    {
        var booking = await db.Set<Booking>().FindAsync([new BookingId(command.BookingId)], cancellationToken);
        if (booking is null)
            return false;

        booking.Cancel();
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

internal static class CancelBookingEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/{id:guid}/cancel", async Task<Results<NoContent, NotFound>> (Guid id, CancelBookingHandler handler, CancellationToken cancellationToken) =>
            await handler.Handle(new CancelBookingCommand(id), cancellationToken) ? TypedResults.NoContent() : TypedResults.NotFound());
}
