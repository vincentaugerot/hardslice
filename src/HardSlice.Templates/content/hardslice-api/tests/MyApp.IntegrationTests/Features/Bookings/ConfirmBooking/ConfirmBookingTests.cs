using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
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
        (await StoredCountAsync(command.RoomId)).ShouldBe(0);
    }

    [Fact]
    public async Task Rejects_a_period_overlapping_a_confirmed_booking_of_the_room_with_409()
    {
        await using var app = new MyAppFactory(database);
        var client = app.CreateClient();
        var roomId = Guid.NewGuid();
        (await ConfirmAsync(client, roomId, Start, Start.AddHours(1))).EnsureSuccessStatusCode();

        var response = await ConfirmAsync(client, roomId, Start.AddMinutes(30), Start.AddMinutes(90));

        await ShouldBeOverlapConflictAsync(response, roomId, new BookingPeriod(Start.AddMinutes(30), Start.AddMinutes(90)));
        (await StoredCountAsync(roomId)).ShouldBe(1);
    }

    [Fact]
    public async Task Accepts_a_period_adjacent_to_a_confirmed_booking_of_the_room()
    {
        await using var app = new MyAppFactory(database);
        var client = app.CreateClient();
        var roomId = Guid.NewGuid();
        (await ConfirmAsync(client, roomId, Start, Start.AddHours(1))).EnsureSuccessStatusCode();

        var response = await ConfirmAsync(client, roomId, Start.AddHours(1), Start.AddHours(2));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Accepts_the_same_period_in_another_room()
    {
        await using var app = new MyAppFactory(database);
        var client = app.CreateClient();
        (await ConfirmAsync(client, Guid.NewGuid(), Start, Start.AddHours(1))).EnsureSuccessStatusCode();

        var response = await ConfirmAsync(client, Guid.NewGuid(), Start, Start.AddHours(1));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Accepts_a_period_freed_by_a_cancellation()
    {
        await using var app = new MyAppFactory(database);
        var client = app.CreateClient();
        var roomId = Guid.NewGuid();
        var confirmed = await ConfirmAsync(client, roomId, Start, Start.AddHours(1));
        (await client.PostAsync($"{confirmed.Headers.Location}/cancel", null, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();

        var response = await ConfirmAsync(client, roomId, Start, Start.AddHours(1));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Confirms_exactly_one_of_simultaneous_bookings_of_the_same_period()
    {
        await using var app = new MyAppFactory(database);
        var client = app.CreateClient();
        var roomId = Guid.NewGuid();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => ConfirmAsync(client, roomId, Start, Start.AddHours(1))));

        responses.Count(response => response.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        responses.Count(response => response.StatusCode == HttpStatusCode.Conflict).ShouldBe(responses.Length - 1);
        (await StoredCountAsync(roomId)).ShouldBe(1);
    }

    // Deterministic race: the request loads the room's bookings before a concurrent one is committed, so only the
    // database exclusion constraint can reject it. Its violation must look exactly like the domain's conflict.
    [Fact]
    public async Task Rejects_an_overlap_detected_only_by_the_database_with_the_same_409()
    {
        await using var app = new MyAppFactory(database);
        var roomId = Guid.NewGuid();
        var period = new BookingPeriod(Start, Start.AddHours(1));
        await using var concurrent = database.CreateDbContext();
        await using var transaction = await concurrent.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        concurrent.Set<Booking>().Add(Booking.Confirm(roomId, period, []));
        await concurrent.SaveChangesAsync(TestContext.Current.CancellationToken);

        var request = ConfirmAsync(app.CreateClient(), roomId, period.Start, period.End);
        await WaitUntilAnInsertIsBlockedAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
        var response = await request;

        await ShouldBeOverlapConflictAsync(response, roomId, period);
        (await StoredCountAsync(roomId)).ShouldBe(1);
    }

    private static Task<HttpResponseMessage> ConfirmAsync(HttpClient client, Guid roomId, DateTimeOffset start, DateTimeOffset end) =>
        client.PostAsJsonAsync("/bookings", new ConfirmBookingCommand(roomId, start, end), TestContext.Current.CancellationToken);

    private static async Task ShouldBeOverlapConflictAsync(HttpResponseMessage response, Guid roomId, BookingPeriod period)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldNotContain("EX_Bookings");
        body.ShouldNotContain("23P01");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        problem.ShouldNotBeNull();
        problem.Detail.ShouldBe(new BookingConflictException(roomId, period).Message);
    }

    private async Task WaitUntilAnInsertIsBlockedAsync()
    {
        await using var db = database.CreateDbContext();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        while (!await db.Database
            .SqlQuery<bool>($"""SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE wait_event_type = 'Lock' AND query LIKE '%INSERT INTO "Bookings"%') AS "Value" """)
            .SingleAsync(timeout.Token))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
        }
    }

    private async Task<int> StoredCountAsync(Guid roomId)
    {
        await using var db = database.CreateDbContext();
        return await db.Set<Booking>().CountAsync(booking => booking.RoomId == roomId, TestContext.Current.CancellationToken);
    }
}
