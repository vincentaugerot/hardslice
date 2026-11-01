using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shop.Domain.Catalog;
using Shop.Features.Catalog.RegisterProduct;

namespace Shop.IntegrationTests.Features.Catalog.RegisterProduct;

public sealed class RegisterProductTests(PostgresDatabase database)
{
    private const string Route = "/catalog/products";

    [Fact]
    public async Task Registers_the_product_and_answers_its_identifier()
    {
        await using var app = new ShopFactory(database);
        var command = new RegisterProductCommand("Coffee mug", 12.50m);

        var response = await app.CreateClient().PostAsJsonAsync(Route, command, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = await response.Content.ReadFromJsonAsync<Guid>(TestContext.Current.CancellationToken);
        id.ShouldNotBe(Guid.Empty);
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location.OriginalString.ShouldBe($"{Route}/{id}");

        await using var db = database.CreateDbContext();
        var productId = new ProductId(id);
        var stored = await db.Set<Product>().AsNoTracking()
            .SingleAsync(product => product.Id == productId, TestContext.Current.CancellationToken);
        stored.Name.ShouldBe("Coffee mug");
        stored.Price.ShouldBe(12.50m);
    }

    [Fact]
    public async Task Rejects_an_invalid_command_with_a_validation_problem_before_its_handler()
    {
        await using var app = new ShopFactory(database);

        var response = await app.CreateClient().PostAsJsonAsync(Route, new RegisterProductCommand(new string('a', 101), 12.50m), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestContext.Current.CancellationToken);
        problem.ShouldNotBeNull();
        problem.Errors.Keys.ShouldBe(["name"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Rejects_a_price_that_is_not_greater_than_zero_and_stores_nothing(int price)
    {
        await using var app = new ShopFactory(database);
        var name = $"Refused {Guid.NewGuid()}";

        var response = await app.CreateClient().PostAsJsonAsync(Route, new RegisterProductCommand(name, price), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        problem.ShouldNotBeNull();
        problem.Detail.ShouldBe("The price of a product must be greater than zero.");

        await using var db = database.CreateDbContext();
        (await db.Set<Product>().AnyAsync(product => product.Name == name, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }
}
