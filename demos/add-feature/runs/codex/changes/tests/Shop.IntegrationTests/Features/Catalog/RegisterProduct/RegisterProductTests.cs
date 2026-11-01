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
    public async Task Registers_a_product_and_returns_its_identifier()
    {
        await using var app = new ShopFactory(database);
        var command = new RegisterProductCommand("Desk lamp", 29.95m);

        var response = await app.CreateClient().PostAsJsonAsync(Route, command, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = await response.Content.ReadFromJsonAsync<Guid>(TestContext.Current.CancellationToken);
        id.ShouldNotBe(Guid.Empty);
        response.Headers.Location.ShouldBe(new Uri($"{Route}/{id}", UriKind.Relative));

        await using var db = database.CreateDbContext();
        var product = await db.Set<Product>().AsNoTracking()
            .SingleAsync(product => product.Id == new ProductId(id), TestContext.Current.CancellationToken);
        product.Name.ShouldBe(command.Name);
        product.Price.Value.ShouldBe(command.Price);
        product.DomainEvents.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    public async Task Rejects_a_missing_name_with_a_validation_problem(string? name)
    {
        await using var app = new ShopFactory(database);

        var response = await app.CreateClient().PostAsJsonAsync(Route, new RegisterProductCommand(name!, 1m), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestContext.Current.CancellationToken);
        problem.ShouldNotBeNull();
        problem.Errors.Keys.ShouldBe(["name"]);
    }

    [Fact]
    public async Task Rejects_a_name_longer_than_100_characters_without_storing_a_product()
    {
        await using var app = new ShopFactory(database);
        var name = $"{Guid.NewGuid()}{new string('a', 65)}";

        var response = await app.CreateClient().PostAsJsonAsync(Route, new RegisterProductCommand(name, 1m), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestContext.Current.CancellationToken);
        problem.ShouldNotBeNull();
        problem.Errors.Keys.ShouldBe(["name"]);
        await using var db = database.CreateDbContext();
        (await db.Set<Product>().AnyAsync(product => product.Name == name, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Rejects_a_nonpositive_price_without_storing_a_product(int price)
    {
        await using var app = new ShopFactory(database);
        var name = $"Invalid price {Guid.NewGuid()}";

        var response = await app.CreateClient().PostAsJsonAsync(Route, new RegisterProductCommand(name, price), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        problem.ShouldNotBeNull();
        problem.Detail.ShouldBe("The price in euros must be greater than zero.");
        await using var db = database.CreateDbContext();
        (await db.Set<Product>().AnyAsync(product => product.Name == name, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }
}
