using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using MyApp.Domain.Bookings;
using MyApp.Features.Bookings.ConfirmBooking;

namespace MyApp.IntegrationTests.Features.Bookings.ConfirmBooking;

public sealed class ConfirmBookingTests(PostgresDatabase database)
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Stores_a_confirmed_booking_in_PostgreSQL()
    {
        await using var app = new MyAppFactory(database);
        var command = new ConfirmBookingCommand(Guid.NewGuid(), Start, Start.AddHours(1));

        var response = await app.CreateClient().PostAsJsonAsync("/bookings", command, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = new BookingId(Guid.Parse(response.Headers.Location!.OriginalString["/bookings/".Length..]));
        await using var db = database.CreateDbContext();
        var booking = await db.Set<Booking>().SingleAsync(booking => booking.Id == id, TestContext.Current.CancellationToken);
        booking.RoomId.ShouldBe(command.RoomId);
        booking.Period.ShouldBe(new BookingPeriod(command.Start, command.End));
        booking.Status.ShouldBe(BookingStatus.Confirmed);
    }

    [Fact]
    public async Task Rejects_a_period_that_does_not_end_after_its_start()
    {
        await using var app = new MyAppFactory(database);
        var command = new ConfirmBookingCommand(Guid.NewGuid(), Start, Start);

        var response = await app.CreateClient().PostAsJsonAsync("/bookings", command, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await using var db = database.CreateDbContext();
        (await db.Set<Booking>().AnyAsync(booking => booking.RoomId == command.RoomId, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }
}
