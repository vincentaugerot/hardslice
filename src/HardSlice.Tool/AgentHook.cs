using System.Text.Json;

namespace HardSlice.Tool;

// The hooks of the coding agents, one command per CLI and event: dotnet hardslice hook <cli> <event>, which reads the payload of
// the CLI on its standard input (docs/agents/support.md). Each CLI hands the answer to its agent its own way (HookCli): before an
// edit, the hook refuses a protected file; after the edit of a .cs file, the agent reads the errors of the build; before the agent
// hands back, the full check runs and its failure keeps the agent working.
internal sealed class AgentHook(HookCli cli, ICommandRunner runner, TextWriter output, TextWriter error, Func<string, string?> environment, string stateDirectory)
{
    public int Run(string eventName, TextReader input, string currentDirectory)
    {
        var payload = input.ReadToEnd();
        if (cli.IsInactive(environment))
            return 0;

        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(payload);
            root = document.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            // Never 2, the code that Claude Code and Codex read as a refusal: a broken payload never blocks their agent.
            error.WriteLine($"error: dotnet hardslice hook {cli.Name} {eventName} reads the JSON payload of {cli.DisplayName} on its standard input: {exception.Message}");
            return CheckCommand.Failure;
        }

        var baseDirectory = HookCli.Text(root, "cwd") ?? currentDirectory;
        var step = cli.Step(eventName);
        try
        {
            return step switch
            {
                HookStep.BeforeEdit => BeforeEdit(root, currentDirectory, baseDirectory),
                HookStep.AfterEdit => AfterEdit(root, currentDirectory, baseDirectory),
                _ => BeforeHandingBack(root, currentDirectory),
            };
        }
        catch (Exception exception)
        {
            return Crashed(step, root, $"dotnet hardslice hook {cli.Name} {eventName} failed: {exception.GetType().Name}: {exception.Message}");
        }
    }

    // A hook that fails never lets the agent through: Claude Code and Codex would read the code of a crash as no objection. Before
    // an edit, the edit is refused; after it, the agent reads a verification that failed; before it hands back, it keeps working
    // once, then may finish, since the stop hook is already active and a hook that fails each time would never let it go.
    private int Crashed(HookStep step, JsonElement payload, string failure)
    {
        var help = "If it happens again, stop and tell the user that the HardSlice hook fails, with this message.";
        return step switch
        {
            HookStep.BeforeEdit => cli.Refuse(
                $"HardSlice: {failure}. The edit is refused, since the hook could not check that it touches no protected file. {help}",
                output, error),
            HookStep.AfterEdit => cli.ReportAfterEdit(
                $"HardSlice: the verification after your edit could not run, so treat it as failed: {failure}. "
                + $"Run scripts/check.sh --build to see whether your edit builds. {help}",
                output, error),
            _ when StopHookActive(payload) => cli.LetFinish(
                $"HardSlice: the check before handing back could not run, so the work is not verified: {failure}. Run scripts/check.sh to verify it.",
                output),
            _ => cli.KeepWorking(
                $"HardSlice: the check before handing back could not run, so the work is not verified: {failure}. "
                + $"Run scripts/check.sh yourself and fix what it reports. {help}",
                output, error),
        };
    }

    private static bool StopHookActive(JsonElement payload) =>
        payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("stop_hook_active", out var active) && active.ValueKind == JsonValueKind.True;

    // An edit of a protected file is refused before it happens, with the reason.
    private int BeforeEdit(JsonElement payload, string currentDirectory, string baseDirectory)
    {
        if (EditedFiles(payload, currentDirectory, baseDirectory) is not var (solution, paths))
            return 0;

        foreach (var path in paths)
        {
            if (ProtectedFiles.Match(path, solution.Name) is not { } entry)
                continue;

            return cli.Refuse(
                $"HardSlice: '{path}' is protected ({entry}): agents never edit it, since it holds the rules, their checks or the hooks that verify your work. "
                + "See the section \"Forbidden\" of AGENTS.md: fix the code, never the rule. "
                + "If the task cannot be done without this change, stop and explain the conflict to the user.",
                output, error);
        }

        return 0;
    }

    // After the edit of a .cs file, the errors of the incremental build of check --build reach the agent, or the markers left
    // when the build passes.
    private int AfterEdit(JsonElement payload, string currentDirectory, string baseDirectory)
    {
        if (EditedFiles(payload, currentDirectory, baseDirectory) is not var (_, paths))
            return 0;

        var sources = paths.Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (sources.Length == 0)
            return 0;

        var (exitCode, failure, markersLeft) = Check(currentDirectory, CheckMode.Build);
        if (exitCode == 0)
            return 0;

        var edited = string.Join(", ", sources.Select(path => $"'{path}'"));
        if (markersLeft is { } markers)
        {
            // The build passes: the markers left by a generated element are work to finish, not errors of this edit.
            return cli.ReportAfterEdit(
                $"HardSlice: the build passes after your edit of {edited}. {markers} {TodoMarkers.Marker} {(markers == 1 ? "marker is" : "markers are")} left: "
                + "finish each one, with the logic or the real assertion it asks for, before handing back."
                + Environment.NewLine + failure,
                output, error);
        }

        return cli.ReportAfterEdit(
            $"HardSlice: the build fails after your edit of {edited}. Fix these errors before going on; for a HardSlice rule, use the skill after Skill: and the sheet after Doc:."
            + Environment.NewLine + failure,
            output, error);
    }

    // Before the agent hands back, the full check runs, and its failure keeps the agent working: at most StopRetries.Max
    // relaunches for the same failure and StopRetries.SessionMax in the session, after which the agent tells the user that the
    // check fails.
    private int BeforeHandingBack(JsonElement payload, string currentDirectory)
    {
        var retries = new StopRetries(stateDirectory, cli.Name, cli.SessionId(payload) ?? "unknown");
        var (exitCode, failure, _) = Check(currentDirectory, CheckMode.Full);
        if (exitCode == 0)
        {
            // The agent wrote its last message before this check: only the user can learn that it passed.
            retries.Clear();
            return cli.LetFinish("HardSlice: the check passed.", output);
        }

        var stopHookActive = StopHookActive(payload);
        var (made, inSession) = retries.Made(failure, stopHookActive);
        if (made >= StopRetries.Max || inSession >= StopRetries.SessionMax)
        {
            retries.Clear();
            var limit = made >= StopRetries.Max
                ? $"{StopRetries.Max} relaunches for the same failure"
                : $"{StopRetries.SessionMax} relaunches in this session";
            return cli.LetFinish(
                $"HardSlice: the check still fails after {limit}, so the work is not done. Run scripts/check.sh to see it.",
                output);
        }

        var reason = new StringWriter();
        if (retries.Record(failure) is { } unrecorded)
        {
            // Without its count, the hook could relaunch the agent forever: it blocks the first end of the turn only.
            if (stopHookActive)
            {
                return cli.LetFinish(
                    $"HardSlice: the check fails, so the work is not done, and the hook cannot count its relaunches in '{stateDirectory}' ({unrecorded}). "
                    + "Run scripts/check.sh to see the failure.",
                    output);
            }

            reason.WriteLine(
                $"HardSlice: the check fails, so the work is not done. Fix the failure below, then finish again. The hook cannot count its relaunches "
                + $"in '{stateDirectory}' ({unrecorded}): if you cannot fix the failure without breaking a rule, finish by telling the user that the check fails, with the failure below.");
            reason.Write(failure);
            return cli.KeepWorking(reason.ToString(), output, error);
        }

        reason.WriteLine(
            $"HardSlice: the check fails, so the work is not done. Fix the failure below, then finish again: this hook runs the check again "
            + $"(relaunch {made + 1} of {StopRetries.Max} for this failure).");
        if (made + 1 == StopRetries.Max || inSession + 1 == StopRetries.SessionMax)
        {
            reason.WriteLine(
                $"This is the last relaunch {(made + 1 == StopRetries.Max ? "for this failure" : "of this session")}. If you cannot fix it without breaking a rule, stop trying: "
                + "finish by telling the user that the check fails, with the failure below.");
        }

        reason.Write(failure);
        return cli.KeepWorking(reason.ToString(), output, error);
    }

    // The solution and the paths of the edited files relative to its root, or null when no edited file is in a generated solution.
    private (Solution Solution, string[] Paths)? EditedFiles(JsonElement payload, string currentDirectory, string baseDirectory)
    {
        var files = cli.EditedFiles(payload);
        if (files.Count == 0)
            return null;

        var solution = Solution.Find(currentDirectory, TextWriter.Null);
        if (solution is null)
            return null;

        var paths = files
            .Select(file => SolutionPath.Relative(solution.Root, file, baseDirectory))
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return paths.Length == 0 ? null : (solution, paths);
    }

    // Runs the check in the given mode with every line it and its commands print kept for the agent, and the number of
    // TODO(hardslice): markers when they are its only failure.
    private (int ExitCode, string Output, int? MarkersLeft) Check(string currentDirectory, CheckMode mode)
    {
        var captured = new StringWriter();
        var check = new CheckCommand(runner.WithOutput(captured), captured, captured);
        var exitCode = check.Run(currentDirectory, mode);
        return (exitCode, captured.ToString(), check.MarkersLeft);
    }
}
