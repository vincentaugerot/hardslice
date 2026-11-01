using System.Text.Json;

namespace HardSlice.Tool;

// The hooks of Claude Code in .claude/settings.json, one command per event: dotnet hardslice hook claude <event>, which reads
// the payload of Claude Code on its standard input (docs/agents/support.md). The code 2 hands the standard error to the agent:
// before an edit, it refuses the edit; after an edit, the agent reads the errors of the build; at the end of a turn, the agent
// goes on with the failure of the check as its instruction.
internal sealed class ClaudeHook(ICommandRunner runner, TextWriter output, TextWriter error, Func<string, string?> environment, string stateDirectory)
{
    public static IReadOnlyList<string> Events { get; } = ["PreToolUse", "PostToolUse", "Stop"];

    // The code that makes Claude Code refuse the tool call or the end of the turn and hand the standard error to the agent.
    public const int Block = 2;

    // Copilot CLI runs the hooks of .claude/settings.json too, sets this variable in their environment and does not hand their
    // code 2 to the agent: under Copilot, these hooks would only double its own.
    public const string CopilotVariable = "COPILOT_CLI";

    public int Run(string eventName, TextReader input, string currentDirectory)
    {
        var payload = input.ReadToEnd();
        if (!string.IsNullOrEmpty(environment(CopilotVariable)))
            return 0;

        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(payload);
            root = document.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            // Any code but 2 is shown to the user and lets the agent go on: a broken payload never blocks it.
            error.WriteLine($"error: dotnet hardslice hook claude {eventName} reads the JSON payload of Claude Code on its standard input: {exception.Message}");
            return 1;
        }

        var baseDirectory = Text(root, "cwd") ?? currentDirectory;
        return eventName switch
        {
            "PreToolUse" => BeforeEdit(root, currentDirectory, baseDirectory),
            "PostToolUse" => AfterEdit(root, currentDirectory, baseDirectory),
            _ => BeforeHandingBack(root, currentDirectory),
        };
    }

    // An edit of a protected file is refused before it happens, with the reason.
    private int BeforeEdit(JsonElement payload, string currentDirectory, string baseDirectory)
    {
        if (EditedFile(payload, currentDirectory, baseDirectory) is not { } edited)
            return 0;

        var (solution, path) = edited;
        var entry = ProtectedFiles.Match(path, solution.Name);
        if (entry is null)
            return 0;

        error.WriteLine(
            $"HardSlice: '{path}' is protected ({entry}): agents never edit it, since it holds the rules, their checks or the hooks that verify your work. "
            + "See the section \"Forbidden\" of AGENTS.md: fix the code, never the rule. "
            + "If the task cannot be done without this change, stop and explain the conflict to the user.");
        return Block;
    }

    // After the edit of a .cs file, the errors of the incremental build of scripts/check.sh --build reach the agent.
    private int AfterEdit(JsonElement payload, string currentDirectory, string baseDirectory)
    {
        if (EditedFile(payload, currentDirectory, baseDirectory) is not { Path: var path }
            || !path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            return 0;

        var (exitCode, failure) = Check(currentDirectory, CheckMode.Build);
        if (exitCode == 0)
            return 0;

        error.WriteLine($"HardSlice: the build fails after your edit of '{path}'. Fix these errors before going on; for a HardSlice rule, use the skill after Skill: and the sheet after Doc:.");
        error.Write(failure);
        return Block;
    }

    // Before the agent hands back, the full check of scripts/check.sh runs, and its failure keeps the agent working: at most
    // StopRetries.Max relaunches for the same failure, after which the agent tells the user that the check fails.
    private int BeforeHandingBack(JsonElement payload, string currentDirectory)
    {
        var retries = new StopRetries(stateDirectory, Text(payload, "session_id") ?? "unknown");
        var (exitCode, failure) = Check(currentDirectory, CheckMode.Full);
        if (exitCode == 0)
        {
            // The agent wrote its last message before this check: only the user can learn that it passed.
            retries.Clear();
            ToUser("HardSlice: the check passed.");
            return 0;
        }

        var stopHookActive = payload.TryGetProperty("stop_hook_active", out var active) && active.ValueKind == JsonValueKind.True;
        var made = retries.Made(failure, stopHookActive);
        if (made >= StopRetries.Max)
        {
            retries.Clear();
            ToUser($"HardSlice: the check still fails after {StopRetries.Max} relaunches for the same failure, so the work is not done. Run scripts/check.sh to see it.");
            return 0;
        }

        retries.Record(failure);
        error.WriteLine(
            $"HardSlice: the check fails, so the work is not done. Fix the failure below, then finish again: this hook runs the check again "
            + $"(relaunch {made + 1} of {StopRetries.Max} for this failure).");
        if (made + 1 == StopRetries.Max)
        {
            error.WriteLine(
                "This is the last relaunch for this failure. If you cannot fix it without breaking a rule, stop trying: "
                + "finish by telling the user that the check fails, with the failure below.");
        }

        error.Write(failure);
        return Block;
    }

    // A message that Claude Code shows the user, read from the standard output of a hook that exits 0.
    private void ToUser(string message) =>
        output.WriteLine(JsonSerializer.Serialize(new Dictionary<string, string> { ["systemMessage"] = message }));

    // The solution and the path of the edited file relative to its root, or null for a file outside a generated solution.
    private static (Solution Solution, string Path)? EditedFile(JsonElement payload, string currentDirectory, string baseDirectory)
    {
        var file = payload.TryGetProperty("tool_input", out var input) && input.ValueKind == JsonValueKind.Object ? Text(input, "file_path") : null;
        if (string.IsNullOrEmpty(file))
            return null;

        var solution = Solution.Find(currentDirectory, TextWriter.Null);
        if (solution is null)
            return null;

        var path = SolutionPath.Relative(solution.Root, file, baseDirectory);
        return path is null ? null : (solution, path);
    }

    // Runs the check in the given mode with every line it and its commands print kept for the agent.
    private (int ExitCode, string Output) Check(string currentDirectory, CheckMode mode)
    {
        var captured = new StringWriter();
        var exitCode = new CheckCommand(runner.WithOutput(captured), captured, captured).Run(currentDirectory, mode);
        return (exitCode, captured.ToString());
    }

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
