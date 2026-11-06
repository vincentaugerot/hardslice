using Microsoft.EntityFrameworkCore;
using MyApp.IntegrationTests;
using MyApp.Shared.Persistence;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(PostgresDatabase))]

namespace MyApp.IntegrationTests;

// One PostgreSQL container per test run, migrated like a real database. Tests isolate themselves by creating their own rows.
// A test whose code works on every row that matches a rule, such as a purge, a retention or a catch-up job, would also work on the
// rows of the tests that run beside it: it takes a database of its own instead, await using var isolated = await
// database.CreateIsolatedAsync(), and starts the application on it with new AppFactory(isolated). That database is migrated, lives
// in the same container and is dropped when the test disposes of it. It is copied from a model database migrated once per run, so
// creating one costs no migration.
public sealed class PostgresDatabase : IAsyncLifetime
{
    private const string ModelDatabase = "isolated_model";

    // Pinned by digest (multi-architecture index): the tag alone could change the database under the tests.
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder("postgres:18-alpine@sha256:10a5c69a83035ab5239f5620d708520e8b28f5aa1f2b20143e6024532edadfb5").Build();

    private readonly Lazy<Task> _modelDatabase;

    public PostgresDatabase() => _modelDatabase = new(CreateModelDatabaseAsync);

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    internal AppDbContext CreateDbContext() => CreateDbContext(ConnectionString);

    // A migrated database of the calling test alone, in the same container, dropped when the test disposes of it.
    public async Task<IsolatedDatabase> CreateIsolatedAsync()
    {
        await _modelDatabase.Value;

        var name = $"isolated_{Guid.NewGuid():N}";
        await ExecuteAsync($"CREATE DATABASE \"{name}\" TEMPLATE \"{ModelDatabase}\"");
        return new IsolatedDatabase(this, name, ConnectionStringOf(name));
    }

    internal Task DropAsync(string name, string connectionString)
    {
        // The pooled connections of the application and of its DbContexts would hold the database open.
        using (var connection = new NpgsqlConnection(connectionString))
            NpgsqlConnection.ClearPool(connection);
        return ExecuteAsync($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)");
    }

    internal static AppDbContext CreateDbContext(string connectionString) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);

    // CREATE DATABASE copies a template only while no session is connected to it: the model is migrated without pooling, so no
    // connection to it stays open after.
    private async Task CreateModelDatabaseAsync()
    {
        await ExecuteAsync($"CREATE DATABASE \"{ModelDatabase}\"");
        var connectionString = new NpgsqlConnectionStringBuilder(ConnectionStringOf(ModelDatabase)) { Pooling = false }.ConnectionString;
        await using var db = CreateDbContext(connectionString);
        await db.Database.MigrateAsync();
    }

    private string ConnectionStringOf(string database) =>
        new NpgsqlConnectionStringBuilder(ConnectionString) { Database = database }.ConnectionString;

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}

// A database of one test, from PostgresDatabase.CreateIsolatedAsync(): migrated, with no row of the other tests. AppFactory takes it
// as it takes PostgresDatabase.
public sealed class IsolatedDatabase : IAsyncDisposable
{
    private readonly PostgresDatabase _container;

    internal IsolatedDatabase(PostgresDatabase container, string name, string connectionString)
    {
        _container = container;
        Name = name;
        ConnectionString = connectionString;
    }

    public string Name { get; }

    public string ConnectionString { get; }

    internal AppDbContext CreateDbContext() => PostgresDatabase.CreateDbContext(ConnectionString);

    public async ValueTask DisposeAsync() => await _container.DropAsync(Name, ConnectionString);
}
