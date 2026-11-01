using Shop.Domain.Catalog.Events;

namespace Shop.Domain.Catalog;

// The root of the Product aggregate: its state changes only through its methods, which check its rules (HS007).
public sealed class Product : IAggregateRoot
{
    public const int NameMaxLength = 100;

    private readonly List<IDomainEvent> _domainEvents = [];

    private Product(ProductId id, string name, decimal price)
    {
        Id = id;
        Name = name;
        Price = price;
    }

    // Rehydration only: persistence restores the state of the stored aggregate. Business code calls Register.
    private Product()
    {
        Name = string.Empty;
    }

    public ProductId Id { get; }

    public string Name { get; private set; }

    // The price in euros, always greater than zero.
    public decimal Price { get; private set; }

    // Events raised and not yet published. Not persisted: a reloaded aggregate has none.
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    // Registers a new product in the catalog, once its name and its price hold.
    public static Product Register(string name, decimal price)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("The name of a product is required.");
        }

        if (name.Length > NameMaxLength)
        {
            throw new DomainException($"The name of a product is at most {NameMaxLength} characters.");
        }

        if (price <= 0)
        {
            throw new DomainException("The price of a product must be greater than zero.");
        }

        if (decimal.Round(price, 2) != price)
        {
            throw new DomainException("The price of a product is in euros and cents: at most 2 decimals.");
        }

        var product = new Product(ProductId.New(), name, price);
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
