namespace MyApp.Domain.ModuleName;

// The identifier of the aggregate AggregateName: other aggregates hold it, never the AggregateName itself (HS008).
public readonly record struct AggregateNameId(Guid Value)
{
    public static AggregateNameId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
