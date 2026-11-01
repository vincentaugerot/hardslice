using Shop.Domain;
using Shop.Domain.Catalog;
using Shop.Domain.Catalog.Events;

namespace Shop.Tests.Domain.Catalog;

public sealed class ProductTests
{
    [Fact]
    public void Register_gives_the_new_Product_its_state()
    {
        var aggregate = Product.Register("Keyboard", 49.90m);

        aggregate.Id.Value.ShouldNotBe(Guid.Empty);
        aggregate.Name.ShouldBe("Keyboard");
        aggregate.Price.ShouldBe(49.90m);
    }

    [Fact]
    public void Register_accepts_a_name_of_100_characters()
    {
        Product.Register(new string('a', 100), 1m).Name.Length.ShouldBe(100);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Register_refuses_a_missing_name(string? name)
    {
        Should.Throw<DomainException>(() => Product.Register(name!, 1m));
    }

    [Fact]
    public void Register_refuses_a_name_over_100_characters()
    {
        Should.Throw<DomainException>(() => Product.Register(new string('a', 101), 1m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Register_refuses_a_price_that_is_not_greater_than_zero(int price)
    {
        Should.Throw<DomainException>(() => Product.Register("Keyboard", price));
    }

    [Fact]
    public void Register_records_ProductRegistered_with_the_product_identifier()
    {
        var aggregate = Product.Register("Keyboard", 10m);

        aggregate.DomainEvents.ShouldBe([new ProductRegistered(aggregate.Id)]);
    }

    [Fact]
    public void Register_gives_each_Product_its_own_identifier()
    {
        Product.Register("A", 1m).Id.ShouldNotBe(Product.Register("A", 1m).Id);
    }

    [Fact]
    public void DequeueDomainEvents_returns_the_raised_events_once_and_empties_the_list()
    {
        var aggregate = Product.Register("Keyboard", 10m);
        var raised = aggregate.DomainEvents.ToArray();

        aggregate.DequeueDomainEvents().ShouldBe(raised);
        aggregate.DomainEvents.ShouldBeEmpty();
        aggregate.DequeueDomainEvents().ShouldBeEmpty();
    }
}
