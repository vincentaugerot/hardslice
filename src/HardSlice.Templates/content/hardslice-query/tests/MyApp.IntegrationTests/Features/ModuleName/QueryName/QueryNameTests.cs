using System.Net;
using System.Net.Http.Json;
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
        var dto = await response.Content.ReadFromJsonAsync<QueryNameDto>(TestContext.Current.CancellationToken);
        dto.ShouldNotBeNull();
        dto.Id.ShouldBe(id);
        Assert.Fail("TODO(hardslice): assert the values of QueryNameDto that the use case reads from the stored AggregateName.");
    }

    [Fact]
    public async Task Returns_404_for_an_unknown_aggregate()
    {
        await using var app = new AppFactory(database);

        var response = await app.CreateClient().GetAsync($"{Route}/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
#else
    [Fact]
    public async Task Lists_a_stored_aggregate_as_its_dto()
    {
        var id = await StoreAggregateName();
        await using var app = new AppFactory(database);

        var response = await app.CreateClient().GetAsync(Route, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dtos = await response.Content.ReadFromJsonAsync<QueryNameDto[]>(TestContext.Current.CancellationToken);
        dtos.ShouldNotBeNull();
        dtos.Select(dto => dto.Id).ShouldContain(id);
        Assert.Fail("TODO(hardslice): assert the values of QueryNameDto that the use case reads, and its filter and order if any.");
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
