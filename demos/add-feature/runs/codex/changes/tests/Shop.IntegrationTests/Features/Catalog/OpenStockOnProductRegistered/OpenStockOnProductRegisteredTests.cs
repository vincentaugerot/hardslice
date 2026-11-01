using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shop.Domain;
using Shop.Domain.Catalog;
using Shop.Domain.Catalog.Events;
using Shop.Features.Catalog.OpenStockOnProductRegistered;
using Shop.Shared.Messaging;
using Shop.Shared.Persistence;

namespace Shop.IntegrationTests.Features.Catalog.OpenStockOnProductRegistered;

public sealed class OpenStockOnProductRegisteredTests(PostgresDatabase database)
{
    [Fact]
    public async Task Opens_stock_for_the_registered_product_and_leaves_saving_to_the_pipeline()
    {
        var product = Product.Register("Desk lamp", 29.95m);
        await using (var seed = database.CreateDbContext())
        {
            seed.Set<Product>().Add(product);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        await using var app = new ShopFactory(database);
        await using var scope = app.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetServices<IDomainEventHandler<ProductRegistered>>().OfType<OpenStockOnProductRegisteredHandler>().SingleOrDefault();
        handler.ShouldNotBeNull("CatalogModule does not register OpenStockOnProductRegisteredHandler as IDomainEventHandler<ProductRegistered>.");
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var domainEvent = product.DequeueDomainEvents().ShouldHaveSingleItem().ShouldBeOfType<ProductRegistered>();

        await handler.Handle(domainEvent, TestContext.Current.CancellationToken);

        var pendingStock = db.ChangeTracker.Entries<Stock>().ShouldHaveSingleItem();
        pendingStock.State.ShouldBe(EntityState.Added);
        pendingStock.Entity.ProductId.ShouldBe(product.Id);
        pendingStock.Entity.Quantity.ShouldBe(0);
        await using (var beforeSave = database.CreateDbContext())
        {
            (await beforeSave.Set<Stock>().AnyAsync(stock => stock.ProductId == product.Id, TestContext.Current.CancellationToken))
                .ShouldBeFalse();
        }
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var verification = database.CreateDbContext();
        var stored = await verification.Set<Stock>().AsNoTracking()
            .SingleAsync(stock => stock.ProductId == product.Id, TestContext.Current.CancellationToken);
        stored.Id.Value.ShouldNotBe(Guid.Empty);
        stored.Quantity.ShouldBe(0);
    }

    [Fact]
    public async Task Registration_publishes_the_event_and_commits_zero_stock_for_its_product()
    {
        await using var app = new ShopFactory(database);

        var response = await app.CreateClient().PostAsJsonAsync("/catalog/products",
            new { Name = "Desk lamp", Price = 29.95m }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var productId = new ProductId(await response.Content.ReadFromJsonAsync<Guid>(TestContext.Current.CancellationToken));
        await using var db = database.CreateDbContext();
        (await db.Set<Product>().AnyAsync(product => product.Id == productId, TestContext.Current.CancellationToken)).ShouldBeTrue();
        var stock = await db.Set<Stock>().AsNoTracking()
            .SingleAsync(stock => stock.ProductId == productId, TestContext.Current.CancellationToken);
        stock.Id.Value.ShouldNotBe(Guid.Empty);
        stock.Quantity.ShouldBe(0);
    }

    [Fact]
    public async Task A_failed_reaction_rolls_back_both_product_and_stock()
    {
        var failure = new FailingReaction();
        await using var factory = new ShopFactory(database);
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IDomainEventHandler<ProductRegistered>>(failure)));

        var response = await app.CreateClient().PostAsJsonAsync("/catalog/products",
            new { Name = "Desk lamp", Price = 29.95m }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        failure.ProductId.ShouldNotBeNull();
        var productId = failure.ProductId.Value;
        await using var db = database.CreateDbContext();
        (await db.Set<Product>().AnyAsync(product => product.Id == productId, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await db.Set<Stock>().AnyAsync(stock => stock.ProductId == productId, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    private sealed class FailingReaction : IDomainEventHandler<ProductRegistered>
    {
        public ProductId? ProductId { get; private set; }

        public Task Handle(ProductRegistered domainEvent, CancellationToken cancellationToken)
        {
            ProductId = domainEvent.ProductId;
            throw new DomainException("Registration rejected by a reaction.");
        }
    }
}
