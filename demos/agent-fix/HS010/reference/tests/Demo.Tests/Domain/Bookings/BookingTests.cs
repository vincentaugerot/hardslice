using Demo.Domain;
using Demo.Domain.Bookings;
using Demo.Domain.Bookings.Events;

namespace Demo.Tests.Domain.Bookings;

public sealed class BookingTests
{
    private static readonly Guid RoomId = Guid.NewGuid();

    private static readonly BookingPeriod Period = new(
        new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 11, 2, 10, 0, 0, TimeSpan.Zero));

    private static readonly DateTimeOffset Now = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Confirm_creates_a_confirmed_booking_for_the_room_and_period()
    {
        var booking = Booking.Confirm(RoomId, Period, []);

        booking.RoomId.ShouldBe(RoomId);
        booking.Period.ShouldBe(Period);
        booking.Status.ShouldBe(BookingStatus.Confirmed);
    }

    [Fact]
    public void Confirm_gives_each_booking_its_own_identifier()
    {
        Booking.Confirm(RoomId, Period, []).Id.ShouldNotBe(Booking.Confirm(RoomId, Period, []).Id);
    }

    [Fact]
    public void Confirm_raises_BookingConfirmed_with_the_booking_data()
    {
        var booking = Booking.Confirm(RoomId, Period, []);

        booking.DomainEvents.ShouldHaveSingleItem().ShouldBe(new BookingConfirmed(booking.Id, RoomId, Period));
    }

    [Fact]
    public void DequeueDomainEvents_returns_the_raised_events_once_and_empties_the_list()
    {
        var booking = Booking.Confirm(RoomId, Period, []);

        booking.DequeueDomainEvents().ShouldHaveSingleItem().ShouldBe(new BookingConfirmed(booking.Id, RoomId, Period));
        booking.DomainEvents.ShouldBeEmpty();
        booking.DequeueDomainEvents().ShouldBeEmpty();
    }

    [Fact]
    public void Confirm_rejects_a_period_overlapping_a_confirmed_booking_of_the_room()
    {
        var existing = Booking.Confirm(RoomId, Period, []);
        var overlapping = new BookingPeriod(Period.Start.AddMinutes(30), Period.End.AddMinutes(30));

        Should.Throw<BookingConflictException>(() => Booking.Confirm(RoomId, overlapping, [existing]));
    }

    [Fact]
    public void Confirm_accepts_a_period_adjacent_to_a_confirmed_booking_of_the_room()
    {
        var existing = Booking.Confirm(RoomId, Period, []);
        var adjacent = new BookingPeriod(Period.End, Period.End.AddHours(1));

        Booking.Confirm(RoomId, adjacent, [existing]).Period.ShouldBe(adjacent);
    }

    [Fact]
    public void Confirm_ignores_a_cancelled_booking_of_the_room()
    {
        var cancelled = Booking.Confirm(RoomId, Period, []);
        cancelled.Cancel(Now);

        Booking.Confirm(RoomId, Period, [cancelled]).Status.ShouldBe(BookingStatus.Confirmed);
    }

    [Fact]
    public void Confirm_ignores_a_booking_of_another_room()
    {
        var otherRoom = Booking.Confirm(Guid.NewGuid(), Period, []);

        Booking.Confirm(RoomId, Period, [otherRoom]).RoomId.ShouldBe(RoomId);
    }

    [Fact]
    public void Cancel_marks_a_confirmed_booking_as_cancelled()
    {
        var booking = Booking.Confirm(RoomId, Period, []);

        booking.Cancel(Now);

        booking.Status.ShouldBe(BookingStatus.Cancelled);
    }

    [Fact]
    public void Cancel_raises_no_further_BookingConfirmed()
    {
        var booking = Booking.Confirm(RoomId, Period, []);

        booking.Cancel(Now);

        booking.DomainEvents.OfType<BookingConfirmed>().ShouldHaveSingleItem().BookingId.ShouldBe(booking.Id);
    }

    [Fact]
    public void Cancel_rejects_a_booking_that_is_already_cancelled()
    {
        var booking = Booking.Confirm(RoomId, Period, []);
        booking.Cancel(Now);

        Should.Throw<DomainException>(() => booking.Cancel(Now));
    }
}
