using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Demo.Domain.Bookings;
using Demo.Features.Bookings.GetRoomBookings;
using Demo.Shared.Messaging;
using Demo.Shared.Persistence;

namespace Demo.IntegrationTests.Features.Bookings.GetRoomBookings;

public sealed class GetRoomBookingsTests(PostgresDatabase database)
{
    private static readonly DateTimeOffset Start = new(2099, 11, 2, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Reads_the_bookings_of_the_room_in_the_order_of_their_periods()
    {
        var roomId = Guid.NewGuid();
        var later = Booking.Confirm(roomId, new BookingPeriod(Start.AddHours(2), Start.AddHours(3)), []);
        var earlier = Booking.Confirm(roomId, new BookingPeriod(Start, Start.AddHours(1)), []);
        await StoreAsync(later, earlier, Booking.Confirm(Guid.NewGuid(), new BookingPeriod(Start, Start.AddHours(1)), []));
        await using var app = new DemoFactory(database);
        await using var scope = app.Services.CreateAsyncScope();

        var bookings = await scope.ServiceProvider.GetRequiredService<IMediator>()
            .Send(new GetRoomBookingsQuery(roomId), TestContext.Current.CancellationToken);

        bookings.Select(booking => booking.Id).ShouldBe([earlier.Id.Value, later.Id.Value]);
        bookings[0].ShouldBe(new RoomBookingDto(earlier.Id.Value, earlier.Period.Start, earlier.Period.End, nameof(BookingStatus.Confirmed)));
    }

    // Sent through the application's own mediator, in one scope: the query must leave its AppDbContext untouched.
    [Fact]
    public async Task Reads_the_bookings_of_a_room_without_tracking_any_entity()
    {
        var roomId = Guid.NewGuid();
        await StoreAsync(Booking.Confirm(roomId, new BookingPeriod(Start, Start.AddHours(1)), []));
        await using var app = new DemoFactory(database);
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var bookings = await scope.ServiceProvider.GetRequiredService<IMediator>()
            .Send(new GetRoomBookingsQuery(roomId), TestContext.Current.CancellationToken);

        bookings.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Answers_an_empty_list_over_http_for_a_room_without_bookings()
    {
        await using var app = new DemoFactory(database);

        var bookings = await app.CreateClient().GetFromJsonAsync<RoomBookingDto[]>($"/bookings/rooms/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        bookings.ShouldNotBeNull();
        bookings.ShouldBeEmpty();
    }

    private async Task StoreAsync(params Booking[] bookings)
    {
        await using var db = database.CreateDbContext();
        db.Set<Booking>().AddRange(bookings);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
