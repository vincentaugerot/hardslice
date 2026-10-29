using Microsoft.EntityFrameworkCore;
using Demo.Domain;
using Demo.Domain.Bookings;
using Demo.Features.Bookings.ConfirmBooking;

namespace Demo.IntegrationTests.Features.Bookings.ConfirmBooking;

public sealed class ConfirmBookingTests(PostgresDatabase database)
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    private readonly TimeProvider _beforeStart = new FixedClock(Start.AddDays(-1));

    [Fact]
    public async Task Stores_a_confirmed_booking_and_returns_its_identifier()
    {
        var command = new ConfirmBookingCommand(Guid.NewGuid(), Start, Start.AddHours(1));
        await using var db = database.CreateDbContext();

        var id = await new ConfirmBookingHandler(db, _beforeStart).Handle(command, TestContext.Current.CancellationToken);

        await using var readDb = database.CreateDbContext();
        var booking = await readDb.Set<Booking>().SingleOrDefaultAsync(booking => booking.Id == id, TestContext.Current.CancellationToken);
        booking.ShouldNotBeNull();
        booking.RoomId.ShouldBe(command.RoomId);
        booking.Period.ShouldBe(new BookingPeriod(command.Start, command.End));
        booking.Status.ShouldBe(BookingStatus.Confirmed);
    }

    [Fact]
    public async Task Rejects_a_period_that_does_not_end_after_its_start()
    {
        var command = new ConfirmBookingCommand(Guid.NewGuid(), Start, Start);
        await using var db = database.CreateDbContext();

        await Should.ThrowAsync<DomainException>(
            () => new ConfirmBookingHandler(db, _beforeStart).Handle(command, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Rejects_a_period_that_has_already_started()
    {
        var command = new ConfirmBookingCommand(Guid.NewGuid(), Start, Start.AddHours(1));
        await using var db = database.CreateDbContext();

        await Should.ThrowAsync<DomainException>(
            () => new ConfirmBookingHandler(db, new FixedClock(Start)).Handle(command, TestContext.Current.CancellationToken));
    }
}
