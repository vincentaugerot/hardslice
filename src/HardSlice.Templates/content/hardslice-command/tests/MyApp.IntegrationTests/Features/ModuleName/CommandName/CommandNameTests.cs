#if (ScheduleIsGiven)
#if (RetryIsGiven)
using System.Globalization;
#endif
using MyApp.Features.ModuleName.CommandName;
#elif (EndpointIsNone)
#if (!ParametersIsNone)
using FluentValidation;
#endif
using Microsoft.Extensions.DependencyInjection;
using MyApp.Features.ModuleName.CommandName;
using MyApp.Shared.Messaging;
#else
using System.Net;
using System.Net.Http.Json;
#if (!ParametersIsNone)
using Microsoft.AspNetCore.Http;
#endif
using MyApp.Features.ModuleName;
using MyApp.Features.ModuleName.CommandName;
#endif

namespace MyApp.IntegrationTests.Features.ModuleName.CommandName;

#if (DailyAtIsGiven)
// AddRecurringCommand sends the command every day at its local time: its tests run its cycles one at a time on a FakeTimeProvider, as
// the application does, without any real wait. No other test starts the recurring command.
#elif (EveryIsGiven)
// AddRecurringCommand sends the command at each interval: its tests run its cycles one at a time on a FakeTimeProvider, as the
// application does, without any real wait. No other test starts the recurring command.
#elif (EndpointIsNone)
// A system command has no endpoint: its tests send it through IMediator, in a scope of their own, as the type of module ModuleName
// that sends it does.
#endif
public sealed class CommandNameTests(PostgresDatabase database)
{
#if (ScheduleIsGiven)
#if (DailyAtIsGiven)
    [Fact]
    public async Task Runs_a_cycle_every_day_at_its_local_time()
    {
        await using var app = new AppFactory(database);
#if (ResponseIsId)
        var cycles = new RecurringCommandCycles<CommandNameCommand, Guid>(app);
#else
        var cycles = new RecurringCommandCycles<CommandNameCommand, ResponseTypeName>(app);
#endif
        var zone = TimeZoneInfo.FindSystemTimeZoneById("RecurringTimeZone");
#if (AtStart)

        // The cycle at start comes first, at TestInstants.Start: Runs_a_cycle_at_start_before_the_first_instant_of_its_schedule tests it.
        await cycles.RunNextAsync();
#endif

        for (var day = 1; day <= 2; day++)
        {
            var due = await cycles.NextDueAsync();
            TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(due, zone).DateTime).ShouldBe(new TimeOnly(RecurringHour, RecurringMinute));
            // CommandNameHandler throws its NotImplementedException until it is replaced by the use case.
            var response = await cycles.RunNextAsync();
        }

        Assert.Fail("TODO(hardslice): assert the business outcome of two days of cycles of CommandName, such as the state stored in PostgreSQL, read with database.CreateDbContext().");
    }
#elif (EveryIsGiven)
    [Fact]
    public async Task Runs_a_cycle_at_each_interval()
    {
        await using var app = new AppFactory(database);
#if (ResponseIsId)
        var cycles = new RecurringCommandCycles<CommandNameCommand, Guid>(app);
#else
        var cycles = new RecurringCommandCycles<CommandNameCommand, ResponseTypeName>(app);
#endif
#if (AtStart)

        // The cycle at start comes first, at TestInstants.Start: Runs_a_cycle_at_start_before_the_first_instant_of_its_schedule tests it.
        await cycles.RunNextAsync();
#endif

        for (var cycle = 1; cycle <= 3; cycle++)
        {
            // CommandNameHandler throws its NotImplementedException until it is replaced by the use case.
            var response = await cycles.RunNextAsync();
        }

        Assert.Fail("TODO(hardslice): assert the business outcome of three cycles of CommandName, such as the state stored in PostgreSQL, read with database.CreateDbContext().");
    }
#endif
#if (AtStart)

    // AlsoAtStart sends the command once when the application starts, before the first instant of its schedule: the cycles start
    // the application at start, TestInstants.Start here, and its first cycle is due then, without moving the clock. Pass another
    // instant to test the cycle at start at a time of day, such as the evening before in the time zone of a daily schedule.
    [Fact]
    public async Task Runs_a_cycle_at_start_before_the_first_instant_of_its_schedule()
    {
        await using var app = new AppFactory(database);
#if (ResponseIsId)
        var cycles = new RecurringCommandCycles<CommandNameCommand, Guid>(app, start: TestInstants.Start);
#else
        var cycles = new RecurringCommandCycles<CommandNameCommand, ResponseTypeName>(app, start: TestInstants.Start);
#endif

        (await cycles.NextDueAsync()).ShouldBe(TestInstants.Start);
        // CommandNameHandler throws its NotImplementedException until it is replaced by the use case.
        var response = await cycles.RunNextAsync();
        cycles.Clock.GetUtcNow().ShouldBe(TestInstants.Start);
        (await cycles.NextDueAsync()).ShouldBeGreaterThan(TestInstants.Start, "The schedule goes on after the cycle at start.");

        Assert.Fail("TODO(hardslice): assert the business outcome of the cycle at start of CommandName, such as the state stored in PostgreSQL, read with database.CreateDbContext().");
    }
#endif
#if (RetryIsGiven)

    // FailNext fails the cycle and each of its retries before the pipeline of the command: AddRecurringCommand sends it again
    // RecurringCountOfRetries times, RecurringDelayOfRetries apart, then waits for the next instant of its schedule.
    [Fact]
    public async Task Sends_a_failed_cycle_again_at_each_retry_then_waits_for_its_next_instant()
    {
        await using var app = new AppFactory(database);
#if (ResponseIsId)
        var cycles = new RecurringCommandCycles<CommandNameCommand, Guid>(app);
#else
        var cycles = new RecurringCommandCycles<CommandNameCommand, ResponseTypeName>(app);
#endif
        var retryDelay = TimeSpan.Parse("RecurringDelayOfRetries", CultureInfo.InvariantCulture);
        cycles.FailNext(1 + RecurringCountOfRetries);

        await Should.ThrowAsync<FailedCycleException>(() => cycles.RunNextAsync());
        var failed = cycles.Clock.GetUtcNow();
        for (var retry = 1; retry <= RecurringCountOfRetries; retry++)
        {
            await Should.ThrowAsync<FailedCycleException>(() => cycles.RunNextAsync());
            cycles.Clock.GetUtcNow().ShouldBe(failed + retry * retryDelay);
        }

        var next = await cycles.NextDueAsync();
        next.ShouldBeGreaterThan(cycles.Clock.GetUtcNow());
#if (DailyAtIsGiven)
        var zone = TimeZoneInfo.FindSystemTimeZoneById("RecurringTimeZone");
        TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(next, zone).DateTime).ShouldBe(new TimeOnly(RecurringHour, RecurringMinute));
        (next - cycles.Clock.GetUtcNow()).ShouldBeLessThanOrEqualTo(TimeSpan.FromHours(25), "The next day comes after the last retry.");
#else
        var interval = TimeSpan.Parse("RecurringInterval", CultureInfo.InvariantCulture);
        ((next - TestInstants.Start).Ticks % interval.Ticks).ShouldBe(0, "The next cycle comes at an instant of the interval from the start.");
        (next - cycles.Clock.GetUtcNow()).ShouldBeLessThanOrEqualTo(interval, "The next interval comes after the last retry.");
#endif
    }
#endif
#elif (EndpointIsNone)
#if (ParametersIsNone)
    // No parameter, so no invalid command: test its success here, and a failure of the domain when one can happen.
    [Fact]
    public async Task Handles_the_command()
    {
        await using var app = new AppFactory(database);
        await using var scope = app.Services.CreateAsyncScope();

        // CommandNameHandler throws its NotImplementedException until it is replaced by the use case.
        var response = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new CommandNameCommand(), TestContext.Current.CancellationToken);

        Assert.Fail("TODO(hardslice): assert the business outcome of CommandName, such as the state stored in PostgreSQL, read with database.CreateDbContext().");
    }
#else
    [Fact]
    public async Task Handles_a_valid_command()
    {
        await using var app = new AppFactory(database);
        await using var scope = app.Services.CreateAsyncScope();
#if (ParametersIsList)
        var command = new CommandNameCommand(ParameterValues);
#else
        var command = new CommandNameCommand(Guid.NewGuid());
#endif

        // CommandNameHandler throws its NotImplementedException until it is replaced by the use case.
        var response = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command, TestContext.Current.CancellationToken);

        Assert.Fail("TODO(hardslice): assert the business outcome of CommandName, such as the state stored in PostgreSQL, read with database.CreateDbContext().");
    }

    [Fact]
    public async Task Rejects_an_invalid_command_before_its_handler()
    {
        await using var app = new AppFactory(database);
        await using var scope = app.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

#if (ParametersIsList)
        var exception = await Should.ThrowAsync<ValidationException>(
            () => mediator.Send(new CommandNameCommand(ParameterNulls), TestContext.Current.CancellationToken));

        exception.Errors.Select(error => error.PropertyName).ShouldBe([ParameterPropertyNames]);
#else
        var exception = await Should.ThrowAsync<ValidationException>(
            () => mediator.Send(new CommandNameCommand(Guid.Empty), TestContext.Current.CancellationToken));

        exception.Errors.Select(error => error.PropertyName).ShouldBe(["Id"]);
#endif
    }
#endif
#else
#if (RouteHasParameters)
    // TODO(hardslice): replace each route parameter between braces in Route with a value of the test, and bind it in the endpoint of CommandName, such as a parameter of its delegate.
#endif
#if (RouteIsGroup)
    private const string Route = ModuleNameModule.Route;
#else
    private const string Route = ModuleNameModule.Route + "/command-route";
#endif
#if (ParametersIsNone)

    // No parameter, so no invalid request: test its success here, and a failure of the domain when one can happen, such as the
    // 409 of a state that forbids it.
    [Fact]
    public async Task Handles_the_command()
    {
        await using var app = new AppFactory(database);

#if (MethodIsDelete)
        var response = await app.CreateClient().DeleteAsync(Route, TestContext.Current.CancellationToken);
#else
        var response = await app.CreateClient().PostAsync(Route, content: null, TestContext.Current.CancellationToken);
#endif

        response.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            "CommandNameHandler answers 500 until its NotImplementedException is replaced by the use case.");
        Assert.Fail("TODO(hardslice): assert the business outcome of CommandName, such as the state stored in PostgreSQL, read with database.CreateDbContext().");
    }
#if (RequireIsGiven)

    // The policy AuthorizationPolicyName refuses a request without authentication before the endpoint: its 401 is not in the signature
    // of the endpoint (HS015), this test stands for it. The other tests send the client of the policy, which AppFactory holds.
    [Fact]
    public async Task Rejects_a_request_without_authentication_before_its_handler()
    {
        await using var app = new AppFactory(database);
        var anonymous = app.CreateClient();

#if (MethodIsDelete)
        var response = await anonymous.DeleteAsync(Route, TestContext.Current.CancellationToken);
#else
        var response = await anonymous.PostAsync(Route, content: null, TestContext.Current.CancellationToken);
#endif

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
#endif
#else

    [Fact]
    public async Task Handles_a_valid_command()
    {
        await using var app = new AppFactory(database);
#if (ParametersIsList)
        var command = new CommandNameCommand(ParameterValues);
#else
        var command = new CommandNameCommand(Guid.NewGuid());
#endif

#if (ReadsIdFromRoute)
        var response = await app.CreateClient().DeleteAsync($"{Route}/{command.Id}", TestContext.Current.CancellationToken);
#else
        var response = await app.CreateClient().PostAsJsonAsync(Route, command, TestContext.Current.CancellationToken);
#endif

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

#if (ReadsIdFromRoute)
        var response = await app.CreateClient().DeleteAsync($"{Route}/{Guid.Empty}", TestContext.Current.CancellationToken);
#elif (ParametersIsList)
        var response = await app.CreateClient().PostAsJsonAsync(Route, new CommandNameCommand(ParameterNulls), TestContext.Current.CancellationToken);
#else
        var response = await app.CreateClient().PostAsJsonAsync(Route, new CommandNameCommand(Guid.Empty), TestContext.Current.CancellationToken);
#endif

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestContext.Current.CancellationToken);
        problem.ShouldNotBeNull();
#if (ParametersIsList)
        problem.Errors.Keys.ShouldBe([parameter-keys]);
#else
        problem.Errors.Keys.ShouldBe(["id"]);
#endif
    }
#if (RequireIsGiven)

    // The policy AuthorizationPolicyName refuses a request without authentication before the endpoint: its 401 is not in the signature
    // of the endpoint (HS015), this test stands for it. The other tests send the client of the policy, which AppFactory holds.
    [Fact]
    public async Task Rejects_a_request_without_authentication_before_its_handler()
    {
        await using var app = new AppFactory(database);
        var anonymous = app.CreateClient();

#if (ReadsIdFromRoute)
        var response = await anonymous.DeleteAsync($"{Route}/{Guid.NewGuid()}", TestContext.Current.CancellationToken);
#elif (ParametersIsList)
        var response = await anonymous.PostAsJsonAsync(Route, new CommandNameCommand(ParameterValues), TestContext.Current.CancellationToken);
#else
        var response = await anonymous.PostAsJsonAsync(Route, new CommandNameCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);
#endif

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
#endif
#endif
#endif
}
