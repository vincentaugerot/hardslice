using MyApp.Features.ModuleName.CommandName;

namespace MyApp.IntegrationTests.Features.ModuleName.CommandName;

// CommandNameCondition decides before each cycle whether AddRecurringCommand sends CommandNameCommand.
// Its tests run the cycles one at a time on a FakeTimeProvider, as the application does, without any real wait. The second
// argument of RecurringCommandCycles configures the instance that runs them, such as builder => builder.UseSetting(key, value),
// and its third one, start, the instant at which the application starts, which is the instant of its cycle at start.
public sealed class CommandNameConditionTests(PostgresDatabase database)
{
    [Fact]
    public async Task Holds_a_cycle_back_with_its_reason()
    {
        await using var app = new AppFactory(database);
        // TODO(hardslice): set up the state in which CommandNameCondition holds CommandNameCommand back: store it in PostgreSQL with database.CreateDbContext(), or set it on the instance that runs the cycles, in the second argument of RecurringCommandCycles.
        var cycles = new RecurringCommandCycles<CommandNameCommand, ResponseTypeName>(app);

        // CommandNameCondition throws its NotImplementedException until it is replaced by its rule.
        var held = await Should.ThrowAsync<HeldCycleException>(cycles.RunNextAsync());

        held.Message.ShouldBe("TODO(hardslice): the reason that CommandNameCondition gives in that state.");
        (await cycles.NextDueAsync()).ShouldBeGreaterThan(cycles.Clock.GetUtcNow(), "A cycle held back is not retried: the next instant of the schedule comes.");
    }

    [Fact]
    public async Task Lets_a_cycle_go_and_sends_the_command()
    {
        await using var app = new AppFactory(database);
        // TODO(hardslice): set up the state in which CommandNameCondition lets CommandNameCommand go, the opposite of the test above.
        var cycles = new RecurringCommandCycles<CommandNameCommand, ResponseTypeName>(app);

        // A cycle that the condition lets go sends the command: RunNextAsync returns its response, or throws the exception of the
        // condition or of the command, such as HeldCycleException when the condition holds it back.
        await cycles.RunNextAsync();
    }
}
