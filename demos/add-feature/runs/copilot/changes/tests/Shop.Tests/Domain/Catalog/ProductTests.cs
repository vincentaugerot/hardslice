using Shop.Domain;
using Shop.Domain.Catalog;
using Shop.Domain.Catalog.Events;

namespace Shop.Tests.Domain.Catalog;

public sealed class ProductTests
{
    [Fact]
    public void Register_gives_the_new_Product_its_state()
    {
        var product = Product.Register("Coffee Mug", 12.50m);

        product.Id.Value.ShouldNotBe(Guid.Empty);
        product.Name.ShouldBe("Coffee Mug");
        product.Price.ShouldBe(12.50m);
    }

    [Fact]
    public void Register_raises_ProductRegistered()
    {
        var product = Product.Register("Coffee Mug", 12.50m);

        var events = product.DomainEvents;
        events.Count.ShouldBe(1);
        var raised = events[0].ShouldBeOfType<ProductRegistered>();
        raised.ProductId.ShouldBe(product.Id);
    }

    [Fact]
    public void Register_rejects_an_empty_name()
    {
        Should.Throw<DomainException>(() => Product.Register("", 12.50m));
    }

    [Fact]
    public void Register_rejects_a_name_longer_than_100_characters()
    {
        Should.Throw<DomainException>(() => Product.Register(new string('a', 101), 12.50m));
    }

    [Fact]
    public void Register_rejects_a_price_of_zero()
    {
        Should.Throw<DomainException>(() => Product.Register("Coffee Mug", 0m));
    }

    [Fact]
    public void Register_rejects_a_negative_price()
    {
        Should.Throw<DomainException>(() => Product.Register("Coffee Mug", -1m));
    }

    [Fact]
    public void Register_gives_each_Product_its_own_identifier()
    {
        Product.Register("A", 1m).Id.ShouldNotBe(Product.Register("B", 2m).Id);
    }

    [Fact]
    public void DequeueDomainEvents_returns_the_raised_events_once_and_empties_the_list()
    {
        var aggregate = Product.Register("Coffee Mug", 12.50m);
        var raised = aggregate.DomainEvents.ToArray();

        aggregate.DequeueDomainEvents().ShouldBe(raised);
        aggregate.DomainEvents.ShouldBeEmpty();
        aggregate.DequeueDomainEvents().ShouldBeEmpty();
    }
}
