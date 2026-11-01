using Demo.Features.Bookings.GetBooking;
using Demo.Features.Rooms;

namespace Demo.Tests.Features.Rooms;

public sealed class RoomsModuleTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_room_is_free_when_no_confirmed_booking_overlaps_the_period()
    {
        var bookings = new[] { Dto(Start, Start.AddHours(1), "Confirmed"), Dto(Start.AddHours(1), Start.AddHours(3), "Cancelled") };

        var free = RoomsModule.IsFree(bookings, Start.AddHours(1), Start.AddHours(2));

        free.ShouldBeTrue();
    }

    [Fact]
    public void A_room_is_taken_when_a_confirmed_booking_overlaps_the_period()
    {
        var bookings = new[] { Dto(Start.AddMinutes(90), Start.AddHours(3), "Confirmed") };

        var free = RoomsModule.IsFree(bookings, Start.AddHours(1), Start.AddHours(2));

        free.ShouldBeFalse();
    }

    private static BookingDto Dto(DateTimeOffset start, DateTimeOffset end, string status) => new(Guid.NewGuid(), Guid.NewGuid(), start, end, status);
}
