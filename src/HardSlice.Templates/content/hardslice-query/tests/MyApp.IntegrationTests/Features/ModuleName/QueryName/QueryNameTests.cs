using System.Net;
using System.Net.Http.Json;
#if (FilterIsCheckedByValidator)
using Microsoft.AspNetCore.Http;
#endif
#if (FilterIsReadFromBinding)
using Microsoft.AspNetCore.Mvc;
#endif
using MyApp.Domain.ModuleName;
using MyApp.Features.ModuleName;
using MyApp.Features.ModuleName.QueryName;

namespace MyApp.IntegrationTests.Features.ModuleName.QueryName;

public sealed class QueryNameTests(PostgresDatabase database)
{
#if (RouteHasParameters && !FilterIsInRoute)
    // TODO(hardslice): replace each route parameter between braces in Route with a value of the test, and bind it in the endpoint of QueryName, such as a parameter of its delegate.
#endif
#if (RouteIsGroup)
    private const string Route = ModuleNameModule.Route;
#else
    private const string Route = ModuleNameModule.Route + "/query-route";
#endif
#if (FilterIsInRoute)

    // The filter FilterName is the parameter filter-route-token of Route: each request puts its value in its place.
    private static string RouteWith(string value) => Route.Replace("filter-route-token", value, StringComparison.Ordinal);
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
#if (RequireIsGiven)

    // The policy AuthorizationPolicyName refuses a request without authentication before the endpoint: its 401 is not in the signature
    // of the endpoint (HS015), this test stands for it. The other tests send the client of the policy, which AppFactory holds.
    [Fact]
    public async Task Rejects_a_request_without_authentication_before_its_handler()
    {
        await using var app = new AppFactory(database);
        var anonymous = app.CreateClient();

        var response = await anonymous.GetAsync($"{Route}/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
#endif
#elif (ResultIsFiltered)
    [Fact]
    public async Task Lists_the_stored_aggregates_that_its_filter_selects()
    {
        var id = await StoreAggregateName();
        await using var app = new AppFactory(database);

#if (FilterIsInRoute)
        var response = await app.CreateClient().GetAsync(RouteWith("filter-query-value"), TestContext.Current.CancellationToken);
#else
        var response = await app.CreateClient().GetAsync($"{Route}?filterName=filter-query-value", TestContext.Current.CancellationToken);
#endif

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dtos = await response.Content.ReadFromJsonAsync<ResultDtoName[]>(TestContext.Current.CancellationToken);
        dtos.ShouldNotBeNull();
        dtos.Select(dto => dto.Id).ShouldContain(id);
#if (FilterIsInRoute)
        Assert.Fail("TODO(hardslice): store the AggregateName with the FilterName that the route selects and another one that it excludes, then assert the values of ResultDtoName that the use case reads, and their order.");
#else
        Assert.Fail("TODO(hardslice): store the AggregateName with the FilterName that the query string selects and another one that it excludes, then assert the values of ResultDtoName that the use case reads, and their order.");
#endif
    }

#if (FilterIsInRoute && FilterIsString)
    // A parameter of the route is never missing, and its binding takes any text: the validator alone refuses a value of FilterName,
    // by the rules of its format.
    [Fact]
    public async Task Rejects_a_filter_that_its_validator_refuses_with_a_validation_problem_before_its_handler()
    {
        Assert.Fail("TODO(hardslice): give QueryNameValidator the rule of the format of FilterName, such as Matches(...), then replace \"filter-refused-value\" below with a value of FilterName that the route matches and that the rule refuses, and remove this line.");
        await using var app = new AppFactory(database);

        var response = await app.CreateClient().GetAsync(RouteWith("filter-refused-value"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestContext.Current.CancellationToken);
        problem.ShouldNotBeNull();
        problem.Errors.Keys.ShouldBe(["filterName"]);
    }
#elif (FilterRouteIsConstrained)
    // A parameter of the route is never missing, and its constraint refuses a value of FilterName that it does not accept: no endpoint
    // matches the request, answered with 404 before the binding and the validator.
    [Fact]
    public async Task Does_not_match_a_filter_that_its_route_constraint_refuses()
    {
        await using var app = new AppFactory(database);

        var response = await app.CreateClient().GetAsync(RouteWith("filter-refused-value"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
#elif (FilterIsInRoute)
    // A parameter of the route is never missing, so the validator never sees a request without FilterName: the binding answers 400 to a
    // value that it cannot read as FilterType, before the validator and the handler.
    [Fact]
    public async Task Rejects_a_filter_that_its_binding_cannot_read_before_its_handler()
    {
        await using var app = new AppFactory(database);

        var response = await app.CreateClient().GetAsync(RouteWith("filter-refused-value"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        problem.ShouldNotBeNull();
        problem.Title.ShouldBe("The request could not be read.");
        problem.Detail.ShouldNotBeNull().ShouldContain("filter-refused-value");
    }
#else
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
#endif
#if (RequireIsGiven)

    // The policy AuthorizationPolicyName refuses a request without authentication before the endpoint: its 401 is not in the signature
    // of the endpoint (HS015), this test stands for it. The other tests send the client of the policy, which AppFactory holds.
    [Fact]
    public async Task Rejects_a_request_without_authentication_before_its_handler()
    {
        await using var app = new AppFactory(database);
        var anonymous = app.CreateClient();

#if (FilterIsInRoute)
        var response = await anonymous.GetAsync(RouteWith("filter-query-value"), TestContext.Current.CancellationToken);
#else
        var response = await anonymous.GetAsync($"{Route}?filterName=filter-query-value", TestContext.Current.CancellationToken);
#endif

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
#endif
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
#if (RequireIsGiven)

    // The policy AuthorizationPolicyName refuses a request without authentication before the endpoint: its 401 is not in the signature
    // of the endpoint (HS015), this test stands for it. The other tests send the client of the policy, which AppFactory holds.
    [Fact]
    public async Task Rejects_a_request_without_authentication_before_its_handler()
    {
        await using var app = new AppFactory(database);
        var anonymous = app.CreateClient();

        var response = await anonymous.GetAsync(Route, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
#endif
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
