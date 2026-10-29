using Microsoft.EntityFrameworkCore;
using Demo.Domain;
using Demo.Domain.Bookings;
using Demo.Features.Bookings.CancelBooking;

namespace Demo.IntegrationTests.Features.Bookings.CancelBooking;

public sealed class CancelBookingTests(PostgresDatabase database)
{
    private static readonly BookingPeriod Period = new(
        new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 11, 2, 10, 0, 0, TimeSpan.Zero));

    private readonly TimeProvider _beforeStart = new FixedClock(Period.Start.AddDays(-1));

    [Fact]
    public async Task Cancels_an_existing_booking()
    {
        var booking = await StoreAsync(Booking.Confirm(Guid.NewGuid(), Period, []));
        await using var db = database.CreateDbContext();

        var cancelled = await new CancelBookingHandler(db, _beforeStart).Handle(
            new CancelBookingCommand(booking.Id.Value), TestContext.Current.CancellationToken);

        cancelled.ShouldBeTrue();
        (await StoredStatusAsync(booking.Id)).ShouldBe(BookingStatus.Cancelled);
    }

    [Fact]
    public async Task Reports_an_unknown_booking()
    {
        await using var db = database.CreateDbContext();

        var cancelled = await new CancelBookingHandler(db, _beforeStart).Handle(
            new CancelBookingCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        cancelled.ShouldBeFalse();
    }

    [Fact]
    public async Task Rejects_a_booking_that_has_started()
    {
        var booking = await StoreAsync(Booking.Confirm(Guid.NewGuid(), Period, []));
        await using var db = database.CreateDbContext();

        await Should.ThrowAsync<DomainException>(() => new CancelBookingHandler(db, new FixedClock(Period.Start)).Handle(
            new CancelBookingCommand(booking.Id.Value), TestContext.Current.CancellationToken));

        (await StoredStatusAsync(booking.Id)).ShouldBe(BookingStatus.Confirmed);
    }

    private async Task<Booking> StoreAsync(Booking booking)
    {
        await using var db = database.CreateDbContext();
        db.Set<Booking>().Add(booking);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return booking;
    }

    private async Task<BookingStatus> StoredStatusAsync(BookingId id)
    {
        await using var db = database.CreateDbContext();
        return await db.Set<Booking>()
            .Where(booking => booking.Id == id)
            .Select(booking => booking.Status)
            .SingleAsync(TestContext.Current.CancellationToken);
    }
}
