namespace MyApp.ArchitectureTests;

// HS013: every command with parameters has a validator. HS014: every command handler and every domain event handler, which
// changes state inside the transaction of a command, has tests at the path of its slice.
// Queries only read: they are out of both rules, but their slices still have tests (HS018).
public sealed class CommandTests
{
    private static readonly TestSources Tests = TestSources.Solution;

    [Fact]
    public void Commands_with_parameters_have_a_validator()
    {
        var types = Application.WebAssembly.GetTypes();
        var validators = RequestTypes.ValidatorsIn(types);

        Violations.ShouldBeNone(
            from command in types
            where RequestTypes.IsCommand(command) && !RequestTypes.IsValidated(command, validators)
            let parameters = RequestTypes.ParametersOf(command)
            where parameters.Count > 0
            let name = Name(command)
            let location = Location.Of(command.Namespace)
            select $"HS013: command '{name}' {location.Describe()} has parameters ({string.Join(", ", parameters)}) but no validator. "
                + "Every command with parameters has a validator that checks the shape of its input before its handler runs. "
                + $"Fix: declare an internal '{ValidatorName(name)} : AbstractValidator<{name}>' next to the command, "
                + $"register it as 'IValidator<{name}>' in its module and test it in '{TestSources.PathOf(Tests.UnitTests, FolderOf(command))}'. "
                + "Doc: 'docs/rules/HS013.md'.");
    }

    // Tests anywhere under the slice folder count; outside a slice, only those directly in the folder of the handler's namespace.
    [Fact]
    public void Command_handlers_have_tests_at_the_path_of_their_slice()
    {
        Violations.ShouldBeNone(
            from handler in Application.WebAssembly.GetTypes()
            let command = RequestTypes.CommandHandledBy(handler)
            where command is not null
            let location = Location.Of(handler.Namespace)
            let folder = FolderOf(handler)
            where !Tests.HasTestsIn(folder, recursive: location.Slice is not null)
            select $"HS014: command handler '{Name(handler)}' {location.Describe()} has no test in {Tests.Describe(folder)}. "
                + "Every command handler has at least one test at the path of its slice. "
                + $"Fix: add an integration test that sends '{Name(command)}', an invalid one included, in '{TestSources.PathOf(Tests.IntegrationTests, folder)}', "
                + $"and the unit tests of its validator in '{TestSources.PathOf(Tests.UnitTests, folder)}'. Doc: 'docs/rules/HS014.md'.");
    }

    [Fact]
    public void Domain_event_handlers_have_tests_at_the_path_of_their_slice()
    {
        Violations.ShouldBeNone(
            from handler in Application.WebAssembly.GetTypes()
            let domainEvent = RequestTypes.EventHandledBy(handler)
            where domainEvent is not null
            let location = Location.Of(handler.Namespace)
            let folder = FolderOf(handler)
            where !Tests.HasTestsIn(folder, recursive: location.Slice is not null)
            select $"HS014: domain event handler '{Name(handler)}' {location.Describe()} has no test in {Tests.Describe(folder)}. "
                + "Every domain event handler has at least one test at the path of its slice. "
                + $"Fix: add an integration test in '{TestSources.PathOf(Tests.IntegrationTests, folder)}' that sends a command whose aggregate raises "
                + $"'{Name(domainEvent)}' and checks what the handler changed. Doc: 'docs/rules/HS014.md'.");
    }

    // The folder of a type below 'src/<App>/', read from its namespace: 'Features/Orders/PlaceOrder' for a type of that slice or below.
    private static string FolderOf(Type type)
    {
        if (Location.Of(type.Namespace).Slice is { } slice)
            return $"Features/{slice}";

        var ns = type.Namespace ?? "";
        if (ns.StartsWith(Application.WebName + ".", StringComparison.Ordinal))
            ns = ns[(Application.WebName.Length + 1)..];
        else if (ns == Application.WebName)
            ns = "";

        return ns.Replace('.', '/');
    }

    // A handler generic over its command names the type parameter: 'TCommand'.
    private static string Name(Type type) => type.IsGenericParameter ? type.Name : Application.DisplayName(ReferencedType.From(type));

    // 'PlaceOrderValidator' for 'PlaceOrderCommand'.
    private static string ValidatorName(string command) =>
        (command.EndsWith("Command", StringComparison.Ordinal) ? command[..^"Command".Length] : command) + "Validator";
}
