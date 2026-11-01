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
        builder.ToTable("Stocks", table =>
            table.HasCheckConstraint("CK_Stocks_Quantity_Nonnegative", "\"Quantity\" >= 0"));

        builder.HasKey(aggregate => aggregate.Id);
        builder.Property(aggregate => aggregate.Id)
            .HasConversion(id => id.Value, value => new StockId(value))
            .ValueGeneratedNever();

        builder.Property(stock => stock.ProductId)
            .HasConversion(id => id.Value, value => new ProductId(value))
            .IsRequired();
        builder.Property(stock => stock.Quantity).IsRequired();
        builder.HasOne<Product>().WithOne().HasForeignKey<Stock>(stock => stock.ProductId);

        builder.Ignore(aggregate => aggregate.DomainEvents);
    }
}
