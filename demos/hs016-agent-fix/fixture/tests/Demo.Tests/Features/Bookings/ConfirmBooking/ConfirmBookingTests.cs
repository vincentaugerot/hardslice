using Demo.Domain;
using Demo.Domain.Bookings;
using Demo.Features.Bookings;
using Demo.Features.Bookings.ConfirmBooking;

namespace Demo.Tests.Features.Bookings.ConfirmBooking;

public sealed class ConfirmBookingTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    private readonly InMemoryBookingStore _store = new();

    private readonly TimeProvider _beforeStart = new FixedClock(Start.AddDays(-1));

    [Fact]
    public async Task Stores_a_confirmed_booking_and_returns_its_identifier()
    {
        var command = new ConfirmBookingCommand(Guid.NewGuid(), Start, Start.AddHours(1));

        var id = await new ConfirmBookingHandler(_store, _beforeStart).Handle(command, TestContext.Current.CancellationToken);

        var booking = await _store.FindAsync(id, TestContext.Current.CancellationToken);
        booking.ShouldNotBeNull();
        booking.RoomId.ShouldBe(command.RoomId);
        booking.Period.ShouldBe(new BookingPeriod(command.Start, command.End));
        booking.Status.ShouldBe(BookingStatus.Confirmed);
    }

    [Fact]
    public async Task Rejects_a_period_that_does_not_end_after_its_start()
    {
        var command = new ConfirmBookingCommand(Guid.NewGuid(), Start, Start);

        await Should.ThrowAsync<DomainException>(
            () => new ConfirmBookingHandler(_store, _beforeStart).Handle(command, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Rejects_a_period_that_has_already_started()
    {
        var command = new ConfirmBookingCommand(Guid.NewGuid(), Start, Start.AddHours(1));

        await Should.ThrowAsync<DomainException>(
            () => new ConfirmBookingHandler(_store, new FixedClock(Start)).Handle(command, TestContext.Current.CancellationToken));
    }
}
