using Microsoft.AspNetCore.Http.HttpResults;
using MyApp.Domain.Bookings;
using MyApp.Features.Bookings;

namespace MyApp.Features.Bookings.CancelBooking;

public sealed record CancelBookingCommand(Guid BookingId);

internal sealed class CancelBookingHandler(IBookingStore store)
{
    public async Task<bool> Handle(CancelBookingCommand command, CancellationToken cancellationToken)
    {
        var booking = await store.FindAsync(new BookingId(command.BookingId), cancellationToken);
        if (booking is null)
            return false;

        booking.Cancel();
        await store.UpdateAsync(booking, cancellationToken);
        return true;
    }
}

internal static class CancelBookingEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/{id:guid}/cancel", async Task<Results<NoContent, NotFound>> (Guid id, CancelBookingHandler handler, CancellationToken cancellationToken) =>
            await handler.Handle(new CancelBookingCommand(id), cancellationToken) ? TypedResults.NoContent() : TypedResults.NotFound());
}
