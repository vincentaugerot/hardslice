using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shop.Domain.Catalog;
using Shop.Domain.Catalog.Events;
using Shop.Features.Catalog.OpenStockOnProductRegistered;
using Shop.Shared.Messaging;
using Shop.Shared.Persistence;

namespace Shop.IntegrationTests.Features.Catalog.OpenStockOnProductRegistered;

public sealed class OpenStockOnProductRegisteredTests(PostgresDatabase database)
{
    [Fact]
    public async Task Reacts_to_ProductRegistered()
    {
        await using var app = new ShopFactory(database);
        await using var scope = app.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetServices<IDomainEventHandler<ProductRegistered>>().OfType<OpenStockOnProductRegisteredHandler>().SingleOrDefault();
        handler.ShouldNotBeNull("CatalogModule does not register OpenStockOnProductRegisteredHandler as IDomainEventHandler<ProductRegistered>.");
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var productId = new ProductId(Guid.NewGuid());
        var domainEvent = new ProductRegistered(productId);

        await handler.Handle(domainEvent, TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var readDb = database.CreateDbContext();
        var stock = await readDb.Set<Stock>().SingleOrDefaultAsync(s => s.ProductId == productId, TestContext.Current.CancellationToken);
        stock.ShouldNotBeNull();
        stock.ProductId.ShouldBe(productId);
        stock.Quantity.ShouldBe(0);
    }
}
