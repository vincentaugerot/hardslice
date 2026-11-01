using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shop.Domain.Catalog;

namespace Shop.Features.Catalog;

// Module-level mapping, outside any slice: the domain stays free of EF Core attributes and references.
// AppDbContext applies it by itself; each change of the mapping needs a migration.
internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");

        builder.HasKey(aggregate => aggregate.Id);
        builder.Property(aggregate => aggregate.Id)
            .HasConversion(id => id.Value, value => new ProductId(value))
            .ValueGeneratedNever();

        builder.Property(aggregate => aggregate.Name)
            .HasMaxLength(Product.NameMaxLength)
            .IsRequired();

        // In euros.
        builder.Property(aggregate => aggregate.Price)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.ToTable(table => table.HasCheckConstraint("CK_Products_Price_Positive", "\"Price\" > 0"));

        builder.Ignore(aggregate => aggregate.DomainEvents);
    }
}
