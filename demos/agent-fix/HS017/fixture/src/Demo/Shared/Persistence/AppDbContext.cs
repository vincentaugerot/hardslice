using Microsoft.EntityFrameworkCore;
using Demo.Features.Bookings;

namespace Demo.Shared.Persistence;

// Each module maps its aggregates in its own IEntityTypeConfiguration, and slices reach them through Set<T>().
// The mappings are applied by name, so that the model reads at a glance.
internal sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfiguration(new BookingConfiguration());
}
