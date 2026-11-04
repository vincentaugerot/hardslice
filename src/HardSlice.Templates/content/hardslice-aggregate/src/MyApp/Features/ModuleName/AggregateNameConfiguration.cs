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

        // Optimistic concurrency: PostgreSQL changes the xmin system column of a row at each update, and EF Core updates an
        // aggregate only while its xmin is the one it loaded. A command that changes an aggregate another request changed since it
        // loaded it saves nothing: TransactionBehavior answers it with 409. A shadow property, so that the domain knows nothing of
        // it, which the migration of the aggregate records in the model without creating a column.
        builder.Property<uint>("Version").IsRowVersion();

        builder.Ignore(aggregate => aggregate.DomainEvents);
    }
}
