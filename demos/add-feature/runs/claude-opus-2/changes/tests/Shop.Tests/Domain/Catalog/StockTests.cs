using Shop.Domain;
using Shop.Domain.Catalog;

namespace Shop.Tests.Domain.Catalog;

public sealed class StockTests
{
    [Fact]
    public void Open_gives_the_new_Stock_its_product_and_a_quantity_of_zero()
    {
        var productId = ProductId.New();

        var aggregate = Stock.Open(productId);

        aggregate.Id.Value.ShouldNotBe(Guid.Empty);
        aggregate.ProductId.ShouldBe(productId);
        aggregate.Quantity.ShouldBe(0);
    }

    [Fact]
    public void Open_refuses_an_empty_product_identifier()
    {
        Should.Throw<DomainException>(() => Stock.Open(new ProductId(Guid.Empty)));
    }

    [Fact]
    public void Open_gives_each_Stock_its_own_identifier()
    {
        Stock.Open(ProductId.New()).Id.ShouldNotBe(Stock.Open(ProductId.New()).Id);
    }

    [Fact]
    public void DequeueDomainEvents_returns_the_raised_events_once_and_empties_the_list()
    {
        var aggregate = Stock.Open(ProductId.New());
        var raised = aggregate.DomainEvents.ToArray();

        aggregate.DequeueDomainEvents().ShouldBe(raised);
        aggregate.DomainEvents.ShouldBeEmpty();
        aggregate.DequeueDomainEvents().ShouldBeEmpty();
    }
}
