using System.Text.Json;
using System.Text.RegularExpressions;

namespace HardSlice.Tool;

// The hooks of the coding agents, one command per CLI and event: dotnet hardslice hook <cli> <event>, which reads the payload of
// the CLI on its standard input (docs/agents/support.md). Each CLI hands the answer to its agent its own way (HookCli): before an
// edit, the hook refuses a protected file; after the edit of a .cs file, the agent reads the errors of the build; before the agent
// hands back, the full check runs and its failure keeps the agent working.
internal sealed partial class AgentHook(HookCli cli, ICommandRunner runner, TextWriter output, TextWriter error, Func<string, string?> environment, string stateDirectory)
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
        if (EditedFiles(payload, currentDirectory, baseDirectory) is not var (solution, paths))
            return 0;

        var sources = paths.Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (sources.Length == 0)
            return 0;

        var (exitCode, failure, markersLeft, _) = Check(currentDirectory, CheckMode.Build);
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
            + Environment.NewLine + EditedFilesFirst(failure, solution.Root, sources),
            output, error);
    }

    // During a change of several files, most errors of the build are in files that the next edits will change: the errors of the
    // edited files are kept in full, and the others are only counted. When no error is in an edited file, every error is kept,
    // since the edit may have broken another file. A line that names no file, as an error of the tool, is always kept.
    private static string EditedFilesFirst(string failure, string root, IReadOnlyCollection<string> edited)
    {
        var lines = failure.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n')
            .Select(line => (Text: line, File: ErrorFile(line, root)))
            .Select(line => (line.Text, line.File, Other: line.File is not null && !edited.Contains(line.File, StringComparer.OrdinalIgnoreCase)))
            .ToArray();
        var others = lines.Where(line => line.Other).ToArray();
        if (others.Length == 0 || !lines.Any(line => line.File is not null && !line.Other))
            return failure;

        var files = others.Select(line => line.File).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        return string.Join(Environment.NewLine, lines.Where(line => !line.Other).Select(line => line.Text)) + Environment.NewLine
            + $"HardSlice: {others.Length} more {(others.Length == 1 ? "error" : "errors")} in {files} other {(files == 1 ? "file" : "files")}, "
            + "not shown here since the next edits of your change may fix them. Once the change is done, run scripts/check.sh --build to see those left: "
            + "the full check before handing back lists every error." + Environment.NewLine;
    }

    // The file of an error that the compiler or MSBuild locates, relative to the root of the solution, or null for another line.
    // A file outside the solution keeps its own path, never one of the edited files.
    private static string? ErrorFile(string line, string root) =>
        LocatedError().Match(line) is { Success: true } match
            ? SolutionPath.Relative(root, match.Groups["file"].Value, root) ?? match.Groups["file"].Value
            : null;

    // 'src/Demo/Program.cs(12,5): error CS0103: ...', as the console logger of MSBuild prints an error, with a line and a column or a range.
    [GeneratedRegex(@"^(?<file>.+?)\(\d+(?:,\d+){0,3}\): error ")]
    private static partial Regex LocatedError();

    // Before the agent hands back, the full check runs, and its failure keeps the agent working: at most StopRetries.Max
    // relaunches for the same failure and StopRetries.SessionMax in the session, after which the agent tells the user that the
    // check fails. A failure that only a maintainer can fix, on a protected HardSlice file of the solution, is reported once.
    private int BeforeHandingBack(JsonElement payload, string currentDirectory)
    {
        var retries = new StopRetries(stateDirectory, cli.Name, cli.SessionId(payload) ?? "unknown");
        var (exitCode, failure, _, stoppedOnProtectedFile) = Check(currentDirectory, CheckMode.Full);
        if (exitCode == 0)
        {
            // The agent wrote its last message before this check: only the user can learn that it passed.
            retries.Forget();
            return cli.LetFinish("HardSlice: the check passed.", output);
        }

        var stopHookActive = StopHookActive(payload);
        if (stoppedOnProtectedFile)
            return ForAMaintainer(retries, failure, stopHookActive);

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

    // The check stops before the build on a protected HardSlice file, as when the solution was generated by another template or
    // another version of the tool: the agent cannot fix it, and each relaunch would only repeat the same explanation. The failure
    // reaches the agent once in the session, so that it hands back and tells the user what a maintainer must do, as after a
    // refused edit of a protected file. Once reported, in this turn or an earlier one while the agent waits for the maintainer, the
    // agent may finish. The relaunches counted for the other failures do not change.
    private int ForAMaintainer(StopRetries retries, string failure, bool stopHookActive)
    {
        var reported = retries.Reported(failure);
        if (!reported && retries.Report(failure) is not null)
        {
            // Without its record, a report would follow each end of the agent: only the first end of a turn reports it.
            reported = stopHookActive;
        }

        if (reported)
        {
            return cli.LetFinish(
                "HardSlice: the check stops on a protected HardSlice file of the solution, so the work is not verified. "
                + "Only a maintainer can fix it: run scripts/check.sh to see the error, its Fix: and its Doc:.",
                output);
        }

        var reason = new StringWriter();
        reason.WriteLine(
            "HardSlice: the check stops before the build on a protected HardSlice file of the solution, so the work is not verified. "
            + "Only a maintainer can fix it, as the error below says: do not try to fix it. "
            + "Finish now by telling the user that the check fails on this file and what a maintainer must do, with the Fix: and the Doc: of the error. "
            + "This hook lets you finish: it does not relaunch you for this failure.");
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

    // Runs the check in the given mode with every line it and its commands print kept for the agent, the number of
    // TODO(hardslice): markers when they are its only failure, and whether it stopped on what only a maintainer can fix.
    private (int ExitCode, string Output, int? MarkersLeft, bool StoppedOnProtectedFile) Check(string currentDirectory, CheckMode mode)
    {
        var captured = new StringWriter();
        var check = new CheckCommand(runner.WithOutput(captured), captured, captured);
        var exitCode = check.Run(currentDirectory, mode);
        return (exitCode, captured.ToString(), check.MarkersLeft, check.StoppedOnProtectedFile);
    }
}
