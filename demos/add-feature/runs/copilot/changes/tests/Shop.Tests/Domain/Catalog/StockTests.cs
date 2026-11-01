using Shop.Domain.Catalog;

namespace Shop.Tests.Domain.Catalog;

public sealed class StockTests
{
    [Fact]
    public void Open_gives_the_new_Stock_its_state()
    {
        var productId = new ProductId(Guid.NewGuid());
        var stock = Stock.Open(productId);

        stock.Id.Value.ShouldNotBe(Guid.Empty);
        stock.ProductId.ShouldBe(productId);
        stock.Quantity.ShouldBe(0);
    }

    [Fact]
    public void Open_gives_each_Stock_its_own_identifier()
    {
        var productId = new ProductId(Guid.NewGuid());
        Stock.Open(productId).Id.ShouldNotBe(Stock.Open(productId).Id);
    }

    [Fact]
    public void DequeueDomainEvents_returns_the_raised_events_once_and_empties_the_list()
    {
        var aggregate = Stock.Open(new ProductId(Guid.NewGuid()));
        var raised = aggregate.DomainEvents.ToArray();

        aggregate.DequeueDomainEvents().ShouldBe(raised);
        aggregate.DomainEvents.ShouldBeEmpty();
        aggregate.DequeueDomainEvents().ShouldBeEmpty();
    }
}
