namespace Shop.Domain.Catalog;

public sealed class Product : IAggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = [];

    private Product(ProductId id)
    {
        Id = id;
    }

    private Product()
    {
    }

    public ProductId Id { get; }
    public string Name { get; private set; } = string.Empty;
    public decimal Price { get; private set; }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public static Product Register(string name, decimal price)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Name is required.");
        if (name.Length > 100)
            throw new DomainException("Name must be at most 100 characters.");
        if (price <= 0)
            throw new DomainException("Price must be greater than zero.");

        var product = new Product(ProductId.New());
        product.Name = name;
        product.Price = price;
        product._domainEvents.Add(new Events.ProductRegistered(product.Id));
        return product;
    }

    public IReadOnlyList<IDomainEvent> DequeueDomainEvents()
    {
        var domainEvents = _domainEvents.ToArray();
        _domainEvents.Clear();
        return domainEvents;
    }
}
