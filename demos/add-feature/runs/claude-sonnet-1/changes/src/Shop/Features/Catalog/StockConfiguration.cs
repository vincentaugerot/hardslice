using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shop.Domain.Catalog;

namespace Shop.Features.Catalog;

// Module-level mapping, outside any slice: the domain stays free of EF Core attributes and references.
// AppDbContext applies it by itself; each change of the mapping needs a migration.
internal sealed class StockConfiguration : IEntityTypeConfiguration<Stock>
{
    public void Configure(EntityTypeBuilder<Stock> builder)
    {
        builder.ToTable("Stocks");

        builder.HasKey(aggregate => aggregate.Id);
        builder.Property(aggregate => aggregate.Id)
            .HasConversion(id => id.Value, value => new StockId(value))
            .ValueGeneratedNever();

        builder.Property(aggregate => aggregate.ProductId)
            .HasConversion(id => id.Value, value => new ProductId(value));
        builder.HasIndex(aggregate => aggregate.ProductId).IsUnique();
        builder.Property(aggregate => aggregate.Quantity);

        builder.Ignore(aggregate => aggregate.DomainEvents);
    }
}
