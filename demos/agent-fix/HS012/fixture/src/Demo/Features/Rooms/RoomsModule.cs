using Demo.Features.Bookings.GetBooking;

namespace Demo.Features.Rooms;

// The public contract of the Rooms module.
public static class RoomsModule
{
    // A room is free over a period when none of its confirmed bookings overlaps it.
    public static bool IsFree(IEnumerable<BookingDto> roomBookings, DateTimeOffset start, DateTimeOffset end) =>
        !roomBookings.Any(booking => booking.Status == "Confirmed" && booking.Start < end && start < booking.End);
}
