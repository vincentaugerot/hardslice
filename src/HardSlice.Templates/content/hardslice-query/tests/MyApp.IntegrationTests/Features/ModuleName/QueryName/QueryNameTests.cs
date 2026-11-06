using System.Net;
using System.Net.Http.Json;
#if (HasQueryStringFilters || HasStringRouteFilters)
using Microsoft.AspNetCore.Http;
#endif
#if (HasBindingRouteFilters)
using Microsoft.AspNetCore.Mvc;
#endif
using MyApp.Domain.ModuleName;
using MyApp.Features.ModuleName;
using MyApp.Features.ModuleName.QueryName;

namespace MyApp.IntegrationTests.Features.ModuleName.QueryName;

public sealed class QueryNameTests(PostgresDatabase database)
{
#if (RouteHasParameters && !HasRouteFilters)
    // TODO(hardslice): replace each route parameter between braces in Route with a value of the test, and bind it in the endpoint of QueryName, such as a parameter of its delegate.
#endif
#if (RouteIsGroup)
    private const string Route = ModuleNameModule.Route;
#else
    private const string Route = ModuleNameModule.Route + "/query-route";
#endif
#if (HasQueryStringFilters)

    // A valid value of each filter of the query string.
    private const string QueryString = "?query-string-example";
#endif
#if (HasRouteFilters)

    // Route with a valid value in place of each filter of the route, or the value that a test gives to the filter of that key.
    private static string RouteWith(string? filter = null, string? value = null) =>
        Route
            RouteFilterReplacements;
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
    public async Task Lists_the_stored_aggregates_that_its_filters_select()
    {
        var id = await StoreAggregateName();
        await using var app = new AppFactory(database);

#if (HasRouteFilters && HasQueryStringFilters)
        var response = await app.CreateClient().GetAsync(RouteWith() + QueryString, TestContext.Current.CancellationToken);
#elif (HasRouteFilters)
        var response = await app.CreateClient().GetAsync(RouteWith(), TestContext.Current.CancellationToken);
#else
        var response = await app.CreateClient().GetAsync(Route + QueryString, TestContext.Current.CancellationToken);
#endif

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dtos = await response.Content.ReadFromJsonAsync<ResultDtoName[]>(TestContext.Current.CancellationToken);
        dtos.ShouldNotBeNull();
        dtos.Select(dto => dto.Id).ShouldContain(id);
        Assert.Fail("TODO(hardslice): store the AggregateName that the filters of the request select and another one that they exclude, then assert the values of ResultDtoName that the use case reads, and their order.");
    }
#if (HasQueryStringFilters)

    [Fact]
    public async Task Rejects_a_request_without_its_filters_with_a_validation_problem_before_its_handler()
    {
        await using var app = new AppFactory(database);

#if (HasRouteFilters)
        var response = await app.CreateClient().GetAsync(RouteWith(), TestContext.Current.CancellationToken);
#else
        var response = await app.CreateClient().GetAsync(Route, TestContext.Current.CancellationToken);
#endif

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestContext.Current.CancellationToken);
        problem.ShouldNotBeNull();
        problem.Errors.Keys.ShouldBe([parameter-keys]);
    }
#endif
#if (HasBindingRouteFilters)

    // A parameter of the route is never missing, so the validator never sees a request without a filter of the route: the binding
    // answers 400 to a value that it cannot read as the type of its filter, before the validator and the handler.
    [Theory]
    BindingRouteFilterCases
    public async Task Rejects_a_filter_that_its_binding_cannot_read_before_its_handler(string filter, string value)
    {
        await using var app = new AppFactory(database);

#if (HasQueryStringFilters)
        var response = await app.CreateClient().GetAsync(RouteWith(filter, value) + QueryString, TestContext.Current.CancellationToken);
#else
        var response = await app.CreateClient().GetAsync(RouteWith(filter, value), TestContext.Current.CancellationToken);
#endif

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        problem.ShouldNotBeNull();
        problem.Title.ShouldBe("The request could not be read.");
        problem.Detail.ShouldNotBeNull().ShouldContain(value);
    }
#endif
#if (HasConstrainedRouteFilters)

    // A parameter of the route is never missing, and its constraint refuses a value that it does not accept: no endpoint matches the
    // request, answered with 404 before the binding and the validator.
    [Theory]
    ConstrainedRouteFilterCases
    public async Task Does_not_match_a_filter_that_its_route_constraint_refuses(string filter, string value)
    {
        await using var app = new AppFactory(database);

#if (HasQueryStringFilters)
        var response = await app.CreateClient().GetAsync(RouteWith(filter, value) + QueryString, TestContext.Current.CancellationToken);
#else
        var response = await app.CreateClient().GetAsync(RouteWith(filter, value), TestContext.Current.CancellationToken);
#endif

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
#endif
#if (HasStringRouteFilters)

    // A parameter of the route is never missing, and its binding takes any text: the validator alone refuses a value of a text filter
    // of the route, by the rules of its format.
    [Theory]
    StringRouteFilterCases
    public async Task Rejects_a_filter_that_its_validator_refuses_with_a_validation_problem_before_its_handler(string filter, string value)
    {
        Assert.Fail("TODO(hardslice): give QueryNameValidator the rule of the format of each text filter of the route, such as Matches(...), then replace \"refused-value\" in each case above with a value of its filter that the route matches and that the rule refuses, give the filter a value that the rule accepts in RouteWith, and remove this line.");
        await using var app = new AppFactory(database);

#if (HasQueryStringFilters)
        var response = await app.CreateClient().GetAsync(RouteWith(filter, value) + QueryString, TestContext.Current.CancellationToken);
#else
        var response = await app.CreateClient().GetAsync(RouteWith(filter, value), TestContext.Current.CancellationToken);
#endif

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestContext.Current.CancellationToken);
        problem.ShouldNotBeNull();
        problem.Errors.Keys.ShouldBe([filter]);
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

#if (HasRouteFilters && HasQueryStringFilters)
        var response = await anonymous.GetAsync(RouteWith() + QueryString, TestContext.Current.CancellationToken);
#elif (HasRouteFilters)
        var response = await anonymous.GetAsync(RouteWith(), TestContext.Current.CancellationToken);
#else
        var response = await anonymous.GetAsync(Route + QueryString, TestContext.Current.CancellationToken);
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
