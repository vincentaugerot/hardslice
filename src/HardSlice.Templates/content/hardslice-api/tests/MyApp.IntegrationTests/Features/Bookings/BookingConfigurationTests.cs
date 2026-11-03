using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MyApp.Domain.Bookings;
using MyApp.Shared.Persistence;
using Npgsql;

namespace MyApp.IntegrationTests.Features.Bookings;

public sealed class BookingConfigurationTests(PostgresDatabase database)
{
    private static readonly DateTimeOffset Start = TestInstants.Start;

    [Fact]
    public async Task Database_rejects_an_empty_period_even_when_bypassing_the_domain()
    {
        await using var db = database.CreateDbContext();
        var id = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        try
        {
            var exception = await Should.ThrowAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"""INSERT INTO "Bookings" ("Id", "RoomId", "Start", "End", "Status") VALUES ({id}, {roomId}, {Start}, {Start}, 'Confirmed')""",
                TestContext.Current.CancellationToken));

            exception.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
            exception.ConstraintName.ShouldBe("CK_Bookings_Period_Positive");
        }
        finally
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM "Bookings" WHERE "Id" = {id}""", TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Upgrade_preserves_bookings_and_requires_explicit_repair_of_legacy_empty_periods()
    {
        // A separate database exercises the old schema without changing the other tests' shared database.
        var connection = new NpgsqlConnectionStringBuilder(database.ConnectionString)
        {
            Database = $"period_upgrade_{Guid.NewGuid():N}"
        };
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection.ConnectionString).Options);
        try
        {
            await db.GetService<IMigrator>().MigrateAsync("20261029160532_AddBookingNoOverlapConstraint", TestContext.Current.CancellationToken);
            var valid = Booking.Confirm(Guid.NewGuid(), new BookingPeriod(Start, Start.AddHours(1)), []);
            db.Set<Booking>().Add(valid);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            var invalidId = Guid.NewGuid();
            var roomId = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""INSERT INTO "Bookings" ("Id", "RoomId", "Start", "End", "Status") VALUES ({invalidId}, {roomId}, {Start}, {Start}, 'Confirmed')""",
                TestContext.Current.CancellationToken);

            var exception = await Should.ThrowAsync<PostgresException>(() => db.Database.MigrateAsync(TestContext.Current.CancellationToken));

            exception.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
            exception.ConstraintName.ShouldBe("CK_Bookings_Period_Positive");
            (await db.Set<Booking>().CountAsync(TestContext.Current.CancellationToken)).ShouldBe(2);
            // Simulate an operator restoring the intended bounds before retrying the migration.
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE "Bookings" SET "End" = {Start.AddHours(1)} WHERE "Id" = {invalidId}""", TestContext.Current.CancellationToken);
            await db.Database.MigrateAsync(TestContext.Current.CancellationToken);

            (await db.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
            var reloaded = await db.Set<Booking>().AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
            reloaded.Count.ShouldBe(2);
            reloaded.Single(booking => booking.Id == valid.Id).Period.ShouldBe(valid.Period);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync(CancellationToken.None);
        }
    }

    // Domain events describe what happened during one use case: they are not stored with the booking.
    [Fact]
    public async Task Reloads_a_booking_without_its_domain_events()
    {
        var booking = Booking.Confirm(Guid.NewGuid(), new BookingPeriod(Start, Start.AddHours(1)), []);
        await using (var db = database.CreateDbContext())
        {
            db.Set<Booking>().Add(booking);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var freshDb = database.CreateDbContext();
        var reloaded = await freshDb.Set<Booking>().SingleAsync(stored => stored.Id == booking.Id, TestContext.Current.CancellationToken);

        reloaded.DomainEvents.ShouldBeEmpty();
    }
}
