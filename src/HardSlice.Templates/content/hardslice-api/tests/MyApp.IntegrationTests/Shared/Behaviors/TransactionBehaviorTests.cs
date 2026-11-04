using System.Diagnostics;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
#if (!NoSample)
using MyApp.Domain.Bookings;
#endif
using MyApp.Domain;
using MyApp.Shared.Messaging;
using MyApp.Shared.Persistence;
using Npgsql;

namespace MyApp.IntegrationTests.Shared.Behaviors;

// Commands sent through the application's own pipeline, with handlers registered for these tests only.
public sealed class TransactionBehaviorTests(PostgresDatabase database)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

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

    // PB-069: two commands that create the same row under a unique key, the second one waiting on the index until the first commits.
    [Fact]
    public async Task Answers_a_conflict_to_the_second_of_two_concurrent_creations_under_the_same_key()
    {
        var id = Guid.NewGuid();
        await CreateProbeTableAsync();
        var firstInserted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondConnection = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        await WithAppAsync(async services =>
        {
            var first = SendInScopeAsync(services, new HoldProbe(id, firstInserted, releaseFirst.Task));
            try
            {
                await firstInserted.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
                var second = SendInScopeAsync(services, new InsertProbe(id, secondConnection));
                await WaitUntilBlockedAsync(await secondConnection.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken));
                releaseFirst.SetResult();

                (await first).ShouldBeTrue();
                var conflict = await Should.ThrowAsync<ConflictException>(second);
                conflict.Message.ShouldNotContain(id.ToString());
            }
            finally
            {
                releaseFirst.TrySetResult();
            }
        });

        (await ProbeCountAsync(id)).ShouldBe(1);
    }

    [Fact]
    public async Task Keeps_the_exception_of_a_handler_that_translates_a_unique_violation_itself()
    {
        var id = Guid.NewGuid();
        await CreateProbeTableAsync();
        await SendAsync(new WriteProbe(id, Fail: false));

        var exception = await Should.ThrowAsync<DomainException>(() => SendAsync(new WriteProbeOnce(id)));

        exception.ShouldNotBeOfType<ConflictException>();
        exception.Message.ShouldBe(WriteProbeOnceHandler.AlreadyWritten);
        (await ProbeCountAsync(id)).ShouldBe(1);
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

    // PB-069: the unique violation of SaveChangesAsync, wrapped in a DbUpdateException. Here the primary key of a booking
    // stored again; the booking is cancelled, so that the exclusion constraint of confirmed bookings stays out of the way.
    [Fact]
    public async Task Answers_a_conflict_to_a_creation_that_EF_Core_saves_under_an_existing_key()
    {
        var booking = Booking.Confirm(Guid.NewGuid(), Period, []);
        booking.Cancel();
        await using (var db = database.CreateDbContext())
        {
            db.Set<Booking>().Add(booking);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var conflict = await Should.ThrowAsync<ConflictException>(() => SendAsync(new AddStoredBookingAgain(booking.Id)));

        conflict.Message.ShouldNotContain(booking.Id.Value.ToString());
        (await StoredCountAsync(booking.RoomId)).ShouldBe(1);
    }
#endif

    private Task SendAsync<TResponse>(ICommand<TResponse> command) =>
        RunInScopeAsync((mediator, _) => mediator.Send(command, TestContext.Current.CancellationToken));

    private Task RunInScopeAsync(Func<IMediator, AppDbContext, Task> action) =>
        WithAppAsync(async services =>
        {
            await using var scope = services.CreateAsyncScope();
            await action(scope.ServiceProvider.GetRequiredService<IMediator>(), scope.ServiceProvider.GetRequiredService<AppDbContext>());
        });

    private async Task WithAppAsync(Func<IServiceProvider, Task> action)
    {
        await using var factory = new AppFactory(database);
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services
#if (!NoSample)
            .AddScoped<IRequestHandler<AddBooking, bool>, AddBookingHandler>()
            .AddScoped<IRequestHandler<SaveBookingThenFail, bool>, SaveBookingThenFailHandler>()
            .AddScoped<IRequestHandler<CancelBookingThenFail, bool>, CancelBookingThenFailHandler>()
            .AddScoped<IRequestHandler<AddStoredBookingAgain, bool>, AddStoredBookingAgainHandler>()
#endif
            .AddScoped<IRequestHandler<WriteProbe, bool>, WriteProbeHandler>()
            .AddScoped<IRequestHandler<WriteProbeOnce, bool>, WriteProbeOnceHandler>()
            .AddScoped<IRequestHandler<HoldProbe, bool>, HoldProbeHandler>()
            .AddScoped<IRequestHandler<InsertProbe, bool>, InsertProbeHandler>()));

        await action(app.Services);
    }

    // Each command in a scope of its own, as two concurrent requests.
    private static async Task<bool> SendInScopeAsync(IServiceProvider services, ICommand<bool> command)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command, TestContext.Current.CancellationToken);
    }

    // The INSERT of that connection waits on a lock held by another transaction, here the row of the same key not yet committed.
    private async Task WaitUntilBlockedAsync(int processId)
    {
        await using var db = database.CreateDbContext();
        var waited = Stopwatch.StartNew();
        while (await db.Database
                   .SqlQuery<int>($"SELECT cardinality(pg_blocking_pids({processId}))::int AS \"Value\"")
                   .SingleAsync(TestContext.Current.CancellationToken) == 0)
        {
            if (waited.Elapsed > Timeout)
                throw new TimeoutException($"The INSERT of PostgreSQL process {processId} never waited on the unique index.");
            await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        }
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

    public sealed record WriteProbeOnce(Guid Id) : ICommand<bool>;

    // Translates the unique violation into an exception of the domain, as a handler does for a rule the database enforces.
    private sealed class WriteProbeOnceHandler(AppDbContext db) : IRequestHandler<WriteProbeOnce, bool>
    {
        public const string AlreadyWritten = "This probe is already written.";

        public async Task<bool> Handle(WriteProbeOnce command, CancellationToken cancellationToken)
        {
            try
            {
                await db.Database.ExecuteSqlAsync($"INSERT INTO \"TransactionBehaviorProbes\" (\"Id\") VALUES ({command.Id})", cancellationToken);
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                throw new DomainException(AlreadyWritten);
            }

            return true;
        }
    }

    public sealed record HoldProbe(Guid Id, TaskCompletionSource Inserted, Task Release) : ICommand<bool>;

    // Writes its probe, then keeps its transaction open until the test releases it.
    private sealed class HoldProbeHandler(AppDbContext db) : IRequestHandler<HoldProbe, bool>
    {
        public async Task<bool> Handle(HoldProbe command, CancellationToken cancellationToken)
        {
            await db.Database.ExecuteSqlAsync($"INSERT INTO \"TransactionBehaviorProbes\" (\"Id\") VALUES ({command.Id})", cancellationToken);
            command.Inserted.SetResult();
            await command.Release.WaitAsync(cancellationToken);
            return true;
        }
    }

    public sealed record InsertProbe(Guid Id, TaskCompletionSource<int> Connection) : ICommand<bool>;

    // Tells the test which PostgreSQL process runs its transaction, then writes its probe.
    private sealed class InsertProbeHandler(AppDbContext db) : IRequestHandler<InsertProbe, bool>
    {
        public async Task<bool> Handle(InsertProbe command, CancellationToken cancellationToken)
        {
            command.Connection.SetResult(await db.Database
                .SqlQuery<int>($"SELECT pg_backend_pid() AS \"Value\"")
                .SingleAsync(cancellationToken));
            await db.Database.ExecuteSqlAsync($"INSERT INTO \"TransactionBehaviorProbes\" (\"Id\") VALUES ({command.Id})", cancellationToken);
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

    public sealed record AddStoredBookingAgain(BookingId BookingId) : ICommand<bool>;

    // Adds a booking read from the database as a new one: its primary key is taken.
    private sealed class AddStoredBookingAgainHandler(AppDbContext db) : IRequestHandler<AddStoredBookingAgain, bool>
    {
        public async Task<bool> Handle(AddStoredBookingAgain command, CancellationToken cancellationToken)
        {
            var stored = await db.Set<Booking>().AsNoTracking().SingleAsync(booking => booking.Id == command.BookingId, cancellationToken);
            db.Set<Booking>().Add(stored);
            return true;
        }
    }

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
