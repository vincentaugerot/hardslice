using Microsoft.EntityFrameworkCore;

namespace MyApp.Shared.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public const string ConnectionStringName = "Database";

    // The application never migrates its database: run 'dotnet ef database update' instead.
    // No retrying execution strategy, such as EnableRetryOnFailure, which an error of Npgsql may suggest: it refuses the transaction
    // that TransactionBehavior opens itself, and every command would fail.
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration) =>
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString(ConnectionStringName)));
}
