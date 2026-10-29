using Microsoft.EntityFrameworkCore;

namespace MyApp.IntegrationTests.Shared.Persistence;

public sealed class MigrationsTests(PostgresDatabase database)
{
    // A mapping changed without its migration would break 'dotnet ef database update' on a real database.
    [Fact]
    public async Task Model_has_no_changes_missing_from_the_migrations()
    {
        await using var db = database.CreateDbContext();

        db.Database.HasPendingModelChanges().ShouldBeFalse(
            "Add a migration: dotnet ef migrations add <Name> --project src/MyApp --output-dir Shared/Persistence/Migrations");
    }
}
