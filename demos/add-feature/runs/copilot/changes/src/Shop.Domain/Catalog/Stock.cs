namespace Shop.Domain.Catalog;

public sealed class Stock : IAggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = [];

    private Stock(StockId id)
    {
        Id = id;
    }

    private Stock()
    {
    }

    public StockId Id { get; }
    public ProductId ProductId { get; private set; }
    public int Quantity { get; private set; }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public static Stock Open(ProductId productId)
    {
        var stock = new Stock(StockId.New());
        stock.ProductId = productId;
        stock.Quantity = 0;
        return stock;
    }

    public IReadOnlyList<IDomainEvent> DequeueDomainEvents()
    {
        var domainEvents = _domainEvents.ToArray();
        _domainEvents.Clear();
        return domainEvents;
    }
}
