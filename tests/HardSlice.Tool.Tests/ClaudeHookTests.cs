using System.Text.Json;
using System.Text.Json.Nodes;

namespace HardSlice.Tool.Tests;

// dotnet hardslice hook claude <event>, fed with the payloads that Claude Code and Copilot CLI sent during the trials of PB-034,
// their solution moved to a temporary one: the code 2 and the standard error are what the agent receives.
public sealed class ClaudeHookTests : IDisposable
{
    private const string IncrementalBuild = "dotnet build Demo.slnx --no-restore";
    private const string FullBuild = "dotnet build Demo.slnx";
    private const string RecordedRoot = "/home/dev/Demo";

    private const string Hs010 =
        "/home/dev/Demo/src/Demo.Domain/Bookings/BookingStatus.cs(5,12): error HS010: 'BookingStatus.Today' in domain project 'Demo.Domain' reads 'DateTime.Today'. "
        + "Fix: pass the instant as a parameter. Skill: fix-architecture-violation. Doc: 'docs/rules/HS010.md'.";

    private static readonly string Fixtures = Path.Combine(Repository.Root, "tests", "HardSlice.Tool.Tests", "Fixtures", "hooks");

    private readonly TemporarySolution _solution = new();
    private readonly string _state = Directory.CreateTempSubdirectory("hardslice-hook-state-").FullName;

    public void Dispose()
    {
        _solution.Dispose();
        Directory.Delete(_state, recursive: true);
    }

    [Theory]
    [InlineData("claude/PostToolUse.Edit.json")]
    [InlineData("claude/PostToolUse.Write.json")]
    public void After_the_edit_of_a_cs_file_that_breaks_the_build_the_errors_reach_the_agent_with_code_2(string fixture)
    {
        var runner = new FakeRunner().Failing(IncrementalBuild, 1, Hs010);

        var (exitCode, output, error) = Hook("PostToolUse", Payload(fixture), runner);

        exitCode.ShouldBe(2);
        runner.Commands.ShouldHaveSingleItem().ShouldStartWith(IncrementalBuild);
        error.ShouldStartWith("HardSlice: the build fails after your edit of 'src/Demo.Domain/Bookings/");
        error.ShouldContain(Hs010);
        error.ShouldContain(CheckCommand.BuildModeReduction);
        output.ShouldBeEmpty();
    }

    [Fact]
    public void After_the_edit_of_a_cs_file_that_builds_the_agent_receives_nothing()
    {
        var runner = new FakeRunner();

        var (exitCode, output, error) = Hook("PostToolUse", Payload("claude/PostToolUse.Edit.json"), runner);

        exitCode.ShouldBe(0);
        runner.Commands.ShouldHaveSingleItem().ShouldStartWith(IncrementalBuild);
        output.ShouldBeEmpty();
        error.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("README.md")]
    [InlineData("src/Demo/appsettings.json")]
    [InlineData("src/Demo/Demo.csproj")]
    [InlineData("docs/rules/HS016.cs.md")]
    public void After_the_edit_of_a_file_other_than_cs_nothing_is_built(string file)
    {
        var runner = new FakeRunner().Failing(IncrementalBuild, 1, Hs010);

        var (exitCode, _, error) = Hook("PostToolUse", WithFile(Payload("claude/PostToolUse.Edit.json"), $"{_solution.Root}/{file}"), runner);

        exitCode.ShouldBe(0);
        runner.Commands.ShouldBeEmpty();
        error.ShouldBeEmpty();
    }

    [Fact]
    public void After_the_edit_of_a_cs_file_outside_the_solution_nothing_is_built()
    {
        var runner = new FakeRunner();

        var (exitCode, _, _) = Hook("PostToolUse", WithFile(Payload("claude/PostToolUse.Edit.json"), "/home/dev/Other/Program.cs"), runner);

        exitCode.ShouldBe(0);
        runner.Commands.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(".editorconfig", ".editorconfig")]
    [InlineData("src/Demo/.editorconfig", ".editorconfig")]
    [InlineData("hardslice.json", "hardslice.json")]
    [InlineData("Directory.Build.props", "Directory.Build.props")]
    [InlineData("tests/Demo.Tests/Directory.Build.props", "Directory.Build.props")]
    [InlineData("Directory.Build.targets", "Directory.Build.targets")]
    [InlineData("Directory.Packages.props", "Directory.Packages.props")]
    [InlineData(".config/dotnet-tools.json", ".config/dotnet-tools.json")]
    [InlineData("scripts/check.sh", "scripts/")]
    [InlineData("scripts/check.ps1", "scripts/")]
    [InlineData("tests/Demo.ArchitectureTests/SliceTests.cs", "tests/Demo.ArchitectureTests/")]
    [InlineData(".claude/settings.json", ".claude/")]
    [InlineData(".claude/skills/add-slice/SKILL.md", ".claude/")]
    [InlineData(".githooks/pre-commit", ".githooks/")]
    [InlineData(".github/workflows/hardslice.yml", ".github/workflows/hardslice.yml")]
    [InlineData("src/../scripts/check.sh", "scripts/")]
    [InlineData("SCRIPTS/check.sh", "scripts/")]
    public void The_edit_of_a_protected_file_is_refused_with_the_reason(string file, string entry)
    {
        var runner = new FakeRunner();

        var (exitCode, _, error) = Hook("PreToolUse", WithFile(Payload("claude/PreToolUse.Edit.json"), $"{_solution.Root}/{file}"), runner);

        exitCode.ShouldBe(2);
        runner.Commands.ShouldBeEmpty();
        error.ShouldStartWith($"HardSlice: '{file.Replace("src/../", "")}' is protected ({entry}): agents never edit it");
        error.ShouldContain("\"Forbidden\" of AGENTS.md");
        error.ShouldContain("stop and explain the conflict");
    }

    [Theory]
    [InlineData("claude/PreToolUse.Edit.json")]
    [InlineData("claude/PreToolUse.Write.json")]
    public void The_edit_of_a_file_of_the_code_or_of_its_tests_is_allowed(string fixture)
    {
        var runner = new FakeRunner();

        var (exitCode, output, error) = Hook("PreToolUse", Payload(fixture), runner);

        exitCode.ShouldBe(0);
        runner.Commands.ShouldBeEmpty();
        output.ShouldBeEmpty();
        error.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("src/Demo/scripts/Seed.cs")]
    [InlineData("tests/Demo.Tests/Features/Bookings/ConfirmBooking/ConfirmBookingTests.cs")]
    [InlineData("tests/Demo.ArchitectureTestsHelpers/Helper.cs")]
    [InlineData("docs/hardslice.json.md")]
    [InlineData("src/Demo/.config/dotnet-tools.json")]
    [InlineData(".github/workflows/deploy.yml")]
    [InlineData("/home/dev/Other/scripts/check.sh")]
    public void A_file_that_only_looks_like_a_protected_one_is_not_refused(string file)
    {
        var path = file.StartsWith('/') ? file : $"{_solution.Root}/{file}";

        var (exitCode, _, error) = Hook("PreToolUse", WithFile(Payload("claude/PreToolUse.Edit.json"), path), new FakeRunner());

        exitCode.ShouldBe(0);
        error.ShouldBeEmpty();
    }

    // Claude Code on Windows names files with backslashes, whatever the case of the drive and of the folders.
    [Fact]
    public void A_Windows_path_is_read_with_its_separators()
    {
        var windowsRoot = _solution.Root.Replace('/', '\\');
        var payload = WithFile(Payload("claude/PreToolUse.Edit.json"), windowsRoot + @"\scripts\check.ps1");

        var (exitCode, _, error) = Hook("PreToolUse", payload, new FakeRunner());

        exitCode.ShouldBe(2);
        error.ShouldStartWith("HardSlice: 'scripts/check.ps1' is protected (scripts/)");

        var runner = new FakeRunner().Failing(IncrementalBuild, 1, Hs010);
        Hook("PostToolUse", WithFile(Payload("claude/PostToolUse.Edit.json"), windowsRoot + @"\src\Demo.Domain\Bookings\BookingStatus.CS"), runner)
            .ExitCode.ShouldBe(2);
    }

    [Theory]
    [InlineData(@"C:\Users\dev\Demo", @"C:\Users\dev\Demo\Directory.Build.props", "Directory.Build.props")]
    [InlineData(@"C:\Users\dev\Demo", @"c:\users\DEV\demo\tests\Demo.ArchitectureTests\SliceTests.cs", "tests/Demo.ArchitectureTests/SliceTests.cs")]
    [InlineData(@"C:\Users\dev\Demo\", @"C:/Users/dev/Demo/src/Demo/.\..\..\scripts\check.ps1", "scripts/check.ps1")]
    [InlineData(@"C:\Users\dev\Demo", @"scripts\check.ps1", "scripts/check.ps1")]
    [InlineData(@"C:\Users\dev\Demo", @"C:\Users\dev\DemoOther\scripts\check.ps1", null)]
    [InlineData(@"C:\Users\dev\Demo", @"D:\Users\dev\Demo\scripts\check.ps1", null)]
    [InlineData(@"C:\Users\dev\Demo", @"C:\Users\dev\Demo\..\Other\hardslice.json", null)]
    [InlineData("/home/dev/Demo", "/home/dev/Demo/src/Demo/Program.cs", "src/Demo/Program.cs")]
    [InlineData("/home/dev/Demo", "/home/dev/Demo", null)]
    public void A_path_is_relative_to_the_root_of_the_solution_whatever_its_separators_and_case(string root, string path, string? relative) =>
        SolutionPath.Relative(root, path, root).ShouldBe(relative);

    [Fact]
    public void Before_handing_back_a_passing_check_lets_the_agent_finish()
    {
        var runner = new FakeRunner();

        var (exitCode, output, error) = Hook("Stop", Payload("claude/Stop.json"), runner);

        exitCode.ShouldBe(0);
        runner.Commands.ShouldContain(FullBuild);
        runner.Commands.ShouldContain(command => command.StartsWith("dotnet test --project tests/Demo.IntegrationTests/", StringComparison.Ordinal));
        JsonNode.Parse(output)!["systemMessage"]!.GetValue<string>().ShouldBe("HardSlice: the check passed.");
        error.ShouldBeEmpty();
    }

    [Fact]
    public void Before_handing_back_a_failing_check_keeps_the_agent_working_three_times_for_the_same_failure_then_lets_it_say_so()
    {
        var runner = new FakeRunner().Failing(FullBuild, 1, Hs010);

        var first = Hook("Stop", Payload("claude/Stop.json"), runner);
        var second = Hook("Stop", Payload("claude/Stop.active.json"), runner);
        // The same failure on another line, after another duration, is the same failure.
        runner.Failing(FullBuild, 1, Hs010.Replace("(5,12)", "(7,12)") + "\nTime Elapsed 00:00:04.21");
        var third = Hook("Stop", Payload("claude/Stop.active.json"), runner);
        var fourth = Hook("Stop", Payload("claude/Stop.active.json"), runner);

        first.ExitCode.ShouldBe(2);
        first.Error.ShouldStartWith("HardSlice: the check fails, so the work is not done.");
        first.Error.ShouldContain("(relaunch 1 of 3 for this failure)");
        first.Error.ShouldContain(Hs010);
        first.Error.ShouldNotContain("last relaunch");
        second.ExitCode.ShouldBe(2);
        second.Error.ShouldContain("(relaunch 2 of 3 for this failure)");
        third.ExitCode.ShouldBe(2);
        third.Error.ShouldContain("(relaunch 3 of 3 for this failure)");
        third.Error.ShouldContain("This is the last relaunch for this failure. If you cannot fix it without breaking a rule, stop trying: "
            + "finish by telling the user that the check fails");
        fourth.ExitCode.ShouldBe(0);
        fourth.Error.ShouldBeEmpty();
        JsonNode.Parse(fourth.Output)!["systemMessage"]!.GetValue<string>()
            .ShouldStartWith("HardSlice: the check still fails after 3 relaunches for the same failure, so the work is not done.");
        Directory.EnumerateFiles(_state).ShouldBeEmpty();
    }

    [Fact]
    public void Before_handing_back_another_failure_counts_its_own_relaunches()
    {
        var runner = new FakeRunner().Failing(FullBuild, 1, Hs010);
        Hook("Stop", Payload("claude/Stop.json"), runner);
        Hook("Stop", Payload("claude/Stop.active.json"), runner);
        Hook("Stop", Payload("claude/Stop.active.json"), runner);

        runner.Failing(FullBuild, 1, "src/Demo/Features/Bookings/ConfirmBooking/ConfirmBookingHandler.cs(9,5): error CS1002: ; expected");
        var other = Hook("Stop", Payload("claude/Stop.active.json"), runner);

        other.ExitCode.ShouldBe(2);
        other.Error.ShouldContain("(relaunch 1 of 3 for this failure)");
        other.Error.ShouldContain("error CS1002");
    }

    // A new turn, where no end hook is active, starts the count again, and a passing check forgets it.
    [Fact]
    public void Before_handing_back_a_new_turn_or_a_passing_check_starts_the_count_again()
    {
        var runner = new FakeRunner().Failing(FullBuild, 1, Hs010);
        Hook("Stop", Payload("claude/Stop.json"), runner);
        Hook("Stop", Payload("claude/Stop.active.json"), runner);

        Hook("Stop", Payload("claude/Stop.json"), runner).Error.ShouldContain("(relaunch 1 of 3 for this failure)");

        Hook("Stop", Payload("claude/Stop.active.json"), new FakeRunner()).ExitCode.ShouldBe(0);
        Directory.EnumerateFiles(_state).ShouldBeEmpty();
        Hook("Stop", Payload("claude/Stop.active.json"), runner).Error.ShouldContain("(relaunch 1 of 3 for this failure)");
    }

    [Fact]
    public void Before_handing_back_the_failures_of_the_check_steps_reach_the_agent()
    {
        _solution.Write("src/Demo/Features/Rooms/BookRoom/BookRoomHandler.cs", "// TODO(hardslice): implement BookRoomHandler.\n");
        var runner = new FakeRunner().Failing("docker info", 1);

        var (exitCode, _, error) = Hook("Stop", Payload("claude/Stop.json"), runner);

        exitCode.ShouldBe(2);
        error.ShouldContain(CheckCommand.ContainerRuntimeMissing);
        runner.Commands.ShouldNotContain(command => command.Contains("IntegrationTests", StringComparison.Ordinal));
    }

    // Copilot CLI runs the hooks of .claude/settings.json without handing their code 2 to the agent: they do nothing there.
    [Theory]
    [InlineData("PreToolUse", "copilot-claude-compat/PreToolUse.Edit.json")]
    [InlineData("PostToolUse", "copilot-claude-compat/PostToolUse.Edit.json")]
    [InlineData("Stop", "copilot-claude-compat/Stop.json")]
    [InlineData("PreToolUse", "claude/PreToolUse.Edit.json")]
    public void Under_Copilot_CLI_the_hooks_exit_0_without_running_anything(string eventName, string fixture)
    {
        var runner = new FakeRunner().Failing(IncrementalBuild, 1, Hs010).Failing(FullBuild, 1, Hs010);
        var payload = eventName == "PreToolUse" ? WithFile(Payload(fixture), $"{_solution.Root}/scripts/check.sh") : Payload(fixture);

        var (exitCode, output, error) = Hook(eventName, payload, runner, copilot: true);

        exitCode.ShouldBe(0);
        runner.Commands.ShouldBeEmpty();
        output.ShouldBeEmpty();
        error.ShouldBeEmpty();
    }

    // Any code but 2 is shown to the user and lets the agent go on: a mistake in the hook command never blocks it.
    [Theory]
    [InlineData("hook")]
    [InlineData("hook", "claude")]
    [InlineData("hook", "claude", "SessionStart")]
    [InlineData("hook", "claude", "stop")]
    [InlineData("hook", "gemini", "AfterAgent")]
    [InlineData("hook", "codex", "agentStop")]
    [InlineData("hook", "copilot", "Stop")]
    [InlineData("hook", "claude", "Stop", "--build")]
    public void An_unknown_CLI_or_event_exits_1_without_running_anything(params string[] args)
    {
        var runner = new FakeRunner();
        var error = new StringWriter();

        var exitCode = Cli.Run(args, _solution.Root, runner, new StringWriter(), error, new StringReader("{}"), _ => null, _state);

        exitCode.ShouldBe(1);
        runner.Commands.ShouldBeEmpty();
        error.ToString().ShouldStartWith(
            "error: hook takes a CLI and one of its events: 'hook claude PreToolUse|PostToolUse|Stop', 'hook codex PreToolUse|PostToolUse|Stop', "
            + "'hook copilot preToolUse|postToolUse|agentStop'.");
    }

    [Fact]
    public void A_payload_that_is_not_JSON_exits_1_without_running_anything()
    {
        var runner = new FakeRunner();

        var (exitCode, _, error) = Hook("Stop", "not json", runner);

        exitCode.ShouldBe(1);
        runner.Commands.ShouldBeEmpty();
        error.ShouldStartWith("error: dotnet hardslice hook claude Stop reads the JSON payload of Claude Code on its standard input");
    }

    // The template declares one hook per event, each running the command of its event with a timeout above its budget, and the
    // edit hooks on the tools that Claude Code used for its edits during the trials of PB-034.
    [Theory]
    [InlineData("PreToolUse", "Edit|Write", 30)]
    [InlineData("PostToolUse", "Edit|Write", 120)]
    [InlineData("Stop", null, 600)]
    public void The_template_declares_each_hook_with_its_command(string eventName, string? matcher, int timeout)
    {
        using var settings = JsonDocument.Parse(File.ReadAllText(Path.Combine(Repository.Template, ".claude", "settings.json")));
        var hooks = settings.RootElement.GetProperty("hooks");

        hooks.EnumerateObject().Select(hook => hook.Name).ShouldBe(new ClaudeCodeHooks().Events);
        var group = hooks.GetProperty(eventName).EnumerateArray().ShouldHaveSingleItem();
        (group.TryGetProperty("matcher", out var declared) ? declared.GetString() : null).ShouldBe(matcher);
        var hook = group.GetProperty("hooks").EnumerateArray().ShouldHaveSingleItem();
        hook.GetProperty("type").GetString().ShouldBe("command");
        hook.GetProperty("command").GetString().ShouldBe($"dotnet hardslice hook claude {eventName}");
        hook.GetProperty("timeout").GetInt32().ShouldBe(timeout);
    }

    // The refusal sends the agent to "Forbidden" of AGENTS.md, which names every file that the hook protects.
    [Fact]
    public void Forbidden_in_AGENTS_md_names_every_protected_file()
    {
        var forbidden = File.ReadAllText(Path.Combine(Repository.Template, "AGENTS.md")).Split("## Forbidden")[1];

        ProtectedFiles.Entries("MyApp").ShouldAllBe(entry => forbidden.Contains($"`{entry}`", StringComparison.Ordinal));
    }

    private (int ExitCode, string Output, string Error) Hook(string eventName, string payload, FakeRunner runner, bool copilot = false)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = Cli.Run(
            ["hook", "claude", eventName], _solution.Root, runner, output, error, new StringReader(payload),
            name => copilot && name == "COPILOT_CLI" ? "1" : null, _state);
        return (exitCode, output.ToString(), error.ToString());
    }

    // A recorded payload, with the paths of its solution moved to the temporary one.
    private string Payload(string fixture) =>
        File.ReadAllText(Path.Combine(Fixtures, fixture)).Replace(RecordedRoot + "/", _solution.Root + "/").Replace($"\"{RecordedRoot}\"", $"\"{_solution.Root}\"");

    private static string WithFile(string payload, string file)
    {
        var node = JsonNode.Parse(payload)!;
        var input = node["tool_input"]!.AsObject();
        input[input.ContainsKey("path") ? "path" : "file_path"] = file;
        return node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }
}
