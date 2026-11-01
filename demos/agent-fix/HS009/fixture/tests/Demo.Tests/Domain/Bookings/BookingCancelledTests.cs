using Demo.Domain.Bookings;
using Demo.Domain.Bookings.Events;

namespace Demo.Tests.Domain.Bookings;

public sealed class BookingCancelledTests
{
    private static readonly Guid RoomId = Guid.NewGuid();

    private static readonly BookingPeriod Period = new(
        new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 11, 2, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Cancel_raises_BookingCancelled_for_the_booking()
    {
        var booking = Booking.Confirm(RoomId, Period, []);
        booking.DequeueDomainEvents();

        booking.Cancel();

        var cancelled = booking.DequeueDomainEvents().ShouldHaveSingleItem().ShouldBeOfType<BookingCancelled>();
        cancelled.BookingId.ShouldBe(booking.Id);
        cancelled.RoomId.ShouldBe(RoomId);
    }
}
