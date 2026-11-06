using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyApp.Shared.Persistence;

namespace MyApp.IntegrationTests.Shared.Persistence;

// PostgresDatabase.CreateIsolatedAsync(): the database of one test, for a test whose code works on every row that matches a rule.
public sealed class IsolatedDatabaseTests(PostgresDatabase database)
{
    [Fact]
    public async Task Gives_each_test_a_migrated_database_without_the_rows_of_the_shared_database_or_of_another_test()
    {
        var sharedRow = Guid.NewGuid();
        await using (var shared = database.CreateDbContext())
        {
            await CreateProbeTableAsync(shared);
            await InsertProbeAsync(shared, sharedRow);
        }

        var created = await Task.WhenAll(database.CreateIsolatedAsync(), database.CreateIsolatedAsync());
        await using var first = created[0];
        await using var second = created[1];
        first.Name.ShouldNotBe(second.Name);

        var rows = new List<Guid>();
        foreach (var isolated in created)
        {
            await using var db = isolated.CreateDbContext();
            (await db.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
            await CreateProbeTableAsync(db);
            var row = Guid.NewGuid();
            await InsertProbeAsync(db, row);
            rows.Add(row);
        }

        for (var index = 0; index < created.Length; index++)
        {
            await using var db = created[index].CreateDbContext();
            (await ProbeRowsAsync(db)).ShouldBe([rows[index]], $"{created[index].Name} sees the rows of another database.");
        }

        await using (var shared = database.CreateDbContext())
        {
            var seen = await ProbeRowsAsync(shared);
            seen.ShouldContain(sharedRow);
            seen.ShouldNotContain(row => rows.Contains(row), "The shared database sees the rows of an isolated one.");
        }
    }

    [Fact]
    public async Task Runs_an_AppFactory_on_the_isolated_database_and_drops_it_when_the_test_disposes_of_it()
    {
        var isolated = await database.CreateIsolatedAsync();
        await using (isolated)
        {
            await using var app = new AppFactory(isolated);
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            (await db.Database.SqlQuery<string>($"SELECT current_database() AS \"Value\"").SingleAsync(TestContext.Current.CancellationToken))
                .ShouldBe(isolated.Name);
            app.Services.GetRequiredService<IHostEnvironment>().EnvironmentName.ShouldBe(AppFactory.TestEnvironment);
        }

        await using var shared = database.CreateDbContext();
        (await shared.Database
                .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM pg_database WHERE datname = {isolated.Name}")
                .SingleAsync(TestContext.Current.CancellationToken))
            .ShouldBe(0, $"{isolated.Name} was not dropped.");
    }

    // A table of these tests only, outside the model.
    private static Task CreateProbeTableAsync(AppDbContext db) =>
        db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS \"IsolatedDatabaseProbes\" (\"Id\" uuid PRIMARY KEY)", TestContext.Current.CancellationToken);

    private static Task InsertProbeAsync(AppDbContext db, Guid id) =>
        db.Database.ExecuteSqlAsync($"INSERT INTO \"IsolatedDatabaseProbes\" (\"Id\") VALUES ({id})", TestContext.Current.CancellationToken);

    private static Task<List<Guid>> ProbeRowsAsync(AppDbContext db) =>
        db.Database.SqlQuery<Guid>($"SELECT \"Id\" AS \"Value\" FROM \"IsolatedDatabaseProbes\"").ToListAsync(TestContext.Current.CancellationToken);
}
