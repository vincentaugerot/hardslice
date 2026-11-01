namespace Shop.Domain.Catalog;

// The root of the Stock aggregate: its state changes only through its methods, which check its rules (HS007).
// It holds its product by its identifier only (HS008), and its quantity is never negative.
public sealed class Stock : IAggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = [];

    private Stock(StockId id, ProductId productId)
    {
        Id = id;
        ProductId = productId;
        Quantity = 0;
    }

    // Rehydration only: persistence restores the state of the stored aggregate. Business code calls Open.
    private Stock()
    {
    }

    public StockId Id { get; }

    public ProductId ProductId { get; }

    // Never negative.
    public int Quantity { get; private set; }

    // Events raised and not yet published. Not persisted: a reloaded aggregate has none.
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    // Opens the stock of a product, at a quantity of zero.
    public static Stock Open(ProductId productId)
    {
        if (productId.Value == Guid.Empty)
        {
            throw new DomainException("A stock needs the identifier of its product.");
        }

        return new Stock(StockId.New(), productId);
    }

    public IReadOnlyList<IDomainEvent> DequeueDomainEvents()
    {
        var domainEvents = _domainEvents.ToArray();
        _domainEvents.Clear();
        return domainEvents;
    }
}
