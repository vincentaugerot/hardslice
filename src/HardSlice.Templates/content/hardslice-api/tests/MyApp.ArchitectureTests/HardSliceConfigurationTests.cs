namespace MyApp.ArchitectureTests;

public sealed class HardSliceConfigurationTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("hardslice-config").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private const string Contracts =
        """
        "contracts": {
            "entity": "MyApp.Domain.IEntity, MyApp.Domain",
            "aggregateRoot": "MyApp.Domain.IAggregateRoot, MyApp.Domain",
            "domainEvent": "MyApp.Domain.IDomainEvent, MyApp.Domain",
            "command": "MyApp.Shared.Messaging.ICommand`1, MyApp",
            "query": "MyApp.Shared.Messaging.IQuery`1, MyApp",
            "requestHandler": "MyApp.Shared.Messaging.IRequestHandler`2, MyApp",
            "domainEventHandler": "MyApp.Shared.Messaging.IDomainEventHandler`1, MyApp"
        }
        """;

    [Fact]
    public void The_solution_configuration_is_valid()
    {
        var configuration = HardSliceConfiguration.Current;

        configuration.Style.ShouldBe("slices");
        // systemCommand is optional: a file that leaves it out declares no system command.
        configuration.Contracts.Keys.Where(key => key != "systemCommand")
            .ShouldBe(["entity", "aggregateRoot", "domainEvent", "command", "query", "requestHandler", "domainEventHandler"], ignoreOrder: true);
        configuration.SharedFolders.ShouldNotBeEmpty();
    }

    [Fact]
    public void A_missing_file_fails_explicitly() =>
        Should.Throw<InvalidOperationException>(() => HardSliceConfiguration.Load(Path.Combine(_directory, "hardslice.json")))
            .Message.ShouldStartWith("hardslice.json: no file at");

    [Theory]
    [InlineData("""{ "version": 1, "style": "slices", """, "not valid JSON")]
    [InlineData("""{ "version": 1, "shared": { "allowedFolders": ["Messaging"] } }""", "'style' must be \"slices\"")]
    [InlineData("""{ "version": 1, "style": "layers", "shared": { "allowedFolders": ["Messaging"] } }""", "'style' must be \"slices\"")]
    [InlineData("""{ "version": 1, "style": "slices", #, "modules": [] }""", "unknown key 'modules'")]
    [InlineData("""{ "version": 1, "style": "slices", # }""", "'shared' is missing")]
    [InlineData("""{ "version": 1, "style": "slices", #, "shared": { "allowedFolders": [] } }""", "must be a non-empty array")]
    [InlineData("""{ "version": 1, "style": "slices", #, "shared": { "allowedFolders": ["Messaging/Bus"] } }""", "not a single folder name")]
    [InlineData("""{ "version": 1, "style": "slices", #, "shared": { "allowedFolders": ["Messaging", "Messaging"] } }""", "twice")]
    [InlineData("""{ "version": 1, "style": "slices", #, "shared": { "allowedFolder": ["Messaging"] } }""", "unknown key 'allowedFolder'")]
    public void An_invalid_file_fails_explicitly(string content, string reason)
    {
        var path = Path.Combine(_directory, "hardslice.json");
        File.WriteAllText(path, content.Replace("#", Contracts));

        var message = Should.Throw<InvalidOperationException>(() => HardSliceConfiguration.Load(path)).Message;

        message.ShouldStartWith("hardslice.json: ");
        message.ShouldContain(reason);
        message.ShouldContain("Skill: fix-architecture-violation. Doc: 'docs/rules/HS017.md'.");
    }

    [Theory]
    [InlineData("""{ "version": 1, "style": "slices", "shared": { "allowedFolders": ["Messaging"] } }""", "'contracts' is missing")]
    [InlineData("""{ "version": 1, "style": "slices", "contracts": [], "shared": { "allowedFolders": ["Messaging"] } }""", "'contracts' must be a JSON object")]
    [InlineData("""{ "version": 1, "style": "slices", "contracts": { "entities": "MyApp.Domain.IEntity, MyApp.Domain" }, "shared": { "allowedFolders": ["Messaging"] } }""", "unknown key 'entities' in 'contracts'")]
    [InlineData("""{ "version": 1, "style": "slices", "contracts": { "entity": "MyApp.Domain.IEntity, MyApp.Domain" }, "shared": { "allowedFolders": ["Messaging"] } }""", "'contracts.aggregateRoot' is missing")]
    public void An_invalid_contracts_section_fails_explicitly(string content, string reason)
    {
        var path = Path.Combine(_directory, "hardslice.json");
        File.WriteAllText(path, content);

        var message = Should.Throw<InvalidOperationException>(() => HardSliceConfiguration.Load(path)).Message;

        message.ShouldStartWith("hardslice.json: ");
        message.ShouldContain(reason);
        message.ShouldContain("Skill: fix-architecture-violation. Doc: 'docs/rules/HS900.md'.");
    }

    [Theory]
    [InlineData("""[]""", "the root must be a JSON object", "Skill: fix-architecture-violation. Doc: 'docs/rules/HS017.md'.")]
    [InlineData("""{ "style": "slices", #, "shared": { "allowedFolders": ["Messaging"] } }""", "'version' is missing", "Skill: fix-architecture-violation. Doc: 'docs/rules/HS900.md'.")]
    [InlineData("""{ "version": "1", "style": "slices", #, "shared": { "allowedFolders": ["Messaging"] } }""", "'version' is \"1\", not a whole number", "Skill: fix-architecture-violation. Doc: 'docs/rules/HS900.md'.")]
    [InlineData("""{ "version": 1.0, "style": "slices", #, "shared": { "allowedFolders": ["Messaging"] } }""", "'version' is 1.0, not a whole number", "Skill: fix-architecture-violation. Doc: 'docs/rules/HS900.md'.")]
    [InlineData("""{ "version": 2, "baseline": {}, "style": "slices", #, "shared": { "allowedFolders": ["Messaging"] } }""", "version 2 is not read by these architecture tests, which read version 1. Fix: update HardSlice.Analyzers", "Skill: fix-architecture-violation. Doc: 'docs/rules/HS900.md'.")]
    public void A_file_without_the_version_these_tests_read_fails_explicitly(string content, string reason, string footer)
    {
        var path = Path.Combine(_directory, "hardslice.json");
        File.WriteAllText(path, content.Replace("#", Contracts));

        var message = Should.Throw<InvalidOperationException>(() => HardSliceConfiguration.Load(path)).Message;

        message.ShouldStartWith("hardslice.json: ");
        message.ShouldContain(reason);
        message.ShouldContain(footer);
    }

    [Fact]
    public void A_schema_for_editors_is_accepted()
    {
        var path = Path.Combine(_directory, "hardslice.json");
        File.WriteAllText(path, $$"""{ "$schema": "https://example.invalid/hardslice.schema.json", "version": 1, "style": "slices", {{Contracts}}, "shared": { "allowedFolders": ["Messaging"] } }""");

        HardSliceConfiguration.Load(path).Style.ShouldBe("slices");
    }

    // An application without work in the background declares no system command.
    [Fact]
    public void The_system_command_contract_is_optional_and_checked_when_present()
    {
        var path = Path.Combine(_directory, "hardslice.json");
        File.WriteAllText(path, $$"""{ "version": 1, "style": "slices", {{Contracts}}, "shared": { "allowedFolders": ["Messaging"] } }""");
        HardSliceConfiguration.Load(path).Contracts.ShouldNotContainKey("systemCommand");

        var withSystemCommand = Contracts.Replace("\"query\":", "\"systemCommand\": \"MyApp.Shared.Messaging.ISystemCommand`1, MyApp\",\n    \"query\":");
        File.WriteAllText(path, $$"""{ "version": 1, "style": "slices", {{withSystemCommand}}, "shared": { "allowedFolders": ["Messaging"] } }""");
        HardSliceConfiguration.Load(path).Contracts["systemCommand"].ShouldBe("MyApp.Shared.Messaging.ISystemCommand`1, MyApp");

        File.WriteAllText(path, $$"""{ "version": 1, "style": "slices", {{withSystemCommand.Replace("ISystemCommand`1, MyApp", "ISystemCommand`1")}}, "shared": { "allowedFolders": ["Messaging"] } }""");
        Should.Throw<InvalidOperationException>(() => HardSliceConfiguration.Load(path)).Message
            .ShouldContain("'contracts.systemCommand' is \"MyApp.Shared.Messaging.ISystemCommand`1\", not \"<metadata name>, <assembly>\"");
    }

    [Theory]
    [InlineData("\"MyApp.Domain.IEntity\"")]
    [InlineData("\"IEntity, \"")]
    [InlineData("\"MyApp.Domain.IEntity,MyApp.Domain\"")]
    [InlineData("\"MyApp.Domain.IEntity, MyApp.Domain, Version=1.0.0.0\"")]
    [InlineData("42")]
    public void A_malformed_contract_fails_explicitly(string entity)
    {
        var path = Path.Combine(_directory, "hardslice.json");
        File.WriteAllText(path, $$"""{ "version": 1, "style": "slices", {{Contracts.Replace("\"MyApp.Domain.IEntity, MyApp.Domain\"", entity)}}, "shared": { "allowedFolders": ["Messaging"] } }""");

        var message = Should.Throw<InvalidOperationException>(() => HardSliceConfiguration.Load(path)).Message;

        message.ShouldContain($"'contracts.entity' is {entity}, not \"<metadata name>, <assembly>\"");
        message.ShouldContain("Skill: fix-architecture-violation. Doc: 'docs/rules/HS900.md'.");
    }
}
