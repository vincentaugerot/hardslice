namespace Shop.Domain.Catalog;

// The identifier of the aggregate Product: other aggregates hold it, never the Product itself (HS008).
public readonly record struct ProductId(Guid Value)
{
    public static ProductId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
