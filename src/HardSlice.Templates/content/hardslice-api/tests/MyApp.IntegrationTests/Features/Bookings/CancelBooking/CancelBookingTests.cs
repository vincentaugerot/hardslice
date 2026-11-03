using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MyApp.Domain.Bookings;
using MyApp.Features.Bookings.ConfirmBooking;

namespace MyApp.IntegrationTests.Features.Bookings.CancelBooking;

public sealed class CancelBookingTests(PostgresDatabase database)
{
    private static readonly DateTimeOffset Start = TestInstants.Start;

    [Fact]
    public async Task Cancels_a_booking_confirmed_by_another_instance_of_the_application()
    {
        await using var confirmingApp = new AppFactory(database);
        await using var cancellingApp = new AppFactory(database);
        var id = await ConfirmAsync(confirmingApp.CreateClient());

        var response = await cancellingApp.CreateClient().PostAsync($"/bookings/{id}/cancel", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await StoredStatusAsync(id)).ShouldBe(BookingStatus.Cancelled);
    }

    [Fact]
    public async Task Rejects_a_second_cancellation_and_keeps_the_booking_cancelled()
    {
        await using var app = new AppFactory(database);
        var client = app.CreateClient();
        var id = await ConfirmAsync(client);
        (await client.PostAsync($"/bookings/{id}/cancel", null, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();

        var response = await client.PostAsync($"/bookings/{id}/cancel", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await StoredStatusAsync(id)).ShouldBe(BookingStatus.Cancelled);
    }

    // Deterministic race: a session of the test locks the row, both requests load the confirmed booking and wait to save it.
    // Once the lock is released, one cancels it; the other saves nothing, since the row changed since it loaded it, and answers 409.
    [Fact]
    public async Task Cancels_once_and_answers_409_to_a_simultaneous_cancellation()
    {
        await using var app = new AppFactory(database);
        var client = app.CreateClient();
        var id = await ConfirmAsync(client);
        await using var locking = database.CreateDbContext();
        await using var transaction = await locking.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await locking.Database.ExecuteSqlAsync($"SELECT 1 FROM \"Bookings\" WHERE \"Id\" = {id.Value} FOR UPDATE", TestContext.Current.CancellationToken);

        var cancellations = new[] { CancelAsync(client, id), CancelAsync(client, id) };
        await WaitUntilUpdatesAreBlockedAsync(2);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
        var responses = await Task.WhenAll(cancellations);

        responses.Select(response => response.StatusCode).Order().ShouldBe([HttpStatusCode.NoContent, HttpStatusCode.Conflict]);
        var conflict = responses.Single(response => response.StatusCode == HttpStatusCode.Conflict);
        conflict.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        (await StoredStatusAsync(id)).ShouldBe(BookingStatus.Cancelled);
    }

    [Fact]
    public async Task Returns_404_for_an_unknown_booking()
    {
        await using var app = new AppFactory(database);

        var response = await app.CreateClient().PostAsync($"/bookings/{Guid.NewGuid()}/cancel", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Rejects_an_empty_booking_id_with_a_validation_problem()
    {
        await using var app = new AppFactory(database);

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

    private static Task<HttpResponseMessage> CancelAsync(HttpClient client, BookingId id) =>
        client.PostAsync($"/bookings/{id.Value}/cancel", null, TestContext.Current.CancellationToken);

    // Waits until the given number of updates of a booking wait for a lock, in the database of the tests.
    private async Task WaitUntilUpdatesAreBlockedAsync(int count)
    {
        await using var db = database.CreateDbContext();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        while (await db.Database
            .SqlQuery<int>($"""SELECT count(*)::int AS "Value" FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock' AND query LIKE '%UPDATE "Bookings"%' """)
            .SingleAsync(timeout.Token) < count)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
        }
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
