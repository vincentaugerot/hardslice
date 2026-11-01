using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Shop.Domain.Catalog;
using Shop.Features.Catalog.RegisterProduct;

namespace Shop.IntegrationTests.Features.Catalog.RegisterProduct;

public sealed class RegisterProductTests(PostgresDatabase database)
{
    private const string Route = "/catalog/register-product";

    [Fact]
    public async Task Registers_the_product_and_opens_its_stock_at_zero()
    {
        await using var app = new ShopFactory(database);
        var command = new RegisterProductCommand("Keyboard", 49.90m);

        var response = await app.CreateClient().PostAsJsonAsync(Route, command, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var id = await response.Content.ReadFromJsonAsync<Guid>(TestContext.Current.CancellationToken);
        var productId = new ProductId(id);
        await using var db = database.CreateDbContext();
        var product = await db.Set<Product>().AsNoTracking().SingleAsync(p => p.Id == productId, TestContext.Current.CancellationToken);
        product.Name.ShouldBe("Keyboard");
        product.Price.ShouldBe(49.90m);
        var stock = await db.Set<Stock>().AsNoTracking().SingleAsync(s => s.ProductId == productId, TestContext.Current.CancellationToken);
        stock.Quantity.ShouldBe(0);
    }

    [Fact]
    public async Task Rejects_an_invalid_command_with_a_validation_problem_before_its_handler()
    {
        await using var app = new ShopFactory(database);

        var response = await app.CreateClient().PostAsJsonAsync(Route, new RegisterProductCommand("", 0m), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestContext.Current.CancellationToken);
        problem.ShouldNotBeNull();
        problem.Errors.Keys.Order().ShouldBe(["name", "price"]);
    }
}
