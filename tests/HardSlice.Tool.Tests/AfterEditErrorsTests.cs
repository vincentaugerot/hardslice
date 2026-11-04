using System.Text.Json.Nodes;
using static HardSlice.Tool.Tests.HookHarness;

namespace HardSlice.Tool.Tests;

// After an edit, the hook of each CLI hands the agent the errors of the edited files in full and only counts the others, which the
// next edits of a planned change may fix (Ghostline, "the build hook reports the files not yet edited of a planned change").
// Each recorded payload edits src/Demo.Domain/Bookings/BookingStatus.cs.
public sealed class AfterEditErrorsTests : IDisposable
{
    private const string EditedFile = "src/Demo.Domain/Bookings/BookingStatus.cs";

    private readonly HookHarness _hook = new();

    public void Dispose() => _hook.Dispose();

    public static TheoryData<string, string, string> Adapters() => new()
    {
        { "claude", "PostToolUse", "claude/PostToolUse.Edit.json" },
        { "codex", "PostToolUse", "codex/PostToolUse.apply_patch.json" },
        { "copilot", "postToolUse", "copilot/postToolUse.edit.json" },
    };

    [Theory]
    [MemberData(nameof(Adapters))]
    public void The_errors_of_the_edited_file_are_shown_and_the_others_only_counted(string cli, string eventName, string fixture)
    {
        var inEdited = $"{_hook.Root}/{EditedFile}(5,12): error CS0103: The name 'Today' does not exist in the current context [{_hook.Root}/src/Demo.Domain/Demo.Domain.csproj]";
        var tool = $"CSC : error CS2001: Source file '{_hook.Root}/src/Demo/Gone.cs' could not be found. [{_hook.Root}/src/Demo/Demo.csproj]";
        string[] others =
        [
            $"{_hook.Root}/tests/Demo.Tests/Bookings/BookingStatusTests.cs(14,9): error CS1061: 'BookingStatus' does not contain a definition for 'Today' [{_hook.Root}/tests/Demo.Tests/Demo.Tests.csproj]",
            $"{_hook.Root}/tests/Demo.Tests/Bookings/BookingStatusTests.cs(21,9): error CS1061: 'BookingStatus' does not contain a definition for 'Today' [{_hook.Root}/tests/Demo.Tests/Demo.Tests.csproj]",
            $"src/Demo/Features/Bookings/ConfirmBooking/ConfirmBookingHandler.cs(9,5): error CS1061: 'BookingStatus' does not contain a definition for 'Today' [{_hook.Root}/src/Demo/Demo.csproj]",
        ];
        var runner = new FakeRunner().Failing(IncrementalBuild, 1, string.Join('\n', [others[0], inEdited, others[1], tool, others[2]]));

        var errors = AfterEdit(cli, eventName, fixture, runner);

        errors.ShouldStartWith($"HardSlice: the build fails after your edit of '{EditedFile}'.");
        errors.ShouldContain(CheckCommand.BuildModeReduction);
        errors.ShouldContain(inEdited);
        errors.ShouldContain(tool);
        foreach (var other in others)
            errors.ShouldNotContain(other);
        errors.ShouldContain(
            "HardSlice: 3 more errors in 2 other files, not shown here since the next edits of your change may fix them. "
            + "Once the change is done, run scripts/check.sh --build to see those left: the full check before handing back lists every error.");
    }

    // An edit may break a file it did not touch: when no error is in an edited file, every error is shown, as before.
    [Theory]
    [MemberData(nameof(Adapters))]
    public void Without_an_error_in_the_edited_file_every_error_is_shown(string cli, string eventName, string fixture)
    {
        string[] others =
        [
            $"{_hook.Root}/tests/Demo.Tests/Bookings/BookingStatusTests.cs(14,9): error CS1061: 'BookingStatus' does not contain a definition for 'Today'",
            "src/Demo/Features/Bookings/ConfirmBooking/ConfirmBookingHandler.cs(9,5): error CS1061: 'BookingStatus' does not contain a definition for 'Today'",
        ];
        var runner = new FakeRunner().Failing(IncrementalBuild, 1, string.Join('\n', others));

        var errors = AfterEdit(cli, eventName, fixture, runner);

        foreach (var other in others)
            errors.ShouldContain(other);
        errors.ShouldNotContain("not shown here");
    }

    [Theory]
    [MemberData(nameof(Adapters))]
    public void Errors_in_the_edited_file_alone_are_shown_without_a_count(string cli, string eventName, string fixture)
    {
        var inEdited = $"{_hook.Root}/{EditedFile}(5,12): error CS0103: The name 'Today' does not exist in the current context";
        var runner = new FakeRunner().Failing(IncrementalBuild, 1, inEdited);

        var errors = AfterEdit(cli, eventName, fixture, runner);

        errors.ShouldContain(inEdited);
        errors.ShouldNotContain("not shown here");
    }

    // The full check before handing back keeps every error, whatever the agent edited last.
    [Fact]
    public void The_check_before_handing_back_lists_every_error()
    {
        var inEdited = $"{_hook.Root}/{EditedFile}(5,12): error CS0103: The name 'Today' does not exist in the current context";
        var other = $"{_hook.Root}/tests/Demo.Tests/Bookings/BookingStatusTests.cs(14,9): error CS1061: 'BookingStatus' does not contain a definition for 'Today'";
        var runner = new FakeRunner().Failing(FullBuild, 1, inEdited + "\n" + other);

        var (exitCode, _, error) = _hook.Run("claude", "Stop", _hook.Payload("claude/Stop.json"), runner);

        exitCode.ShouldBe(2);
        error.ShouldContain(inEdited);
        error.ShouldContain(other);
        error.ShouldNotContain("not shown here");
    }

    // What the agent reads after the edit: the standard error with the code 2 for Claude Code and Codex, the additional context of
    // the JSON answer with the code 0 for Copilot CLI.
    private string AfterEdit(string cli, string eventName, string fixture, FakeRunner runner)
    {
        var (exitCode, output, error) = _hook.Run(cli, eventName, _hook.Payload(fixture), runner);
        if (cli == "copilot")
        {
            exitCode.ShouldBe(0);
            error.ShouldBeEmpty();
            return JsonNode.Parse(output)!["additionalContext"]!.GetValue<string>();
        }

        exitCode.ShouldBe(2);
        output.ShouldBeEmpty();
        return error;
    }
}
