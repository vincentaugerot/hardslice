using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MyApp.Domain.Bookings;
using MyApp.Features.Bookings.ConfirmBooking;

namespace MyApp.IntegrationTests.Features.Bookings.CancelBooking;

public sealed class CancelBookingTests(PostgresDatabase database)
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Cancels_a_booking_confirmed_by_another_instance_of_the_application()
    {
        await using var confirmingApp = new MyAppFactory(database);
        await using var cancellingApp = new MyAppFactory(database);
        var id = await ConfirmAsync(confirmingApp.CreateClient());

        var response = await cancellingApp.CreateClient().PostAsync($"/bookings/{id}/cancel", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await StoredStatusAsync(id)).ShouldBe(BookingStatus.Cancelled);
    }

    [Fact]
    public async Task Rejects_a_second_cancellation_and_keeps_the_booking_cancelled()
    {
        await using var app = new MyAppFactory(database);
        var client = app.CreateClient();
        var id = await ConfirmAsync(client);
        (await client.PostAsync($"/bookings/{id}/cancel", null, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();

        var response = await client.PostAsync($"/bookings/{id}/cancel", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await StoredStatusAsync(id)).ShouldBe(BookingStatus.Cancelled);
    }

    [Fact]
    public async Task Returns_404_for_an_unknown_booking()
    {
        await using var app = new MyAppFactory(database);

        var response = await app.CreateClient().PostAsync($"/bookings/{Guid.NewGuid()}/cancel", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Rejects_an_empty_booking_id_with_a_validation_problem()
    {
        await using var app = new MyAppFactory(database);

        var response = await app.CreateClient().PostAsync($"/bookings/{Guid.Empty}/cancel", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestContext.Current.CancellationToken);
        problem.ShouldNotBeNull();
        problem.Errors.Keys.ShouldBe(["bookingId"]);
    }

    private static async Task<BookingId> ConfirmAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/bookings", new ConfirmBookingCommand(Guid.NewGuid(), Start, Start.AddHours(1)), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return new BookingId(Guid.Parse(response.Headers.Location!.OriginalString["/bookings/".Length..]));
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
