using Shop.Domain;
using Shop.Domain.Catalog;

namespace Shop.Tests.Domain.Catalog;

public sealed class StockTests
{
    [Fact]
    public void Open_gives_the_stock_its_product_identifier_and_zero_quantity()
    {
        var productId = ProductId.New();
        var aggregate = Stock.Open(productId);

        aggregate.Id.Value.ShouldNotBe(Guid.Empty);
        aggregate.ProductId.ShouldBe(productId);
        aggregate.Quantity.ShouldBe(0);
    }

    [Fact]
    public void Open_gives_each_stock_its_own_identifier()
    {
        Stock.Open(ProductId.New()).Id.ShouldNotBe(Stock.Open(ProductId.New()).Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(int.MaxValue)]
    public void Open_accepts_a_nonnegative_quantity(int quantity)
    {
        Stock.Open(ProductId.New(), quantity).Quantity.ShouldBe(quantity);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Open_rejects_a_negative_quantity(int quantity)
    {
        Should.Throw<DomainException>(() => Stock.Open(ProductId.New(), quantity));
    }

    [Fact]
    public void Open_rejects_an_empty_product_identifier()
    {
        Should.Throw<DomainException>(() => Stock.Open(new ProductId(Guid.Empty)));
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
