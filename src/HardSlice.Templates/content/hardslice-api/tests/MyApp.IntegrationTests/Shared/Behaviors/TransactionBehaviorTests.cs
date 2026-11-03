using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
#if (!NoSample)
using MyApp.Domain.Bookings;
#endif
using MyApp.Shared.Messaging;
using MyApp.Shared.Persistence;

namespace MyApp.IntegrationTests.Shared.Behaviors;

// Commands sent through the application's own pipeline, with handlers registered for these tests only.
public sealed class TransactionBehaviorTests(PostgresDatabase database)
{
    [Fact]
    public async Task Commits_what_a_handler_wrote_inside_the_transaction_of_its_command()
    {
        var id = Guid.NewGuid();
        await CreateProbeTableAsync();

        await SendAsync(new WriteProbe(id, Fail: false));

        (await ProbeCountAsync(id)).ShouldBe(1);
    }

    [Fact]
    public async Task Rolls_back_what_a_handler_wrote_when_it_throws()
    {
        var id = Guid.NewGuid();
        await CreateProbeTableAsync();

        await Should.ThrowAsync<InvalidOperationException>(() => SendAsync(new WriteProbe(id, Fail: true)));

        (await ProbeCountAsync(id)).ShouldBe(0);
    }
#if (!NoSample)

    private static readonly BookingPeriod Period = new(TestInstants.Start, TestInstants.Start.AddHours(1));

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
#endif

    private Task SendAsync<TResponse>(ICommand<TResponse> command) =>
        RunInScopeAsync((mediator, _) => mediator.Send(command, TestContext.Current.CancellationToken));

    private async Task RunInScopeAsync(Func<IMediator, AppDbContext, Task> action)
    {
        await using var factory = new AppFactory(database);
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services
#if (!NoSample)
            .AddScoped<IRequestHandler<AddBooking, bool>, AddBookingHandler>()
            .AddScoped<IRequestHandler<SaveBookingThenFail, bool>, SaveBookingThenFailHandler>()
            .AddScoped<IRequestHandler<CancelBookingThenFail, bool>, CancelBookingThenFailHandler>()
#endif
            .AddScoped<IRequestHandler<WriteProbe, bool>, WriteProbeHandler>()));
        await using var scope = app.Services.CreateAsyncScope();

        await action(scope.ServiceProvider.GetRequiredService<IMediator>(), scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    // A table of these tests only, outside the model: what a handler writes there with SQL shows what the transaction kept.
    private async Task CreateProbeTableAsync()
    {
        await using var db = database.CreateDbContext();
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS \"TransactionBehaviorProbes\" (\"Id\" uuid PRIMARY KEY)", TestContext.Current.CancellationToken);
    }

    private async Task<int> ProbeCountAsync(Guid id)
    {
        await using var db = database.CreateDbContext();
        return await db.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM \"TransactionBehaviorProbes\" WHERE \"Id\" = {id}")
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    public sealed record WriteProbe(Guid Id, bool Fail) : ICommand<bool>;

    private sealed class WriteProbeHandler(AppDbContext db) : IRequestHandler<WriteProbe, bool>
    {
        public async Task<bool> Handle(WriteProbe command, CancellationToken cancellationToken)
        {
            db.Database.CurrentTransaction.ShouldNotBeNull();
            await db.Database.ExecuteSqlAsync($"INSERT INTO \"TransactionBehaviorProbes\" (\"Id\") VALUES ({command.Id})", cancellationToken);
            if (command.Fail)
                throw new InvalidOperationException("Failed after writing a probe.");
            return true;
        }
    }
#if (!NoSample)

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
            booking!.Cancel();
            if (command.SaveBeforeFailure)
                await db.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Failed after cancelling a booking.");
        }
    }
#endif
}
