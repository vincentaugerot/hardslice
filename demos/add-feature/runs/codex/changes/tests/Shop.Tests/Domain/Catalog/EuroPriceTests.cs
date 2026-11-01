using Shop.Domain;
using Shop.Domain.Catalog;

namespace Shop.Tests.Domain.Catalog;

public sealed class EuroPriceTests
{
    [Fact]
    public void Preserves_the_decimal_amount_without_rounding()
    {
        new EuroPrice(0.001m).Value.ShouldBe(0.001m);
        new EuroPrice(decimal.MaxValue).Value.ShouldBe(decimal.MaxValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_a_nonpositive_amount(int value)
    {
        Should.Throw<DomainException>(() => new EuroPrice(value));
    }
}
