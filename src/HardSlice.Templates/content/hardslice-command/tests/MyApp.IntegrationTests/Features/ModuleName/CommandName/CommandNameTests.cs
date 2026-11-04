using System.Net;
using System.Net.Http.Json;
#if (!ParametersIsNone)
using Microsoft.AspNetCore.Http;
#endif
using MyApp.Features.ModuleName.CommandName;

namespace MyApp.IntegrationTests.Features.ModuleName.CommandName;

public sealed class CommandNameTests(PostgresDatabase database)
{
    private const string Route = "/module-route/command-route";
#if (ParametersIsNone)

    // No parameter, so no invalid request: test its success here, and a failure of the domain when one can happen, such as the
    // 409 of a state that forbids it.
    [Fact]
    public async Task Handles_the_command()
    {
        await using var app = new AppFactory(database);

        var response = await app.CreateClient().PostAsync(Route, content: null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            "CommandNameHandler answers 500 until its NotImplementedException is replaced by the use case.");
        Assert.Fail("TODO(hardslice): assert the business outcome of CommandName, such as the state stored in PostgreSQL, read with database.CreateDbContext().");
    }
#else

    [Fact]
    public async Task Handles_a_valid_command()
    {
        await using var app = new AppFactory(database);
        var command = new CommandNameCommand(Guid.NewGuid());

        var response = await app.CreateClient().PostAsJsonAsync(Route, command, TestContext.Current.CancellationToken);

#if (ResponseIsNone)
        response.StatusCode.ShouldBe(
            HttpStatusCode.NoContent,
#else
        response.StatusCode.ShouldBe(
            HttpStatusCode.OK,
#endif
            "CommandNameHandler answers 500 until its NotImplementedException is replaced by the use case.");
        Assert.Fail("TODO(hardslice): assert the business outcome of CommandName, such as the state stored in PostgreSQL, read with database.CreateDbContext().");
    }

    [Fact]
    public async Task Rejects_an_invalid_command_with_a_validation_problem_before_its_handler()
    {
        await using var app = new AppFactory(database);

        var response = await app.CreateClient().PostAsJsonAsync(Route, new CommandNameCommand(Guid.Empty), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestContext.Current.CancellationToken);
        problem.ShouldNotBeNull();
        problem.Errors.Keys.ShouldBe(["id"]);
    }
#endif
}
