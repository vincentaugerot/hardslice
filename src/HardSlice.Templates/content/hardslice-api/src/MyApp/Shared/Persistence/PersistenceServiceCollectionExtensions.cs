using Microsoft.EntityFrameworkCore;

namespace MyApp.Shared.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public const string ConnectionStringName = "Database";

    // The application never migrates its database: run 'dotnet ef database update' instead.
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration) =>
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString(ConnectionStringName)));
}
