using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shop.Domain.Catalog;
using Shop.Domain.Catalog.Events;
using Shop.Features.Catalog.OpenStockOnProductRegistered;
using Shop.Features.Catalog.RegisterProduct;
using Shop.Shared.Messaging;
using Shop.Shared.Persistence;

namespace Shop.IntegrationTests.Features.Catalog.OpenStockOnProductRegistered;

// The pipeline calls OpenStockOnProductRegisteredHandler with the ProductRegistered that Product.Register raised during RegisterProduct,
// then saves the command. The first test does the same with an event it builds; the second sends the command through its endpoint.
public sealed class OpenStockOnProductRegisteredTests(PostgresDatabase database)
{
    [Fact]
    public async Task Opens_the_stock_of_the_product_at_zero()
    {
        await using var app = new ShopFactory(database);
        await using var scope = app.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetServices<IDomainEventHandler<ProductRegistered>>().OfType<OpenStockOnProductRegisteredHandler>().SingleOrDefault();
        handler.ShouldNotBeNull("CatalogModule does not register OpenStockOnProductRegisteredHandler as IDomainEventHandler<ProductRegistered>.");
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var domainEvent = new ProductRegistered(ProductId.New());

        await handler.Handle(domainEvent, TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var stock = await ReadStock(domainEvent.ProductId);
        stock.ShouldNotBeNull();
        stock.Quantity.ShouldBe(0);
    }

    [Fact]
    public async Task Opens_the_stock_when_RegisterProduct_registers_a_product()
    {
        await using var app = new ShopFactory(database);

        var response = await app.CreateClient().PostAsJsonAsync(
            "/catalog/products", new RegisterProductCommand("Coffee mug", 12.50m), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = await response.Content.ReadFromJsonAsync<Guid>(TestContext.Current.CancellationToken);
        var stock = await ReadStock(new ProductId(id));
        stock.ShouldNotBeNull();
        stock.Quantity.ShouldBe(0);
    }

    private async Task<Stock?> ReadStock(ProductId productId)
    {
        await using var db = database.CreateDbContext();
        return await db.Set<Stock>().AsNoTracking()
            .SingleOrDefaultAsync(stock => stock.ProductId == productId, TestContext.Current.CancellationToken);
    }
}
