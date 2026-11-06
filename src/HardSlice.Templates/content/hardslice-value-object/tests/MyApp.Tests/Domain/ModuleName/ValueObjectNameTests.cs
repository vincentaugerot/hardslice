using MyApp.Domain;
using MyApp.Domain.ModuleName;

namespace MyApp.Tests.Domain.ModuleName;

public sealed class ValueObjectNameTests
{
    [Fact]
    public void Holds_the_values_it_is_built_with_and_equals_another_built_with_the_same()
    {
        var value = new ValueObjectName(12.5m);

        value.Value.ShouldBe(12.5m);
        value.ShouldBe(new ValueObjectName(12.5m));
        value.ShouldNotBe(new ValueObjectName(13m));
    }

    [Fact]
    public void Refuses_values_that_break_its_rule()
    {
        Should.Throw<DomainException>(() => new ValueObjectName(-1m));
    }
}
