using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using MyApp.Shared.Persistence;

namespace MyApp.IntegrationTests;

// One running instance of the application on the test database. Several factories on the same database act as several instances.
public sealed class AppFactory(PostgresDatabase database) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting($"ConnectionStrings:{PersistenceServiceCollectionExtensions.ConnectionStringName}", database.ConnectionString);
}
