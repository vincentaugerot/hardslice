using System.Net;
using System.Net.Http.Json;
using Shop.Domain.Catalog;
using Shop.Features.Catalog.GetProduct;

namespace Shop.IntegrationTests.Features.Catalog.GetProduct;

public sealed class GetProductTests(PostgresDatabase database)
{
    private const string Route = "/catalog/products";

    public static TheoryData<decimal> Prices => new() { 29.95m, 0.001m, decimal.MaxValue };

    [Theory]
    [MemberData(nameof(Prices))]
    public async Task Reads_the_name_and_exact_euro_price_of_a_stored_product(decimal price)
    {
        var name = new string('a', 100);
        var id = await StoreProduct(name, price);
        await using var app = new ShopFactory(database);

        var response = await app.CreateClient().GetAsync($"{Route}/{id}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<GetProductDto>(TestContext.Current.CancellationToken);
        dto.ShouldNotBeNull();
        dto.Id.ShouldBe(id);
        dto.Name.ShouldBe(name);
        dto.Price.ShouldBe(price);
    }

    [Fact]
    public async Task Returns_404_for_an_unknown_aggregate()
    {
        await using var app = new ShopFactory(database);

        var response = await app.CreateClient().GetAsync($"{Route}/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Reads_a_registered_product_at_the_location_returned_by_the_API()
    {
        await using var app = new ShopFactory(database);
        var client = app.CreateClient();
        var registration = await client.PostAsJsonAsync(Route, new { Name = "Desk lamp", Price = 29.95m }, TestContext.Current.CancellationToken);
        registration.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = await registration.Content.ReadFromJsonAsync<Guid>(TestContext.Current.CancellationToken);
        registration.Headers.Location.ShouldNotBeNull();

        var response = await client.GetAsync(registration.Headers.Location, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<GetProductDto>(TestContext.Current.CancellationToken);
        dto.ShouldBe(new GetProductDto(id, "Desk lamp", 29.95m));
    }

    [Fact]
    public async Task Projects_values_without_tracking_an_aggregate()
    {
        var id = await StoreProduct("Desk lamp", 29.95m);
        await using var db = database.CreateDbContext();
        var handler = new GetProductHandler(db);

        var dto = await handler.Handle(new GetProductQuery(id), TestContext.Current.CancellationToken);

        dto.ShouldBe(new GetProductDto(id, "Desk lamp", 29.95m));
        db.ChangeTracker.Entries().ShouldBeEmpty();
    }

    // Stores, outside the application, the Product that the query reads, and returns its identifier.
    private async Task<Guid> StoreProduct(string name, decimal price)
    {
        await using var db = database.CreateDbContext();
        var product = Product.Register(name, price);
        db.Set<Product>().Add(product);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return product.Id.Value;
    }
}
