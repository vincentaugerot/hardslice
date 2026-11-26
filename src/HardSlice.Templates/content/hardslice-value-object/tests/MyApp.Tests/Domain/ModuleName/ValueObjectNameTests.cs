using MyApp.Domain;
using MyApp.Domain.ModuleName;
#if (HasOtherModuleField)
FieldUsings
#endif

namespace MyApp.Tests.Domain.ModuleName;

public sealed class ValueObjectNameTests
{
    [Fact]
    public void Holds_the_values_it_is_built_with_and_equals_another_built_with_the_same()
    {
        var value = Valid();

FieldAssertions        value.ShouldBe(Valid());
    }

FieldRefusalTests    // A valid ValueObjectName, built anew at each call from the valid value of each field below.
    private static ValueObjectName Valid() => Create(FieldValidNames);

    private static ValueObjectName Create(FieldParameters) =>
        new(FieldArguments);

FieldSamples}
