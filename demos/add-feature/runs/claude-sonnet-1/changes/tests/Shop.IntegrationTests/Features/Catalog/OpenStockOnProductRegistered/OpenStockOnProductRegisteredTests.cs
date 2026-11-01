using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shop.Domain.Catalog;
using Shop.Domain.Catalog.Events;
using Shop.Features.Catalog.OpenStockOnProductRegistered;
using Shop.Shared.Messaging;
using Shop.Shared.Persistence;

namespace Shop.IntegrationTests.Features.Catalog.OpenStockOnProductRegistered;

// The pipeline calls OpenStockOnProductRegisteredHandler with the ProductRegistered that an aggregate raised during a command, then saves the command.
// This test does the same with an event it builds. The RegisterProduct tests also check the stock through the endpoint.
public sealed class OpenStockOnProductRegisteredTests(PostgresDatabase database)
{
    [Fact]
    public async Task Opens_a_stock_of_zero_for_the_registered_product()
    {
        await using var app = new ShopFactory(database);
        await using var scope = app.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetServices<IDomainEventHandler<ProductRegistered>>().OfType<OpenStockOnProductRegisteredHandler>().SingleOrDefault();
        handler.ShouldNotBeNull("CatalogModule does not register OpenStockOnProductRegisteredHandler as IDomainEventHandler<ProductRegistered>.");
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var productId = ProductId.New();

        await handler.Handle(new ProductRegistered(productId), TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var reader = database.CreateDbContext();
        var stock = await reader.Set<Stock>().AsNoTracking().SingleAsync(s => s.ProductId == productId, TestContext.Current.CancellationToken);
        stock.Quantity.ShouldBe(0);
    }
}
