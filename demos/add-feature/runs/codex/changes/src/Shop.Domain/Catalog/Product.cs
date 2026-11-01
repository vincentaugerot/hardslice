using Shop.Domain.Catalog.Events;

namespace Shop.Domain.Catalog;

// The root of the Product aggregate: its state changes only through its methods, which check its rules (HS007).
public sealed class Product : IAggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = [];

    private Product(ProductId id, string name, EuroPrice price)
    {
        Id = id;
        Name = name;
        Price = price;
    }

    // Rehydration only: persistence restores the state of the stored aggregate. Business code calls Register.
    private Product()
    {
    }

    public ProductId Id { get; }
    public string Name { get; } = null!;
    public EuroPrice Price { get; } = null!;

    // Events raised and not yet published. Not persisted: a reloaded aggregate has none.
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public static Product Register(string name, decimal price)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("A product name is required.");
        if (name.Length > 100)
            throw new DomainException("A product name must be at most 100 characters.");

        var product = new Product(ProductId.New(), name, new EuroPrice(price));
        product._domainEvents.Add(new ProductRegistered(product.Id));
        return product;
    }

    public IReadOnlyList<IDomainEvent> DequeueDomainEvents()
    {
        var domainEvents = _domainEvents.ToArray();
        _domainEvents.Clear();
        return domainEvents;
    }
}
