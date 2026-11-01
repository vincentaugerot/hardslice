using Demo.Domain;
using Demo.Domain.Bookings;

namespace Demo.Tests.Domain.Bookings;

public sealed class WaitlistTests
{
    private static readonly Guid RoomId = Guid.NewGuid();

    private static readonly BookingPeriod Period = new(
        new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 11, 2, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Join_adds_the_bookings_in_the_order_they_join()
    {
        var waitlist = new Waitlist(RoomId);
        var first = Booking.Confirm(RoomId, Period, []);
        var second = Booking.Confirm(RoomId, Period, []);

        waitlist.Join(first);
        waitlist.Join(second);

        waitlist.Contains(first.Id).ShouldBeTrue();
        waitlist.Contains(second.Id).ShouldBeTrue();
        waitlist.Entries.Select(entry => entry.Position).ShouldBe([1, 2]);
    }

    [Fact]
    public void Join_rejects_a_booking_of_another_room()
    {
        var waitlist = new Waitlist(RoomId);
        var booking = Booking.Confirm(Guid.NewGuid(), Period, []);

        Should.Throw<DomainException>(() => waitlist.Join(booking));
    }

    [Fact]
    public void Join_rejects_a_booking_already_on_the_waitlist()
    {
        var waitlist = new Waitlist(RoomId);
        var booking = Booking.Confirm(RoomId, Period, []);
        waitlist.Join(booking);

        Should.Throw<DomainException>(() => waitlist.Join(booking));
    }
}
