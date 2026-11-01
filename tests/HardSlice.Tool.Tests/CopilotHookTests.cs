using System.Text.Json;
using System.Text.Json.Nodes;
using static HardSlice.Tool.Tests.HookHarness;

namespace HardSlice.Tool.Tests;

// dotnet hardslice hook copilot <event>, fed with the payloads that Copilot CLI sent during the trials of PB-034. Copilot reads
// the answer of its hooks in JSON on the standard output with the code 0: its code 2 after a tool only warns the user.
public sealed class CopilotHookTests : IDisposable
{
    private readonly HookHarness _hook = new();

    public void Dispose() => _hook.Dispose();

    [Theory]
    [InlineData("copilot/postToolUse.edit.json")]
    [InlineData("copilot/postToolUse.create.json", "src/Demo.Domain/Bookings/RoomOpening.cs")]
    public void After_the_edit_of_a_cs_file_that_breaks_the_build_the_errors_reach_the_agent_as_additional_context(string fixture, string? file = null)
    {
        var runner = new FakeRunner().Failing(IncrementalBuild, 1, Hs010);
        var payload = file is null ? _hook.Payload(fixture) : WithPath(fixture, $"{_hook.Root}/{file}");

        var (exitCode, output, error) = Hook("postToolUse", payload, runner);

        exitCode.ShouldBe(0);
        runner.Commands.ShouldHaveSingleItem().ShouldStartWith(IncrementalBuild);
        var context = Answer(output, "additionalContext");
        context.ShouldStartWith($"HardSlice: the build fails after your edit of '{file ?? "src/Demo.Domain/Bookings/BookingStatus.cs"}'.");
        context.ShouldContain(Hs010);
        error.ShouldBeEmpty();
    }

    [Fact]
    public void After_the_edit_of_a_cs_file_that_builds_the_agent_receives_nothing()
    {
        var runner = new FakeRunner();

        var (exitCode, output, error) = Hook("postToolUse", _hook.Payload("copilot/postToolUse.edit.json"), runner);

        exitCode.ShouldBe(0);
        runner.Commands.ShouldHaveSingleItem();
        output.ShouldBeEmpty();
        error.ShouldBeEmpty();
    }

    // A postToolUse follows every tool, a read (view) or a shell command (bash) too: only edit and create build.
    [Theory]
    [InlineData("copilot/postToolUse.create.json", null)]
    [InlineData("copilot/postToolUse.edit.json", "view")]
    [InlineData("copilot/postToolUse.edit.json", "bash")]
    public void After_another_tool_or_a_file_other_than_cs_nothing_is_built(string fixture, string? tool)
    {
        var runner = new FakeRunner().Failing(IncrementalBuild, 1, Hs010);
        var payload = JsonNode.Parse(_hook.Payload(fixture))!;
        if (tool is not null)
            payload["toolName"] = tool;

        var (exitCode, output, _) = Hook("postToolUse", payload.ToJsonString(), runner);

        exitCode.ShouldBe(0);
        runner.Commands.ShouldBeEmpty();
        output.ShouldBeEmpty();
    }

    // Copilot appends additionalContext up to 10 KB: longer errors are cut and say where to find the rest.
    [Fact]
    public void After_an_edit_errors_longer_than_Copilot_keeps_are_cut()
    {
        var errors = string.Join('\n', Enumerable.Repeat(Hs010, 60));
        var runner = new FakeRunner().Failing(IncrementalBuild, 1, errors);

        var (_, output, _) = Hook("postToolUse", _hook.Payload("copilot/postToolUse.edit.json"), runner);

        var context = Answer(output, "additionalContext");
        context.Length.ShouldBeLessThan(CopilotHooks.AdditionalContextLimit + 100);
        context.ShouldEndWith("[HardSlice: errors cut here. Run scripts/check.sh --build to see all of them.]");
    }

    [Theory]
    [InlineData("scripts/check.sh", "scripts/")]
    [InlineData("src/Demo/Directory.Build.props", "Directory.Build.props")]
    [InlineData(".github/hooks/hardslice.json", ".github/hooks/")]
    [InlineData(".claude/settings.json", ".claude/")]
    public void The_edit_of_a_protected_file_is_denied_with_the_reason(string file, string entry)
    {
        var runner = new FakeRunner();

        var (exitCode, output, error) = Hook("preToolUse", WithPath("copilot/preToolUse.edit.json", $"{_hook.Root}/{file}"), runner);

        exitCode.ShouldBe(0);
        runner.Commands.ShouldBeEmpty();
        Answer(output, "permissionDecision").ShouldBe("deny");
        Answer(output, "permissionDecisionReason").ShouldStartWith($"HardSlice: '{file}' is protected ({entry}): agents never edit it");
        Answer(output, "permissionDecisionReason").ShouldContain("\"Forbidden\" of AGENTS.md");
        error.ShouldBeEmpty();
    }

    // A file that is not protected, and a shell command, which names no file the hook can read, are left to Copilot.
    [Theory]
    [InlineData("copilot/preToolUse.edit.json")]
    [InlineData("copilot/preToolUse.bash.json")]
    public void The_edit_of_the_code_and_a_shell_command_are_left_to_Copilot(string fixture)
    {
        var runner = new FakeRunner();

        var (exitCode, output, error) = Hook("preToolUse", _hook.Payload(fixture), runner);

        exitCode.ShouldBe(0);
        runner.Commands.ShouldBeEmpty();
        output.ShouldBeEmpty();
        error.ShouldBeEmpty();
    }

    // Copilot has no message to the user: a passing check lets the agent finish without a word.
    [Fact]
    public void Before_handing_back_a_passing_check_lets_the_agent_finish()
    {
        var runner = new FakeRunner();

        var (exitCode, output, error) = Hook("agentStop", _hook.Payload("copilot/agentStop.json"), runner);

        exitCode.ShouldBe(0);
        runner.Commands.ShouldContain(FullBuild);
        output.ShouldBeEmpty();
        error.ShouldBeEmpty();
    }

    [Fact]
    public void Before_handing_back_a_failing_check_blocks_the_end_three_times_for_the_same_failure()
    {
        var runner = new FakeRunner().Failing(FullBuild, 1, Hs010);

        var first = Hook("agentStop", _hook.Payload("copilot/agentStop.json"), runner);
        var second = Hook("agentStop", _hook.Payload("copilot/agentStop.active.json"), runner);
        var third = Hook("agentStop", _hook.Payload("copilot/agentStop.active.json"), runner);
        var fourth = Hook("agentStop", _hook.Payload("copilot/agentStop.active.json"), runner);

        first.ExitCode.ShouldBe(0);
        Answer(first.Output, "decision").ShouldBe("block");
        Answer(first.Output, "reason").ShouldStartWith("HardSlice: the check fails, so the work is not done.");
        Answer(first.Output, "reason").ShouldContain("(relaunch 1 of 3 for this failure)");
        Answer(first.Output, "reason").ShouldContain(Hs010);
        Answer(second.Output, "reason").ShouldContain("(relaunch 2 of 3 for this failure)");
        Answer(third.Output, "reason").ShouldContain("This is the last relaunch for this failure.");
        fourth.ExitCode.ShouldBe(0);
        fourth.Output.ShouldBeEmpty();
        Directory.EnumerateFiles(_hook.State).ShouldBeEmpty();
        new[] { first.Error, second.Error, third.Error, fourth.Error }.ShouldAllBe(error => error.Length == 0);
    }

    // Copilot sets COPILOT_CLI for every hook it runs: the variable that turns the hooks of Claude Code off leaves its own on.
    [Fact]
    public void The_hooks_of_Copilot_run_with_the_variable_that_Copilot_sets()
    {
        var runner = new FakeRunner().Failing(IncrementalBuild, 1, Hs010);

        var (_, output, _) = _hook.Run("copilot", "postToolUse", _hook.Payload("copilot/postToolUse.edit.json"), runner, "COPILOT_CLI");

        Answer(output, "additionalContext").ShouldContain(Hs010);
    }

    [Fact]
    public void A_payload_that_is_not_JSON_exits_1_without_running_anything()
    {
        var runner = new FakeRunner();

        var (exitCode, _, error) = Hook("agentStop", "not json", runner);

        exitCode.ShouldBe(1);
        runner.Commands.ShouldBeEmpty();
        error.ShouldStartWith("error: dotnet hardslice hook copilot agentStop reads the JSON payload of Copilot CLI on its standard input");
    }

    // One hook per event with the same command for bash and PowerShell, the edit hooks on the tools that Copilot used for its
    // edits during the trials of PB-034, and a timeout above the budget of each verification: the default of 30 s is too short.
    [Theory]
    [InlineData("preToolUse", "edit|create", 30)]
    [InlineData("postToolUse", "edit|create", 120)]
    [InlineData("agentStop", null, 600)]
    public void The_template_declares_each_hook_with_its_command(string eventName, string? matcher, int timeout)
    {
        using var settings = JsonDocument.Parse(File.ReadAllText(Path.Combine(Repository.Template, ".github", "hooks", "hardslice.json")));
        var hooks = settings.RootElement.GetProperty("hooks");

        settings.RootElement.GetProperty("version").GetInt32().ShouldBe(1);
        hooks.EnumerateObject().Select(hook => hook.Name).ShouldBe(new CopilotHooks().Events);
        var hook = hooks.GetProperty(eventName).EnumerateArray().ShouldHaveSingleItem();
        hook.GetProperty("type").GetString().ShouldBe("command");
        (hook.TryGetProperty("matcher", out var declared) ? declared.GetString() : null).ShouldBe(matcher);
        hook.GetProperty("bash").GetString().ShouldBe($"dotnet hardslice hook copilot {eventName}");
        hook.GetProperty("powershell").GetString().ShouldBe($"dotnet hardslice hook copilot {eventName}");
        hook.GetProperty("timeoutSec").GetInt32().ShouldBe(timeout);
    }

    private (int ExitCode, string Output, string Error) Hook(string eventName, string payload, FakeRunner runner) =>
        _hook.Run("copilot", eventName, payload, runner);

    private static string Answer(string output, string field) => JsonNode.Parse(output)![field]!.GetValue<string>();

    private string WithPath(string fixture, string path)
    {
        var node = JsonNode.Parse(_hook.Payload(fixture))!;
        node["toolArgs"]!["path"] = path;
        return node.ToJsonString();
    }
}
