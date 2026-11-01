using Shop.Domain;
using Shop.Domain.Catalog;
using Shop.Domain.Catalog.Events;

namespace Shop.Tests.Domain.Catalog;

public sealed class ProductTests
{
    [Fact]
    public void Register_gives_the_new_product_its_state()
    {
        var aggregate = Product.Register("Desk lamp", 29.95m);

        aggregate.Id.Value.ShouldNotBe(Guid.Empty);
        aggregate.Name.ShouldBe("Desk lamp");
        aggregate.Price.Value.ShouldBe(29.95m);
        aggregate.DomainEvents.ShouldHaveSingleItem()
            .ShouldBeOfType<ProductRegistered>().ProductId.ShouldBe(aggregate.Id);
    }

    [Fact]
    public void Register_gives_each_product_its_own_identifier()
    {
        Product.Register("Desk lamp", 29.95m).Id.ShouldNotBe(Product.Register("Desk lamp", 29.95m).Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    public void Register_rejects_a_missing_name(string? name)
    {
        Should.Throw<DomainException>(() => Product.Register(name!, 1m));
    }

    [Fact]
    public void Register_rejects_a_name_longer_than_100_characters()
    {
        Should.Throw<DomainException>(() => Product.Register(new string('a', 101), 1m));
    }

    [Fact]
    public void Register_accepts_a_name_of_100_characters_and_a_small_positive_price()
    {
        var product = Product.Register(new string('a', 100), 0.001m);

        product.Name.Length.ShouldBe(100);
        product.Price.Value.ShouldBe(0.001m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Register_rejects_a_nonpositive_price(int price)
    {
        Should.Throw<DomainException>(() => Product.Register("Desk lamp", price));
    }

    [Fact]
    public void DequeueDomainEvents_returns_the_raised_events_once_and_empties_the_list()
    {
        var aggregate = Product.Register("Desk lamp", 29.95m);
        var raised = aggregate.DomainEvents.ToArray();

        aggregate.DequeueDomainEvents().ShouldBe(raised);
        aggregate.DomainEvents.ShouldBeEmpty();
        aggregate.DequeueDomainEvents().ShouldBeEmpty();
    }
}
