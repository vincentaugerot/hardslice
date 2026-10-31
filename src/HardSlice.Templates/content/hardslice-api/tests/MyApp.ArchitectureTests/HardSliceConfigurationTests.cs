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
        configuration.Contracts.Keys.ShouldBe(["entity", "aggregateRoot", "domainEvent", "command", "query", "requestHandler", "domainEventHandler"], ignoreOrder: true);
        configuration.SharedFolders.ShouldNotBeEmpty();
    }

    [Fact]
    public void A_missing_file_fails_explicitly() =>
        Should.Throw<InvalidOperationException>(() => HardSliceConfiguration.Load(Path.Combine(_directory, "hardslice.json")))
            .Message.ShouldStartWith("hardslice.json: no file at");

    [Theory]
    [InlineData("""{ "style": "slices", """, "not valid JSON")]
    [InlineData("""[]""", "the root must be a JSON object")]
    [InlineData("""{ "shared": { "allowedFolders": ["Messaging"] } }""", "'style' must be \"slices\"")]
    [InlineData("""{ "style": "layers", "shared": { "allowedFolders": ["Messaging"] } }""", "'style' must be \"slices\"")]
    [InlineData("""{ "style": "slices", #, "modules": [] }""", "unknown key 'modules'")]
    [InlineData("""{ "style": "slices", # }""", "'shared' is missing")]
    [InlineData("""{ "style": "slices", #, "shared": { "allowedFolders": [] } }""", "must be a non-empty array")]
    [InlineData("""{ "style": "slices", #, "shared": { "allowedFolders": ["Messaging/Bus"] } }""", "not a single folder name")]
    [InlineData("""{ "style": "slices", #, "shared": { "allowedFolders": ["Messaging", "Messaging"] } }""", "twice")]
    [InlineData("""{ "style": "slices", #, "shared": { "allowedFolder": ["Messaging"] } }""", "unknown key 'allowedFolder'")]
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
    [InlineData("""{ "style": "slices", "shared": { "allowedFolders": ["Messaging"] } }""", "'contracts' is missing")]
    [InlineData("""{ "style": "slices", "contracts": [], "shared": { "allowedFolders": ["Messaging"] } }""", "'contracts' must be a JSON object")]
    [InlineData("""{ "style": "slices", "contracts": { "entities": "MyApp.Domain.IEntity, MyApp.Domain" }, "shared": { "allowedFolders": ["Messaging"] } }""", "unknown key 'entities' in 'contracts'")]
    [InlineData("""{ "style": "slices", "contracts": { "entity": "MyApp.Domain.IEntity, MyApp.Domain" }, "shared": { "allowedFolders": ["Messaging"] } }""", "'contracts.aggregateRoot' is missing")]
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
    [InlineData("\"MyApp.Domain.IEntity\"")]
    [InlineData("\"IEntity, \"")]
    [InlineData("\"MyApp.Domain.IEntity,MyApp.Domain\"")]
    [InlineData("\"MyApp.Domain.IEntity, MyApp.Domain, Version=1.0.0.0\"")]
    [InlineData("42")]
    public void A_malformed_contract_fails_explicitly(string entity)
    {
        var path = Path.Combine(_directory, "hardslice.json");
        File.WriteAllText(path, $$"""{ "style": "slices", {{Contracts.Replace("\"MyApp.Domain.IEntity, MyApp.Domain\"", entity)}}, "shared": { "allowedFolders": ["Messaging"] } }""");

        var message = Should.Throw<InvalidOperationException>(() => HardSliceConfiguration.Load(path)).Message;

        message.ShouldContain($"'contracts.entity' is {entity}, not \"<metadata name>, <assembly>\"");
        message.ShouldContain("Skill: fix-architecture-violation. Doc: 'docs/rules/HS900.md'.");
    }
}
