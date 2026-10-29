using Microsoft.AspNetCore.Http.HttpResults;
using Demo.Domain;
using Demo.Domain.Bookings;
using Demo.Features.Bookings;
using Demo.Features.Bookings.ConfirmBooking;

namespace Demo.Features.Bookings.CancelBooking;

public sealed record CancelBookingCommand(Guid BookingId);

internal sealed class CancelBookingHandler(IBookingStore store, TimeProvider clock)
{
    public async Task<bool> Handle(CancelBookingCommand command, CancellationToken cancellationToken)
    {
        var booking = await store.FindAsync(new BookingId(command.BookingId), cancellationToken);
        if (booking is null)
            return false;

        if (BookingSchedule.HasStarted(booking.Period, clock.GetUtcNow()))
            throw new DomainException($"Booking '{booking.Id}' cannot be cancelled once it has started.");

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
