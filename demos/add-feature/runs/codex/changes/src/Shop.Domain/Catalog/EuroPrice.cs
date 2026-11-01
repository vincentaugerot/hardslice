namespace Shop.Domain.Catalog;

public sealed record EuroPrice
{
    public EuroPrice(decimal value)
    {
        if (value <= 0)
            throw new DomainException("The price in euros must be greater than zero.");

        Value = value;
    }

    public decimal Value { get; }
}
