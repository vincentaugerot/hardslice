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
        builder.ToTable("Products", table =>
            table.HasCheckConstraint("CK_Products_Price_Positive", "\"Price\" > 0"));

        builder.HasKey(aggregate => aggregate.Id);
        builder.Property(aggregate => aggregate.Id)
            .HasConversion(id => id.Value, value => new ProductId(value))
            .ValueGeneratedNever();

        builder.Property(product => product.Name).HasMaxLength(100).IsRequired();
        builder.Property(product => product.Price)
            .HasConversion(price => price.Value, value => new EuroPrice(value))
            .HasColumnType("numeric")
            .IsRequired();

        builder.Ignore(aggregate => aggregate.DomainEvents);
    }
}
