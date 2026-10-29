using MyApp.Domain.Bookings;
using MyApp.Features.Bookings;

namespace MyApp.Features.Bookings.ConfirmBooking;

public sealed record ConfirmBookingCommand(Guid RoomId, DateTimeOffset Start, DateTimeOffset End);

internal sealed class ConfirmBookingHandler(IBookingStore store)
{
    public async Task<BookingId> Handle(ConfirmBookingCommand command, CancellationToken cancellationToken)
    {
        var booking = Booking.Confirm(command.RoomId, new BookingPeriod(command.Start, command.End));
        await store.AddAsync(booking, cancellationToken);
        return booking.Id;
    }
}

internal static class ConfirmBookingEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/", async (ConfirmBookingCommand command, ConfirmBookingHandler handler, CancellationToken cancellationToken) =>
            TypedResults.Created($"/bookings/{await handler.Handle(command, cancellationToken)}"));
}
