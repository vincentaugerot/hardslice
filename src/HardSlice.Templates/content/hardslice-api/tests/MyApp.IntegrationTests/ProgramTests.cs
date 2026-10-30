using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Shared.Persistence;

namespace MyApp.IntegrationTests;

// The application as Program.cs composes it, modules included. The factory runs it in Development,
// where starting the host also validates every service registration.
public sealed class ProgramTests(PostgresDatabase database)
{
    [Fact]
    public async Task Starts_and_answers_HTTP_requests_on_its_migrated_database()
    {
        await using var app = new MyAppFactory(database);

        var response = await app.CreateClient().GetAsync($"/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Database.CanConnectAsync(TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await db.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
    }
}
