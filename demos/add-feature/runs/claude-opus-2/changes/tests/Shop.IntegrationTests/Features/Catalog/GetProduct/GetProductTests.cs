using System.Net;
using System.Net.Http.Json;
using Shop.Domain.Catalog;
using Shop.Features.Catalog.GetProduct;

namespace Shop.IntegrationTests.Features.Catalog.GetProduct;

public sealed class GetProductTests(PostgresDatabase database)
{
    private const string Route = "/catalog/products";

    [Fact]
    public async Task Reads_a_stored_aggregate_as_its_dto()
    {
        var id = await StoreProduct();
        await using var app = new ShopFactory(database);

        var response = await app.CreateClient().GetAsync($"{Route}/{id}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<GetProductDto>(TestContext.Current.CancellationToken);
        dto.ShouldNotBeNull();
        dto.Id.ShouldBe(id);
        dto.Name.ShouldBe("Coffee mug");
        dto.Price.ShouldBe(12.50m);
    }

    [Fact]
    public async Task Returns_404_for_an_unknown_aggregate()
    {
        await using var app = new ShopFactory(database);

        var response = await app.CreateClient().GetAsync($"{Route}/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // Stores, outside the application, the Product that the query reads, and returns its identifier.
    private async Task<Guid> StoreProduct()
    {
        await using var db = database.CreateDbContext();
        var product = Product.Register("Coffee mug", 12.50m);
        db.Set<Product>().Add(product);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return product.Id.Value;
    }
}
