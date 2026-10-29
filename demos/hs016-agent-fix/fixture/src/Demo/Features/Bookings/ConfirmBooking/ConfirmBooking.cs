using Demo.Domain;
using Demo.Domain.Bookings;
using Demo.Features.Bookings;

namespace Demo.Features.Bookings.ConfirmBooking;

public sealed record ConfirmBookingCommand(Guid RoomId, DateTimeOffset Start, DateTimeOffset End);

internal sealed class ConfirmBookingHandler(IBookingStore store, TimeProvider clock)
{
    public async Task<BookingId> Handle(ConfirmBookingCommand command, CancellationToken cancellationToken)
    {
        var period = new BookingPeriod(command.Start, command.End);
        if (BookingSchedule.HasStarted(period, clock.GetUtcNow()))
            throw new DomainException("A booking cannot be confirmed for a period that has already started.");

        var booking = Booking.Confirm(command.RoomId, period);
        await store.AddAsync(booking, cancellationToken);
        return booking.Id;
    }
}

// A booking is frozen once its period has started: it can no longer be confirmed or cancelled.
internal static class BookingSchedule
{
    public static bool HasStarted(BookingPeriod period, DateTimeOffset now) => period.Start <= now;
}

internal static class ConfirmBookingEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/", async (ConfirmBookingCommand command, ConfirmBookingHandler handler, CancellationToken cancellationToken) =>
            TypedResults.Created($"/bookings/{await handler.Handle(command, cancellationToken)}"));
}
