using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Shop.Domain.Catalog;
using Shop.Features.Catalog.RegisterProduct;

namespace Shop.IntegrationTests.Features.Catalog.RegisterProduct;

public sealed class RegisterProductTests(PostgresDatabase database)
{
    private const string Route = "/catalog/register-product";

    [Fact]
    public async Task Handles_a_valid_command()
    {
        await using var app = new ShopFactory(database);
        var command = new RegisterProductCommand("Coffee Mug", 12.50m);

        var response = await app.CreateClient().PostAsJsonAsync(Route, command, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var id = await response.Content.ReadFromJsonAsync<Guid>(TestContext.Current.CancellationToken);
        id.ShouldNotBe(Guid.Empty);
        await using var db = database.CreateDbContext();
        var product = await db.Set<Product>().SingleOrDefaultAsync(p => p.Id == new ProductId(id), TestContext.Current.CancellationToken);
        product.ShouldNotBeNull();
        product.Name.ShouldBe("Coffee Mug");
        product.Price.ShouldBe(12.50m);
    }

    [Fact]
    public async Task Rejects_an_invalid_command_with_a_validation_problem_before_its_handler()
    {
        await using var app = new ShopFactory(database);

        var response = await app.CreateClient().PostAsJsonAsync(Route, new RegisterProductCommand("", 0m), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Http.HttpValidationProblemDetails>(TestContext.Current.CancellationToken);
        problem.ShouldNotBeNull();
        problem.Errors.Keys.ShouldContain("name");
        problem.Errors.Keys.ShouldContain("price");
    }
}
