using System.Text.Json;
using System.Text.RegularExpressions;

namespace HardSlice.Tool.Tests;

// The hook payloads recorded by PB-034 feed the tests of the hook adapters: docs/agents/support.md lists them, and each one is the payload its name announces.
// A fixture is named <cli>/<event>[.<tool>][.<variant>].json, the tool being given for tool events only.
public sealed partial class AgentHookFixturesTests
{
    private static readonly string Fixtures = Path.Combine(Repository.Root, "tests", "HardSlice.Tool.Tests", "Fixtures", "hooks");

    private static readonly string SupportDocument = Path.Combine(Repository.Root, "docs", "agents", "support.md");

    public static TheoryData<string> AllFixtures() => [.. RecordedFixtures()];

    [Fact]
    public void The_support_document_lists_every_recorded_fixture_and_no_other()
    {
        var cited = CitedFixture().Matches(File.ReadAllText(SupportDocument))
            .Select(match => match.Groups["fixture"].Value)
            .Distinct()
            .Order(StringComparer.Ordinal);

        RecordedFixtures().ShouldNotBeEmpty();
        cited.ShouldBe(RecordedFixtures());
    }

    [Theory]
    [MemberData(nameof(AllFixtures))]
    public void A_fixture_is_the_payload_its_name_announces(string fixture)
    {
        using var payload = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, fixture)));
        var root = payload.RootElement;
        var name = Path.GetFileNameWithoutExtension(fixture).Split('.');
        var eventName = name[0];
        var tool = eventName.EndsWith("ToolUse", StringComparison.OrdinalIgnoreCase) ? name[1] : null;

        if (fixture.StartsWith("copilot/", StringComparison.Ordinal))
        {
            // Copilot CLI payloads carry no event name: a tool event names its tool, an end of turn its reason.
            if (tool is null)
                root.GetProperty("stopReason").GetString().ShouldBe("end_turn", fixture);
            else
                root.GetProperty("toolName").GetString().ShouldBe(tool, fixture);
            return;
        }

        root.GetProperty("hook_event_name").GetString().ShouldBe(eventName, fixture);
        if (tool is not null)
            root.GetProperty("tool_name").GetString().ShouldBe(tool, fixture);
    }

    [Theory]
    [MemberData(nameof(AllFixtures))]
    public void A_fixture_keeps_no_path_of_the_machine_that_recorded_it(string fixture)
    {
        var text = File.ReadAllText(Path.Combine(Fixtures, fixture));

        text.ShouldNotContain("/tmp/claude-", Case.Sensitive, fixture);
        text.ShouldNotContain("/root/", Case.Sensitive, fixture);
    }

    private static string[] RecordedFixtures() =>
        [.. Directory.GetFiles(Fixtures, "*.json", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(Fixtures, file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)];

    [GeneratedRegex(@"`(?<fixture>[a-z-]+/[A-Za-z_.]+\.json)`")]
    private static partial Regex CitedFixture();
}
