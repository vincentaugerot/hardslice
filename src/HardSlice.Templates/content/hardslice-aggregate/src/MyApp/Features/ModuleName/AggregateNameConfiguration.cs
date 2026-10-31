using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApp.Domain.ModuleName;

namespace MyApp.Features.ModuleName;

// Module-level mapping, outside any slice: the domain stays free of EF Core attributes and references.
// AppDbContext applies it by itself; each change of the mapping needs a migration.
internal sealed class AggregateNameConfiguration : IEntityTypeConfiguration<AggregateName>
{
    public void Configure(EntityTypeBuilder<AggregateName> builder)
    {
        builder.ToTable("AggregateTable");

        builder.HasKey(aggregate => aggregate.Id);
        builder.Property(aggregate => aggregate.Id)
            .HasConversion(id => id.Value, value => new AggregateNameId(value))
            .ValueGeneratedNever();

        builder.Ignore(aggregate => aggregate.DomainEvents);
    }
}
