using Microsoft.EntityFrameworkCore;
using MyApp.IntegrationTests;
using MyApp.Shared.Persistence;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(PostgresDatabase))]

namespace MyApp.IntegrationTests;

// One PostgreSQL container per test run, migrated like a real database. Tests isolate themselves by creating their own rows.
public sealed class PostgresDatabase : IAsyncLifetime
{
    // Pinned by digest (multi-architecture index): the tag alone could change the database under the tests.
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder("postgres:18-alpine@sha256:10a5c69a83035ab5239f5620d708520e8b28f5aa1f2b20143e6024532edadfb5").Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    internal AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options);

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
