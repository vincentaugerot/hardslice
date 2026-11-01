using Demo.Domain.Bookings;
using Demo.Features.Rooms;

namespace Demo.Tests.Features.Rooms;

public sealed class RoomsModuleTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_room_is_free_when_no_confirmed_booking_overlaps_the_period()
    {
        var bookings = new[] { Stored(Start, Start.AddHours(1), cancelled: false), Stored(Start.AddHours(1), Start.AddHours(3), cancelled: true) };

        var free = RoomsModule.IsFree(bookings, new BookingPeriod(Start.AddHours(1), Start.AddHours(2)));

        free.ShouldBeTrue();
    }

    [Fact]
    public void A_room_is_taken_when_a_confirmed_booking_overlaps_the_period()
    {
        var bookings = new[] { Stored(Start.AddMinutes(90), Start.AddHours(3), cancelled: false) };

        var free = RoomsModule.IsFree(bookings, new BookingPeriod(Start.AddHours(1), Start.AddHours(2)));

        free.ShouldBeFalse();
    }

    private static Booking Stored(DateTimeOffset start, DateTimeOffset end, bool cancelled)
    {
        var booking = Booking.Confirm(Guid.NewGuid(), new BookingPeriod(start, end), []);
        if (cancelled)
            booking.Cancel();
        return booking;
    }
}
