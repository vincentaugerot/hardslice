namespace Shop.Domain.Catalog;

// The identifier of the aggregate Stock: other aggregates hold it, never the Stock itself (HS008).
public readonly record struct StockId(Guid Value)
{
    public static StockId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
