using MyApp.Domain;
using MyApp.Domain.Bookings;
using MyApp.Features.Bookings;
using MyApp.Features.Bookings.ConfirmBooking;

namespace MyApp.Tests.Features.Bookings.ConfirmBooking;

public sealed class ConfirmBookingTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    private readonly InMemoryBookingStore _store = new();

    [Fact]
    public async Task Stores_a_confirmed_booking_and_returns_its_identifier()
    {
        var command = new ConfirmBookingCommand(Guid.NewGuid(), Start, Start.AddHours(1));

        var id = await new ConfirmBookingHandler(_store).Handle(command, TestContext.Current.CancellationToken);

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
            () => new ConfirmBookingHandler(_store).Handle(command, TestContext.Current.CancellationToken));
    }
}
