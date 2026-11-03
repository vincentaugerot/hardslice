using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Domain.Bookings;
using MyApp.Features.Bookings.ConfirmBooking;
using MyApp.Features.Bookings.GetBooking;
using MyApp.Shared.Messaging;
using MyApp.Shared.Persistence;

namespace MyApp.IntegrationTests.Features.Bookings.GetBooking;

public sealed class GetBookingTests(PostgresDatabase database)
{
    private static readonly DateTimeOffset Start = new(2099, 11, 2, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Reads_a_confirmed_booking_at_the_location_returned_by_its_creation()
    {
        await using var app = new AppFactory(database);
        var client = app.CreateClient();
        var command = new ConfirmBookingCommand(Guid.NewGuid(), Start, Start.AddHours(1));
        var created = await client.PostAsJsonAsync("/bookings", command, TestContext.Current.CancellationToken);
        created.EnsureSuccessStatusCode();

        var response = await client.GetAsync(created.Headers.Location, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var booking = await response.Content.ReadFromJsonAsync<BookingDto>(TestContext.Current.CancellationToken);
        booking.ShouldNotBeNull();
        created.Headers.Location!.OriginalString.ShouldBe($"/bookings/{booking.Id}");
        booking.ShouldBe(new BookingDto(booking.Id, command.RoomId, command.Start, command.End, nameof(BookingStatus.Confirmed)));
    }

    [Fact]
    public async Task Reads_a_cancelled_booking_with_the_Cancelled_status()
    {
        await using var app = new AppFactory(database);
        var client = app.CreateClient();
        var created = await client.PostAsJsonAsync(
            "/bookings", new ConfirmBookingCommand(Guid.NewGuid(), Start, Start.AddHours(1)), TestContext.Current.CancellationToken);
        created.EnsureSuccessStatusCode();
        (await client.PostAsync($"{created.Headers.Location}/cancel", null, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();

        var booking = await client.GetFromJsonAsync<BookingDto>(created.Headers.Location, TestContext.Current.CancellationToken);

        booking.ShouldNotBeNull();
        booking.Status.ShouldBe(nameof(BookingStatus.Cancelled));
    }

    [Fact]
    public async Task Returns_404_for_an_unknown_booking()
    {
        await using var app = new AppFactory(database);

        var response = await app.CreateClient().GetAsync($"/bookings/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // Sent through the application's own mediator, in one scope: the query must leave its AppDbContext untouched.
    [Fact]
    public async Task Reads_a_booking_without_tracking_any_entity()
    {
        var stored = Booking.Confirm(Guid.NewGuid(), new BookingPeriod(Start, Start.AddHours(1)), []);
        await using (var seed = database.CreateDbContext())
        {
            seed.Set<Booking>().Add(stored);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var app = new AppFactory(database);
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var booking = await scope.ServiceProvider.GetRequiredService<IMediator>()
            .Send(new GetBookingQuery(stored.Id.Value), TestContext.Current.CancellationToken);

        booking.ShouldNotBeNull();
        booking.Id.ShouldBe(stored.Id.Value);
        db.ChangeTracker.Entries().ShouldBeEmpty();
    }
}
