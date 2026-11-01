using Demo.Domain.Bookings;

namespace Demo.Features.Rooms;

// The public contract of the Rooms module.
public static class RoomsModule
{
    // A room is free over a period when none of its confirmed bookings overlaps it.
    public static bool IsFree(IEnumerable<Booking> roomBookings, BookingPeriod period) =>
        !roomBookings.Any(booking => booking.Status == BookingStatus.Confirmed && booking.Period.Overlaps(period));
}
