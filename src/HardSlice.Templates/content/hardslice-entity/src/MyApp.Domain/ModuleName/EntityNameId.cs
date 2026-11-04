namespace MyApp.Domain.ModuleName;

// The identifier of the child entity EntityName of the aggregate AggregateName.
public readonly record struct EntityNameId(Guid Value)
{
    public static EntityNameId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
