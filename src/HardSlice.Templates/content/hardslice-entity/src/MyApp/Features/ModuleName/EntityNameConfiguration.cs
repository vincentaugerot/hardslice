using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApp.Domain.ModuleName;

namespace MyApp.Features.ModuleName;

// Module-level mapping of a child entity of AggregateName, next to the mapping of its root: the domain stays free of EF Core
// attributes and references. AppDbContext applies it by itself; each change of the mapping needs a migration.
internal sealed class EntityNameConfiguration : IEntityTypeConfiguration<EntityName>
{
    public void Configure(EntityTypeBuilder<EntityName> builder)
    {
        builder.ToTable("EntityTable");

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id)
            .HasConversion(id => id.Value, value => new EntityNameId(value))
            .ValueGeneratedNever();

#if (LinkIsId)
        // The identifier of its root, without navigation: AggregateName never loads its children. The foreign key is indexed, and
        // the database deletes the children with their root.
        builder.Property(entity => entity.AggregateNameId).HasConversion<AggregateNameIdConverter>();
        builder.HasOne<AggregateName>()
            .WithMany()
            .HasForeignKey(entity => entity.AggregateNameId)
            .OnDelete(DeleteBehavior.Cascade);
#else
        // Held in the collection EntityCollection of AggregateName, loaded and saved with it: until AggregateName declares it, EF Core
        // cannot build the model. The identifier of the root is a shadow property: the child knows nothing of its root. A child
        // removed from the collection is deleted, and so are the children of a deleted root.
        builder.Property<AggregateNameId>("AggregateNameId").HasConversion<AggregateNameIdConverter>();
        builder.HasOne<AggregateName>()
            .WithMany("EntityCollection")
            .HasForeignKey("AggregateNameId")
            .OnDelete(DeleteBehavior.Cascade);
#endif
    }
}
