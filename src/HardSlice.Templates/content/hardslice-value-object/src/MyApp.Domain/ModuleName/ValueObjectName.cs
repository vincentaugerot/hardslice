#if (HasOtherModuleField)
FieldUsings

#endif
namespace MyApp.Domain.ModuleName;

// A value object of module ModuleName: it has no identity, equals any other ValueObjectName that holds the same values, and never
// changes once built. Its constructor checks its rules, so that every ValueObjectName holds them, and a rule computed from its
// values, such as a rate or an order, is one of its properties or methods. An aggregate or a child entity holds it as a property
// and replaces it whole to change it.
public sealed record ValueObjectName
{
    public ValueObjectName(FieldParameters)
    {
FieldRules        // TODO(hardslice): add the rules of the values of ValueObjectName that their types do not say, such as a positive amount or an order
        // between two dates, each one throwing a DomainException, with its test, then remove this line.

FieldAssignments    }

FieldProperties
#if (HasListField)

    // A record compares a list by reference: ValueObjectName compares the elements of its lists, in their order, so that two
    // ValueObjectName built from equal lists are equal.
    public bool Equals(ValueObjectName? other) =>
        other is not null
FieldComparisons;

    public override int GetHashCode()
    {
        var hash = new HashCode();
FieldHashes        return hash.ToHashCode();
    }
#endif
}
