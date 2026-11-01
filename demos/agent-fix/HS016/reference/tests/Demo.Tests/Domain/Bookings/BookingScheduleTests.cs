using Demo.Domain;
using Demo.Domain.Bookings;

namespace Demo.Tests.Domain.Bookings;

public sealed class BookingScheduleTests
{
    private static readonly BookingPeriod Period = new(
        new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 11, 2, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public void A_period_has_started_from_its_start()
    {
        Period.HasStarted(Period.Start.AddTicks(-10)).ShouldBeFalse();
        Period.HasStarted(Period.Start).ShouldBeTrue();
    }

    [Fact]
    public void Confirm_rejects_a_period_that_has_started()
    {
        Should.Throw<DomainException>(() => Booking.Confirm(Guid.NewGuid(), Period, [], Period.Start));
    }

    [Fact]
    public void Cancel_rejects_a_booking_that_has_started_and_keeps_it_confirmed()
    {
        var booking = Booking.Confirm(Guid.NewGuid(), Period, [], Period.Start.AddDays(-1));

        Should.Throw<DomainException>(() => booking.Cancel(Period.Start));

        booking.Status.ShouldBe(BookingStatus.Confirmed);
    }

    [Fact]
    public void Cancel_cancels_a_booking_before_it_starts()
    {
        var booking = Booking.Confirm(Guid.NewGuid(), Period, [], Period.Start.AddDays(-1));

        booking.Cancel(Period.Start.AddMinutes(-1));

        booking.Status.ShouldBe(BookingStatus.Cancelled);
    }
}
