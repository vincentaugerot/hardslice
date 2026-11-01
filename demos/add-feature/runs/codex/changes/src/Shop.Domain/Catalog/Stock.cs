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
    public ProductId ProductId { get; }
    public int Quantity { get; }

    // Events raised and not yet published. Not persisted: a reloaded aggregate has none.
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public static Stock Open(ProductId productId, int quantity = 0)
    {
        if (productId.Value == Guid.Empty)
            throw new DomainException("A product identifier is required to open stock.");
        if (quantity < 0)
            throw new DomainException("The stock quantity must never be negative.");

        return new Stock(StockId.New(), productId, quantity);
    }

    public IReadOnlyList<IDomainEvent> DequeueDomainEvents()
    {
        var domainEvents = _domainEvents.ToArray();
        _domainEvents.Clear();
        return domainEvents;
    }
}
