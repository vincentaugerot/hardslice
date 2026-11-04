using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using MyApp.Domain;

namespace MyApp.Shared.Persistence;

// Declares no module type: each module maps its aggregates in its own IEntityTypeConfiguration,
// and slices reach them through Set<T>(). Adding a module never changes Shared.
internal sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        SaveRootsOfChangedChildren();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        SaveRootsOfChangedChildren();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

    // A child entity held in a collection of its root has a table of its own: a command that adds or changes only children
    // leaves the row of the root as it is, and EF Core then checks no concurrency token of the root. Each tracked root with a
    // concurrency token, such as xmin, whose children changed is saved too: two commands that change the same aggregate, through
    // its root or its children, never both commit, and the second answers 409. The children that hold the identifier of their
    // root, without a collection of the root, are written apart from it and leave it as it is.
    private void SaveRootsOfChangedChildren()
    {
        // Entries() detects the changes first, as SaveChanges does.
        var entries = ChangeTracker.Entries().ToList();
        var owners = entries
            .Where(child => child.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .SelectMany(child => child.Metadata.GetForeignKeys()
                .Where(foreignKey => foreignKey.PrincipalToDependent is not null &&
                    foreignKey.PrincipalEntityType.ClrType.IsAssignableTo(typeof(IAggregateRoot)))
                .Select(foreignKey => (ForeignKey: foreignKey, Values: ValuesOf(child, foreignKey.Properties, child.State == EntityState.Deleted))))
            .ToList();
        if (owners.Count == 0)
            return;

        foreach (var root in entries.Where(root => root is { State: EntityState.Unchanged, Entity: IAggregateRoot } &&
                     root.Metadata.GetProperties().Any(property => property.IsConcurrencyToken)))
        {
            if (!owners.Any(owner => owner.ForeignKey.PrincipalEntityType == root.Metadata &&
                    ValuesOf(root, owner.ForeignKey.PrincipalKey.Properties, original: false).SequenceEqual(owner.Values)))
                continue;

            // EF Core sends no UPDATE for a row without a column to write, and would check nothing.
            if (!root.Metadata.GetFlattenedProperties().Any(property => !property.IsKey() && property.GetAfterSaveBehavior() == PropertySaveBehavior.Save))
            {
                throw new InvalidOperationException(
                    $"The children of the aggregate {root.Metadata.ClrType.Name} changed, but its mapping has no column besides its key and its concurrency token: EF Core cannot update its row to check that no other request changed the aggregate. Map a state of the root in its configuration, as the add-aggregate skill says.");
            }

            root.State = EntityState.Modified;
        }
    }

    private static object?[] ValuesOf(EntityEntry entry, IEnumerable<IProperty> properties, bool original) =>
        [.. properties.Select(property => original ? entry.OriginalValues[property] : entry.CurrentValues[property])];
}
