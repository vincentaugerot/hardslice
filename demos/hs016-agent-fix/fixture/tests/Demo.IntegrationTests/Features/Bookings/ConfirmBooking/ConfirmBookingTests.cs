using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Demo.Domain;
using Demo.Domain.Bookings;
using Demo.Features.Bookings.ConfirmBooking;
using Demo.Shared.Messaging;

namespace Demo.IntegrationTests.Features.Bookings.ConfirmBooking;

public sealed class ConfirmBookingTests(PostgresDatabase database)
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    private readonly TimeProvider _beforeStart = new FixedClock(Start.AddDays(-1));

    [Fact]
    public async Task Stores_a_confirmed_booking_and_returns_its_identifier()
    {
        var command = new ConfirmBookingCommand(Guid.NewGuid(), Start, Start.AddHours(1));

        var id = await SendAsync(command, _beforeStart);

        await using var db = database.CreateDbContext();
        var booking = await db.Set<Booking>().SingleOrDefaultAsync(booking => booking.Id == id, TestContext.Current.CancellationToken);
        booking.ShouldNotBeNull();
        booking.RoomId.ShouldBe(command.RoomId);
        booking.Period.ShouldBe(new BookingPeriod(command.Start, command.End));
        booking.Status.ShouldBe(BookingStatus.Confirmed);
    }

    [Fact]
    public async Task Rejects_a_period_that_does_not_end_after_its_start()
    {
        var command = new ConfirmBookingCommand(Guid.NewGuid(), Start, Start);

        await Should.ThrowAsync<DomainException>(() => SendAsync(command, _beforeStart));
    }

    [Fact]
    public async Task Rejects_a_period_that_has_already_started()
    {
        var command = new ConfirmBookingCommand(Guid.NewGuid(), Start, Start.AddHours(1));

        await Should.ThrowAsync<DomainException>(() => SendAsync(command, new FixedClock(Start)));
    }

    // Through the application's pipeline, as its endpoint sends it, at the time the clock gives.
    private async Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, TimeProvider clock)
    {
        await using var factory = new DemoFactory(database);
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton(clock)));
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command, TestContext.Current.CancellationToken);
    }
}
