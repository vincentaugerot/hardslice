using System.Text.Json;
using System.Text.Json.Nodes;
using static HardSlice.Tool.Tests.HookHarness;

namespace HardSlice.Tool.Tests;

// dotnet hardslice hook codex <event>, fed with the payloads that Codex sent during the trials of PB-034: its edits go through
// apply_patch, whose patch names each file it adds, updates, deletes or moves; the code 2 and the standard error are what the agent receives.
public sealed class CodexHookTests : IDisposable
{
    private readonly HookHarness _hook = new();

    public void Dispose() => _hook.Dispose();

    [Fact]
    public void After_a_patch_of_a_cs_file_that_breaks_the_build_the_errors_reach_the_agent_with_code_2()
    {
        var runner = new FakeRunner().Failing(IncrementalBuild, 1, Hs010);

        var (exitCode, output, error) = Hook("PostToolUse", _hook.Payload("codex/PostToolUse.apply_patch.json"), runner);

        exitCode.ShouldBe(2);
        runner.Commands.ShouldHaveSingleItem().ShouldStartWith(IncrementalBuild);
        error.ShouldStartWith("HardSlice: the build fails after your edit of 'src/Demo.Domain/Bookings/BookingStatus.cs'.");
        error.ShouldContain(Hs010);
        output.ShouldBeEmpty();
    }

    [Fact]
    public void After_a_patch_of_a_cs_file_that_builds_the_agent_receives_nothing()
    {
        var runner = new FakeRunner();

        var (exitCode, output, error) = Hook("PostToolUse", _hook.Payload("codex/PostToolUse.apply_patch.json"), runner);

        exitCode.ShouldBe(0);
        runner.Commands.ShouldHaveSingleItem().ShouldStartWith(IncrementalBuild);
        output.ShouldBeEmpty();
        error.ShouldBeEmpty();
    }

    [Fact]
    public void After_a_patch_that_adds_a_file_other_than_cs_nothing_is_built()
    {
        var runner = new FakeRunner().Failing(IncrementalBuild, 1, Hs010);

        var (exitCode, _, error) = Hook("PostToolUse", _hook.Payload("codex/PostToolUse.apply_patch.add.json"), runner);

        exitCode.ShouldBe(0);
        runner.Commands.ShouldBeEmpty();
        error.ShouldBeEmpty();
    }

    // One patch may touch several files: one build, which names every .cs file of the patch, deleted or moved ones included.
    [Fact]
    public void After_a_patch_of_several_files_one_build_names_each_cs_file()
    {
        var runner = new FakeRunner().Failing(IncrementalBuild, 1, Hs010);
        var patch = Patch(
            $"*** Add File: {_hook.Root}/README.md", "+notes",
            $"*** Update File: {_hook.Root}/src/Demo/Program.cs", "@@", "-a", "+b",
            "*** Delete File: src/Demo/Features/Old/OldHandler.cs",
            $"*** Update File: {_hook.Root}/src/Demo/Features/A.cs", $"*** Move to: {_hook.Root}/src/Demo/Features/B.cs", "@@");

        var (exitCode, _, error) = Hook("PostToolUse", WithPatch("codex/PostToolUse.apply_patch.json", patch), runner);

        exitCode.ShouldBe(2);
        runner.Commands.ShouldHaveSingleItem();
        error.ShouldStartWith(
            "HardSlice: the build fails after your edit of 'src/Demo/Program.cs', 'src/Demo/Features/Old/OldHandler.cs', 'src/Demo/Features/A.cs', 'src/Demo/Features/B.cs'.");
    }

    [Theory]
    [InlineData("*** Update File: {root}/scripts/check.sh", "scripts/check.sh", "scripts/")]
    [InlineData("*** Add File: {root}/src/Demo/.editorconfig", "src/Demo/.editorconfig", ".editorconfig")]
    [InlineData("*** Delete File: tests/Demo.ArchitectureTests/SliceTests.cs", "tests/Demo.ArchitectureTests/SliceTests.cs", "tests/Demo.ArchitectureTests/")]
    [InlineData("*** Update File: {root}/.codex/hooks.json", ".codex/hooks.json", ".codex/")]
    [InlineData("*** Update File: {root}/.github/hooks/hardslice.json", ".github/hooks/hardslice.json", ".github/hooks/")]
    [InlineData("*** Update File: {root}/src/Demo/Program.cs\n*** Move to: {root}/hardslice.json", "hardslice.json", "hardslice.json")]
    [InlineData("*** Add File: {root}/src/Demo/.globalconfig", "src/Demo/.globalconfig", ".globalconfig")]
    [InlineData("*** Add File: {root}/Directory.Build.rsp", "Directory.Build.rsp", "Directory.Build.rsp")]
    [InlineData("*** Add File: {root}/.agents/skills/add-slice/SKILL.md", ".agents/skills/add-slice/SKILL.md", ".agents/")]
    public void A_patch_that_touches_a_protected_file_is_refused_with_the_reason(string header, string file, string entry)
    {
        var runner = new FakeRunner();
        var patch = Patch($"*** Update File: {_hook.Root}/src/Demo/Program.cs", "@@", header.Replace("{root}", _hook.Root), "@@");

        var (exitCode, output, error) = Hook("PreToolUse", WithPatch("codex/PreToolUse.apply_patch.json", patch), runner);

        exitCode.ShouldBe(2);
        runner.Commands.ShouldBeEmpty();
        error.ShouldStartWith($"HardSlice: '{file}' is protected ({entry}): agents never edit it");
        error.ShouldContain("\"Forbidden\" of AGENTS.md");
        output.ShouldBeEmpty();
    }

    [Fact]
    public void A_patch_of_the_code_is_allowed()
    {
        var runner = new FakeRunner();

        var (exitCode, output, error) = Hook("PreToolUse", _hook.Payload("codex/PreToolUse.apply_patch.json"), runner);

        exitCode.ShouldBe(0);
        runner.Commands.ShouldBeEmpty();
        output.ShouldBeEmpty();
        error.ShouldBeEmpty();
    }

    // Codex expects JSON on the standard output of a Stop hook that exits 0: the passing check is shown to the user.
    [Fact]
    public void Before_handing_back_a_passing_check_lets_the_agent_finish_and_tells_the_user()
    {
        var runner = new FakeRunner();

        var (exitCode, output, error) = Hook("Stop", _hook.Payload("codex/Stop.json"), runner);

        exitCode.ShouldBe(0);
        runner.Commands.ShouldContain(FullBuild);
        JsonNode.Parse(output)!["systemMessage"]!.GetValue<string>().ShouldBe("HardSlice: the check passed.");
        error.ShouldBeEmpty();
    }

    [Fact]
    public void Before_handing_back_a_failing_check_keeps_the_agent_working_three_times_for_the_same_failure()
    {
        var runner = new FakeRunner().Failing(FullBuild, 1, Hs010);

        var first = Hook("Stop", _hook.Payload("codex/Stop.json"), runner);
        var second = Hook("Stop", _hook.Payload("codex/Stop.active.json"), runner);
        var third = Hook("Stop", _hook.Payload("codex/Stop.active.json"), runner);
        var fourth = Hook("Stop", _hook.Payload("codex/Stop.active.json"), runner);

        first.ExitCode.ShouldBe(2);
        first.Error.ShouldStartWith("HardSlice: the check fails, so the work is not done.");
        first.Error.ShouldContain("(relaunch 1 of 3 for this failure)");
        first.Error.ShouldContain(Hs010);
        second.Error.ShouldContain("(relaunch 2 of 3 for this failure)");
        third.Error.ShouldContain("This is the last relaunch for this failure.");
        fourth.ExitCode.ShouldBe(0);
        JsonNode.Parse(fourth.Output)!["systemMessage"]!.GetValue<string>()
            .ShouldStartWith("HardSlice: the check still fails after 3 relaunches for the same failure");
        Directory.EnumerateFiles(_hook.State).ShouldBeEmpty();
    }

    // The relaunches are counted per CLI: a session of Claude Code with the same identifier does not share them.
    [Fact]
    public void Before_handing_back_the_relaunches_of_another_CLI_do_not_count()
    {
        var runner = new FakeRunner().Failing(FullBuild, 1, Hs010);
        var session = JsonNode.Parse(_hook.Payload("codex/Stop.json"))!["session_id"]!.GetValue<string>();
        _hook.Run("claude", "Stop", WithSession("claude/Stop.json", session), runner);
        _hook.Run("claude", "Stop", WithSession("claude/Stop.active.json", session), runner).Error.ShouldContain("(relaunch 2 of 3 for this failure)");

        Hook("Stop", _hook.Payload("codex/Stop.json"), runner).Error.ShouldContain("(relaunch 1 of 3 for this failure)");
        Hook("Stop", _hook.Payload("codex/Stop.active.json"), runner).Error.ShouldContain("(relaunch 2 of 3 for this failure)");
    }

    // Copilot CLI does not read .codex/hooks.json: its variable does not turn these hooks off.
    [Fact]
    public void The_variable_of_Copilot_CLI_does_not_turn_the_hooks_of_Codex_off()
    {
        var runner = new FakeRunner().Failing(IncrementalBuild, 1, Hs010);

        var (exitCode, _, _) = _hook.Run("codex", "PostToolUse", _hook.Payload("codex/PostToolUse.apply_patch.json"), runner, "COPILOT_CLI");

        exitCode.ShouldBe(2);
    }

    [Fact]
    public void A_payload_that_is_not_JSON_exits_1_without_running_anything()
    {
        var runner = new FakeRunner();

        var (exitCode, _, error) = Hook("Stop", "not json", runner);

        exitCode.ShouldBe(1);
        runner.Commands.ShouldBeEmpty();
        error.ShouldStartWith("error: dotnet hardslice hook codex Stop reads the JSON payload of Codex on its standard input");
    }

    // The template declares one hook per event, with a command that never changes from one version of HardSlice to the next:
    // Codex records the approval of a hook against its definition, and a changed hook is skipped until approved again in /hooks.
    [Theory]
    [InlineData("PreToolUse", "Edit|Write", 30)]
    [InlineData("PostToolUse", "Edit|Write", 120)]
    [InlineData("Stop", null, 600)]
    public void The_template_declares_each_hook_with_its_command(string eventName, string? matcher, int timeout)
    {
        using var settings = JsonDocument.Parse(File.ReadAllText(Path.Combine(Repository.Template, ".codex", "hooks.json")));
        var hooks = settings.RootElement.GetProperty("hooks");

        settings.RootElement.EnumerateObject().Select(property => property.Name).ShouldBe(["hooks"]);
        hooks.EnumerateObject().Select(hook => hook.Name).ShouldBe(new CodexHooks().Events);
        var group = hooks.GetProperty(eventName).EnumerateArray().ShouldHaveSingleItem();
        (group.TryGetProperty("matcher", out var declared) ? declared.GetString() : null).ShouldBe(matcher);
        var hook = group.GetProperty("hooks").EnumerateArray().ShouldHaveSingleItem();
        hook.EnumerateObject().Select(property => property.Name).ShouldBe(["type", "command", "timeout"]);
        hook.GetProperty("type").GetString().ShouldBe("command");
        hook.GetProperty("command").GetString().ShouldBe($"dotnet hardslice hook codex {eventName}");
        hook.GetProperty("timeout").GetInt32().ShouldBe(timeout);
    }

    private (int ExitCode, string Output, string Error) Hook(string eventName, string payload, FakeRunner runner) =>
        _hook.Run("codex", eventName, payload, runner);

    private static string Patch(params string[] lines) => string.Join('\n', ["*** Begin Patch", .. lines, "*** End Patch"]);

    private string WithSession(string fixture, string session)
    {
        var node = JsonNode.Parse(_hook.Payload(fixture))!;
        node["session_id"] = session;
        return node.ToJsonString();
    }

    private string WithPatch(string fixture, string patch)
    {
        var node = JsonNode.Parse(_hook.Payload(fixture))!;
        node["tool_input"]!["command"] = patch;
        return node.ToJsonString();
    }
}
