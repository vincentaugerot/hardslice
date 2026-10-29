using Microsoft.EntityFrameworkCore;

namespace MyApp.Shared.Persistence;

// Declares no module type: each module maps its aggregates in its own IEntityTypeConfiguration,
// and slices reach them through Set<T>(). Adding a module never changes Shared.
internal sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
