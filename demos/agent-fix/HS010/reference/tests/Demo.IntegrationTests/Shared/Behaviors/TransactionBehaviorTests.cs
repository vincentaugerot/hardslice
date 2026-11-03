using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Demo.Domain.Bookings;
using Demo.Shared.Messaging;
using Demo.Shared.Persistence;

namespace Demo.IntegrationTests.Shared.Behaviors;

// Commands sent through the application's own pipeline, with handlers registered for these tests only.
public sealed class TransactionBehaviorTests(PostgresDatabase database)
{
    private static readonly BookingPeriod Period = new(
        new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 11, 2, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Saves_and_commits_what_the_handler_changed()
    {
        var roomId = Guid.NewGuid();

        await SendAsync(new AddBooking(roomId));

        (await StoredCountAsync(roomId)).ShouldBe(1);
    }

    [Fact]
    public async Task Rolls_back_everything_a_command_saved_when_it_throws()
    {
        var roomId = Guid.NewGuid();

        await Should.ThrowAsync<InvalidOperationException>(() => SendAsync(new SaveBookingThenFail(roomId)));

        (await StoredCountAsync(roomId)).ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_later_command_never_commits_a_failed_commands_additions(bool saveBeforeFailure)
    {
        var failedRoom = Guid.NewGuid();
        var successfulRoom = Guid.NewGuid();

        await RunInScopeAsync(async (mediator, _) =>
        {
            await Should.ThrowAsync<InvalidOperationException>(() => mediator.Send(
                new SaveBookingThenFail(failedRoom, saveBeforeFailure), TestContext.Current.CancellationToken));

            await mediator.Send(new AddBooking(successfulRoom), TestContext.Current.CancellationToken);
        });

        (await StoredCountAsync(failedRoom)).ShouldBe(0);
        (await StoredCountAsync(successfulRoom)).ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_later_command_reloads_the_state_before_a_failed_commands_changes(bool saveBeforeFailure)
    {
        var booking = Booking.Confirm(Guid.NewGuid(), Period, []);
        await using (var db = database.CreateDbContext())
        {
            db.Set<Booking>().Add(booking);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await RunInScopeAsync(async (mediator, db) =>
        {
            await Should.ThrowAsync<InvalidOperationException>(() => mediator.Send(
                new CancelBookingThenFail(booking.Id, saveBeforeFailure), TestContext.Current.CancellationToken));

            await mediator.Send(new AddBooking(Guid.NewGuid()), TestContext.Current.CancellationToken);
            var reloaded = await db.Set<Booking>().FindAsync([booking.Id], TestContext.Current.CancellationToken);
            reloaded.ShouldNotBeNull();
            reloaded.Status.ShouldBe(BookingStatus.Confirmed);
        });

        await using var readDb = database.CreateDbContext();
        var stored = await readDb.Set<Booking>().SingleAsync(b => b.Id == booking.Id, TestContext.Current.CancellationToken);
        stored.Status.ShouldBe(BookingStatus.Confirmed);
    }

    private Task SendAsync<TResponse>(ICommand<TResponse> command) =>
        RunInScopeAsync((mediator, _) => mediator.Send(command, TestContext.Current.CancellationToken));

    private async Task RunInScopeAsync(Func<IMediator, AppDbContext, Task> action)
    {
        await using var factory = new AppFactory(database);
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services
            .AddScoped<IRequestHandler<AddBooking, bool>, AddBookingHandler>()
            .AddScoped<IRequestHandler<SaveBookingThenFail, bool>, SaveBookingThenFailHandler>()
            .AddScoped<IRequestHandler<CancelBookingThenFail, bool>, CancelBookingThenFailHandler>()));
        await using var scope = app.Services.CreateAsyncScope();

        await action(scope.ServiceProvider.GetRequiredService<IMediator>(), scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private async Task<int> StoredCountAsync(Guid roomId)
    {
        await using var db = database.CreateDbContext();
        return await db.Set<Booking>().CountAsync(booking => booking.RoomId == roomId, TestContext.Current.CancellationToken);
    }

    public sealed record AddBooking(Guid RoomId) : ICommand<bool>;

    public sealed record SaveBookingThenFail(Guid RoomId, bool SaveBeforeFailure = true) : ICommand<bool>;

    public sealed record CancelBookingThenFail(BookingId BookingId, bool SaveBeforeFailure) : ICommand<bool>;

    private sealed class AddBookingHandler(AppDbContext db) : IRequestHandler<AddBooking, bool>
    {
        public Task<bool> Handle(AddBooking command, CancellationToken cancellationToken)
        {
            db.Set<Booking>().Add(Booking.Confirm(command.RoomId, Period, []));
            return Task.FromResult(true);
        }
    }

    private sealed class SaveBookingThenFailHandler(AppDbContext db) : IRequestHandler<SaveBookingThenFail, bool>
    {
        public async Task<bool> Handle(SaveBookingThenFail command, CancellationToken cancellationToken)
        {
            db.Set<Booking>().Add(Booking.Confirm(command.RoomId, Period, []));
            if (command.SaveBeforeFailure)
                await db.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Failed after adding a booking.");
        }
    }

    private sealed class CancelBookingThenFailHandler(AppDbContext db) : IRequestHandler<CancelBookingThenFail, bool>
    {
        public async Task<bool> Handle(CancelBookingThenFail command, CancellationToken cancellationToken)
        {
            var booking = await db.Set<Booking>().FindAsync([command.BookingId], cancellationToken);
            booking!.Cancel(DateTimeOffset.UnixEpoch);
            if (command.SaveBeforeFailure)
                await db.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Failed after cancelling a booking.");
        }
    }
}
