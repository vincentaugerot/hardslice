using MyApp.Domain;
using MyApp.Domain.Bookings;

namespace MyApp.Tests.Domain.Bookings;

public sealed class BookingTests
{
    private static readonly Guid RoomId = Guid.NewGuid();

    private static readonly BookingPeriod Period = new(
        new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 11, 2, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Confirm_creates_a_confirmed_booking_for_the_room_and_period()
    {
        var booking = Booking.Confirm(RoomId, Period);

        booking.RoomId.ShouldBe(RoomId);
        booking.Period.ShouldBe(Period);
        booking.Status.ShouldBe(BookingStatus.Confirmed);
    }

    [Fact]
    public void Confirm_gives_each_booking_its_own_identifier()
    {
        Booking.Confirm(RoomId, Period).Id.ShouldNotBe(Booking.Confirm(RoomId, Period).Id);
    }

    [Fact]
    public void Cancel_marks_a_confirmed_booking_as_cancelled()
    {
        var booking = Booking.Confirm(RoomId, Period);

        booking.Cancel();

        booking.Status.ShouldBe(BookingStatus.Cancelled);
    }

    [Fact]
    public void Cancel_rejects_a_booking_that_is_already_cancelled()
    {
        var booking = Booking.Confirm(RoomId, Period);
        booking.Cancel();

        Should.Throw<DomainException>(booking.Cancel);
    }
}
