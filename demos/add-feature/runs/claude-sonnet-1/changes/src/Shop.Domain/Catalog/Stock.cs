namespace Shop.Domain.Catalog;

// The root of the Stock aggregate: its state changes only through its methods, which check its rules (HS007).
public sealed class Stock : IAggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = [];

    private Stock(StockId id, ProductId productId, int quantity)
    {
        Id = id;
        ProductId = productId;
        Quantity = quantity;
    }

    // Rehydration only: persistence restores the state of the stored aggregate. Business code calls Open.
    private Stock()
    {
    }

    public StockId Id { get; }

    // The product is held by its identifier only (HS008).
    public ProductId ProductId { get; }

    // Never negative: a stock opens at zero.
    public int Quantity { get; private set; }

    // Events raised and not yet published. Not persisted: a reloaded aggregate has none.
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    // Opens the stock of a product at a quantity of zero.
    public static Stock Open(ProductId productId) => new(StockId.New(), productId, 0);

    public IReadOnlyList<IDomainEvent> DequeueDomainEvents()
    {
        var domainEvents = _domainEvents.ToArray();
        _domainEvents.Clear();
        return domainEvents;
    }
}
