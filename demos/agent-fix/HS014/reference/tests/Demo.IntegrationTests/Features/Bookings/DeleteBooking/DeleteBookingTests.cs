using System.Net;
using Microsoft.EntityFrameworkCore;
using Demo.Domain.Bookings;

namespace Demo.IntegrationTests.Features.Bookings.DeleteBooking;

public sealed class DeleteBookingTests(PostgresDatabase database)
{
    private static readonly BookingPeriod Period = new(
        new DateTimeOffset(2099, 11, 2, 9, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2099, 11, 2, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Deletes_a_cancelled_booking()
    {
        var booking = await StoreAsync(cancelled: true);
        await using var app = new AppFactory(database);

        var response = await app.CreateClient().DeleteAsync($"/bookings/{booking.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ExistsAsync(booking.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task Rejects_a_confirmed_booking_and_keeps_it()
    {
        var booking = await StoreAsync(cancelled: false);
        await using var app = new AppFactory(database);

        var response = await app.CreateClient().DeleteAsync($"/bookings/{booking.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ExistsAsync(booking.Id)).ShouldBeTrue();
    }

    [Fact]
    public async Task Returns_404_for_an_unknown_booking()
    {
        await using var app = new AppFactory(database);

        var response = await app.CreateClient().DeleteAsync($"/bookings/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Rejects_an_empty_booking_id_with_a_validation_problem()
    {
        await using var app = new AppFactory(database);

        var response = await app.CreateClient().DeleteAsync($"/bookings/{Guid.Empty}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private async Task<Booking> StoreAsync(bool cancelled)
    {
        var booking = Booking.Confirm(Guid.NewGuid(), Period, []);
        if (cancelled)
            booking.Cancel();
        await using var db = database.CreateDbContext();
        db.Set<Booking>().Add(booking);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return booking;
    }

    private async Task<bool> ExistsAsync(BookingId id)
    {
        await using var db = database.CreateDbContext();
        return await db.Set<Booking>().AnyAsync(booking => booking.Id == id, TestContext.Current.CancellationToken);
    }
}
