namespace MyApp.ArchitectureTests;

public sealed class HardSliceConfigurationTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("hardslice-config").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void The_solution_configuration_is_valid()
    {
        var configuration = HardSliceConfiguration.Current;

        configuration.Style.ShouldBe("slices");
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
    [InlineData("""{ "style": "slices" }""", "'shared' is missing")]
    [InlineData("""{ "style": "slices", "shared": { "allowedFolders": [] } }""", "must be a non-empty array")]
    [InlineData("""{ "style": "slices", "shared": { "allowedFolders": ["Messaging/Bus"] } }""", "not a single folder name")]
    [InlineData("""{ "style": "slices", "shared": { "allowedFolders": ["Messaging", "Messaging"] } }""", "twice")]
    [InlineData("""{ "style": "slices", "shared": { "allowedFolder": ["Messaging"] } }""", "unknown key 'allowedFolder'")]
    [InlineData("""{ "style": "slices", "modules": [], "shared": { "allowedFolders": ["Messaging"] } }""", "unknown key 'modules'")]
    public void An_invalid_file_fails_explicitly(string content, string reason)
    {
        var path = Path.Combine(_directory, "hardslice.json");
        File.WriteAllText(path, content);

        var message = Should.Throw<InvalidOperationException>(() => HardSliceConfiguration.Load(path)).Message;

        message.ShouldStartWith("hardslice.json: ");
        message.ShouldContain(reason);
        message.ShouldContain("Doc: 'docs/rules/HS017.md'.");
    }
}
