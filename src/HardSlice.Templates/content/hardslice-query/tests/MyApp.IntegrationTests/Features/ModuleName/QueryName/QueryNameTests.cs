using System.Net;
using System.Net.Http.Json;
#if (ResultIsFiltered)
using Microsoft.AspNetCore.Http;
#endif
using MyApp.Domain.ModuleName;
using MyApp.Features.ModuleName;
using MyApp.Features.ModuleName.QueryName;

namespace MyApp.IntegrationTests.Features.ModuleName.QueryName;

public sealed class QueryNameTests(PostgresDatabase database)
{
#if (RouteHasParameters)
    // TODO(hardslice): replace each route parameter between braces in Route with a value of the test, and bind it in the endpoint of QueryName, such as a parameter of its delegate.
#endif
#if (RouteIsGroup)
    private const string Route = ModuleNameModule.Route;
#else
    private const string Route = ModuleNameModule.Route + "/query-route";
#endif

#if (ResultIsSingle)
    [Fact]
    public async Task Reads_a_stored_aggregate_as_its_dto()
    {
        var id = await StoreAggregateName();
        await using var app = new AppFactory(database);

        var response = await app.CreateClient().GetAsync($"{Route}/{id}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ResultDtoName>(TestContext.Current.CancellationToken);
        dto.ShouldNotBeNull();
        dto.Id.ShouldBe(id);
        Assert.Fail("TODO(hardslice): assert the values of ResultDtoName that the use case reads from the stored AggregateName.");
    }

    [Fact]
    public async Task Returns_404_for_an_unknown_aggregate()
    {
        await using var app = new AppFactory(database);

        var response = await app.CreateClient().GetAsync($"{Route}/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
#elif (ResultIsFiltered)
    [Fact]
    public async Task Lists_the_stored_aggregates_that_its_filter_selects()
    {
        var id = await StoreAggregateName();
        await using var app = new AppFactory(database);

        var response = await app.CreateClient().GetAsync($"{Route}?filterName=filter-query-value", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dtos = await response.Content.ReadFromJsonAsync<ResultDtoName[]>(TestContext.Current.CancellationToken);
        dtos.ShouldNotBeNull();
        dtos.Select(dto => dto.Id).ShouldContain(id);
        Assert.Fail("TODO(hardslice): store the AggregateName with the FilterName that the query string selects and another one that it excludes, then assert the values of ResultDtoName that the use case reads, and their order.");
    }

    [Fact]
    public async Task Rejects_a_request_without_its_filter_with_a_validation_problem_before_its_handler()
    {
        await using var app = new AppFactory(database);

        var response = await app.CreateClient().GetAsync(Route, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestContext.Current.CancellationToken);
        problem.ShouldNotBeNull();
        problem.Errors.Keys.ShouldBe(["filterName"]);
    }
#else
    [Fact]
    public async Task Lists_a_stored_aggregate_as_its_dto()
    {
        var id = await StoreAggregateName();
        await using var app = new AppFactory(database);

        var response = await app.CreateClient().GetAsync(Route, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dtos = await response.Content.ReadFromJsonAsync<ResultDtoName[]>(TestContext.Current.CancellationToken);
        dtos.ShouldNotBeNull();
        dtos.Select(dto => dto.Id).ShouldContain(id);
        Assert.Fail("TODO(hardslice): assert the values of ResultDtoName that the use case reads, and its filter and order if any.");
    }
#endif

    // Stores, outside the application, the AggregateName that the query reads, and returns its identifier.
    private async Task<Guid> StoreAggregateName()
    {
        await using var db = database.CreateDbContext();
        throw new NotImplementedException(
            "TODO(hardslice): create the AggregateName through the factory method of its aggregate, add it to db.Set<AggregateName>(), "
            + "save it with db.SaveChangesAsync(TestContext.Current.CancellationToken) and return its identifier.");
    }
}
