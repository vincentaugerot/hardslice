using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Demo.Domain;
using Demo.Domain.Bookings;
using Demo.Features.Bookings.CancelBooking;
using Demo.Shared.Messaging;

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

        var cancelled = await SendAsync(new CancelBookingCommand(booking.Id.Value), _beforeStart);

        cancelled.ShouldBeTrue();
        (await StoredStatusAsync(booking.Id)).ShouldBe(BookingStatus.Cancelled);
    }

    [Fact]
    public async Task Reports_an_unknown_booking()
    {
        var cancelled = await SendAsync(new CancelBookingCommand(Guid.NewGuid()), _beforeStart);

        cancelled.ShouldBeFalse();
    }

    [Fact]
    public async Task Rejects_a_booking_that_has_started()
    {
        var booking = await StoreAsync(Booking.Confirm(Guid.NewGuid(), Period, []));

        await Should.ThrowAsync<DomainException>(() => SendAsync(new CancelBookingCommand(booking.Id.Value), new FixedClock(Period.Start)));

        (await StoredStatusAsync(booking.Id)).ShouldBe(BookingStatus.Confirmed);
    }

    // Through the application's pipeline, as its endpoint sends it, at the time the clock gives.
    private async Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, TimeProvider clock)
    {
        await using var factory = new AppFactory(database);
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton(clock)));
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command, TestContext.Current.CancellationToken);
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
