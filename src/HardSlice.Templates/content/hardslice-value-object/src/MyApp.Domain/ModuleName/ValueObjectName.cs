namespace MyApp.Domain.ModuleName;

// A value object of module ModuleName: it has no identity, equals any other ValueObjectName that holds the same values, and never
// changes once built. Its constructor checks its rules, so that every ValueObjectName holds them, and a rule computed from its
// values, such as a rate or an order, is one of its properties or methods. An aggregate or a child entity holds it as a property
// and replaces it whole to change it.
public sealed record ValueObjectName
{
    public ValueObjectName(decimal value)
    {
        // TODO(hardslice): replace Value with the values of ValueObjectName, and this rule with its own: throw a DomainException
        // for each rule that the values break.
        if (value < 0)
            throw new DomainException("ValueObjectName cannot be negative.");

        Value = value;
    }

    public decimal Value { get; }
}
