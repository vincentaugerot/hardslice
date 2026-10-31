using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using MyApp.Features.ModuleName.CommandName;

namespace MyApp.IntegrationTests.Features.ModuleName.CommandName;

public sealed class CommandNameTests(PostgresDatabase database)
{
    private const string Route = "/module-route/command-route";

    [Fact]
    public async Task Handles_a_valid_command()
    {
        await using var app = new MyAppFactory(database);
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
        await using var app = new MyAppFactory(database);

        var response = await app.CreateClient().PostAsJsonAsync(Route, new CommandNameCommand(Guid.Empty), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestContext.Current.CancellationToken);
        problem.ShouldNotBeNull();
        problem.Errors.Keys.ShouldBe(["id"]);
    }
}
