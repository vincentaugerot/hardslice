using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shop.Domain.Catalog;

namespace Shop.Features.Catalog;

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
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(aggregate => aggregate.Price)
            .HasColumnType("numeric(18,2)");

        builder.Ignore(aggregate => aggregate.DomainEvents);
    }
}
