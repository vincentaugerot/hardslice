using Microsoft.EntityFrameworkCore;
using MyApp.Domain.Bookings;

namespace MyApp.IntegrationTests.Features.Bookings;

public sealed class BookingConfigurationTests(PostgresDatabase database)
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    // Domain events describe what happened during one use case: they are not stored with the booking.
    [Fact]
    public async Task Reloads_a_booking_without_its_domain_events()
    {
        var booking = Booking.Confirm(Guid.NewGuid(), new BookingPeriod(Start, Start.AddHours(1)), []);
        await using (var db = database.CreateDbContext())
        {
            db.Set<Booking>().Add(booking);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var freshDb = database.CreateDbContext();
        var reloaded = await freshDb.Set<Booking>().SingleAsync(stored => stored.Id == booking.Id, TestContext.Current.CancellationToken);

        reloaded.DomainEvents.ShouldBeEmpty();
    }
}
