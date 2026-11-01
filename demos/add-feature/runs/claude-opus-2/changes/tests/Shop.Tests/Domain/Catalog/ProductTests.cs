using Shop.Domain;
using Shop.Domain.Catalog;
using Shop.Domain.Catalog.Events;

namespace Shop.Tests.Domain.Catalog;

public sealed class ProductTests
{
    [Fact]
    public void Register_gives_the_new_Product_its_state()
    {
        var aggregate = Product.Register("Coffee mug", 12.50m);

        aggregate.Id.Value.ShouldNotBe(Guid.Empty);
        aggregate.Name.ShouldBe("Coffee mug");
        aggregate.Price.ShouldBe(12.50m);
    }

    [Fact]
    public void Register_records_that_the_product_was_registered()
    {
        var aggregate = Product.Register("Coffee mug", 12.50m);

        aggregate.DomainEvents.ShouldBe([new ProductRegistered(aggregate.Id)]);
    }

    [Fact]
    public void Register_accepts_a_name_of_100_characters()
    {
        var name = new string('a', 100);

        Product.Register(name, 1m).Name.ShouldBe(name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_refuses_a_missing_name(string? name)
    {
        Should.Throw<DomainException>(() => Product.Register(name!, 1m));
    }

    [Fact]
    public void Register_refuses_a_name_longer_than_100_characters()
    {
        Should.Throw<DomainException>(() => Product.Register(new string('a', 101), 1m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(-10)]
    public void Register_refuses_a_price_that_is_not_greater_than_zero(double price)
    {
        Should.Throw<DomainException>(() => Product.Register("Coffee mug", (decimal)price));
    }

    [Fact]
    public void Register_accepts_the_smallest_price_of_one_cent()
    {
        Product.Register("Coffee mug", 0.01m).Price.ShouldBe(0.01m);
    }

    [Fact]
    public void Register_refuses_a_price_with_more_than_2_decimals()
    {
        Should.Throw<DomainException>(() => Product.Register("Coffee mug", 1.005m));
    }

    [Fact]
    public void Register_gives_each_Product_its_own_identifier()
    {
        Product.Register("Coffee mug", 1m).Id.ShouldNotBe(Product.Register("Coffee mug", 1m).Id);
    }

    [Fact]
    public void DequeueDomainEvents_returns_the_raised_events_once_and_empties_the_list()
    {
        var aggregate = Product.Register("Coffee mug", 1m);
        var raised = aggregate.DomainEvents.ToArray();
        raised.ShouldNotBeEmpty();

        aggregate.DequeueDomainEvents().ShouldBe(raised);
        aggregate.DomainEvents.ShouldBeEmpty();
        aggregate.DequeueDomainEvents().ShouldBeEmpty();
    }
}
