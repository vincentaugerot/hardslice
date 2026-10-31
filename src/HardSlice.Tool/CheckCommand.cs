namespace HardSlice.Tool;

// The verification of a solution, in one of its modes. Compiler and test output reach the console as is.
internal enum CheckMode
{
    // The single verification for humans, agents, hooks and CI: a task is not done until it exits 0.
    // Builds the solution (restoring if needed), runs unit and architecture tests, then integration tests,
    // which need a Docker-compatible container runtime.
    Full,

    // The verification after an edit: the incremental build of the solution, errors only, no test.
    Build,
}

internal sealed class CheckCommand(ICommandRunner runner, TextWriter output, TextWriter error)
{
    public const string ContainerRuntimeMissing =
        "error: integration tests need a Docker-compatible container runtime reachable by the 'docker' CLI (Docker Desktop, Docker Engine, Podman, Colima...). "
        + "Start one, check that 'docker info' succeeds, then run this script again.";

    public const string BuildModeReduction =
        "==> check --build: incremental build only, errors only, no test. "
        + "Only the full check, 'dotnet hardslice check' or 'scripts/check.sh' without argument, decides that the work is done.";

    public const string ErrorsOnlyLogger = "-logger:Microsoft.Build.Logging.ConsoleLogger,Microsoft.Build;ErrorsOnly;NoSummary;Verbosity=minimal";

    // Exit code when the solution cannot be verified: no solution, another analyzer version, no container runtime,
    // or a TODO(hardslice): marker left.
    public const int Failure = 1;

    public int Run(string currentDirectory, CheckMode mode)
    {
        var solution = Solution.Find(currentDirectory, error);
        if (solution is null || !AnalyzerVersion.Matches(solution, ToolVersion.Current, error))
            return Failure;

        return mode switch
        {
            CheckMode.Build => RunBuild(solution),
            _ => RunFull(solution),
        };
    }

    private int RunFull(Solution solution) =>
        Step(solution, $"Building {solution.File}", "build", solution.File)
            ?? Step(solution, "Running unit tests", "test", "--project", solution.TestProject("Tests"), "--no-build")
            ?? Step(solution, "Running architecture tests", "test", "--project", solution.TestProject("ArchitectureTests"), "--no-build")
            ?? ContainerRuntime(solution)
            ?? Step(solution, "Running integration tests", "test", "--project", solution.TestProject("IntegrationTests"), "--no-build")
            ?? Markers(solution)
            ?? Passed("==> Check passed");

    // dotnet build --no-restore, after a restore only when an assets file is missing or older than what it was restored from.
    // Its console logger is replaced by one that prints the errors alone, each as the compiler wrote it, without the banner
    // and summary that dotnet build adds to its own.
    private int RunBuild(Solution solution)
    {
        output.WriteLine(BuildModeReduction);
        var restore = RestoreState.Outdated(solution);
        return (restore is null ? null : Step(solution, $"Restoring {solution.File}: {restore}", "restore", solution.File, "-nologo", "-v:q"))
            ?? Step(solution, $"Building {solution.File}", "build", solution.File, "--no-restore", "-nologo", "-tl:off", "-noconsolelogger", ErrorsOnlyLogger)
            ?? Markers(solution)
            ?? Passed("==> Build passed. Tests did not run: only the full check decides that the work is done.");
    }

    // Null when the step passes, otherwise its exit code.
    private int? Step(Solution solution, string title, params string[] arguments)
    {
        output.WriteLine($"==> {title}");
        var exitCode = runner.Run("dotnet", arguments, solution.Root);
        return exitCode == 0 ? null : exitCode;
    }

    private int? ContainerRuntime(Solution solution)
    {
        output.WriteLine("==> Checking the container runtime");
        if (runner.Succeeds("docker", ["info"], solution.Root))
            return null;

        error.WriteLine(ContainerRuntimeMissing);
        return Failure;
    }

    // A generated element is never done: its TODO(hardslice): markers say what is left, and replacing a failing test
    // with a trivial assertion does not remove them.
    private int? Markers(Solution solution)
    {
        output.WriteLine($"==> Looking for {TodoMarkers.Marker} markers in {string.Join(" and ", TodoMarkers.Folders.Select(folder => folder + "/"))}");
        var markers = TodoMarkers.Find(solution.Root);
        if (markers.Count == 0)
            return null;

        error.WriteLine(
            $"error: {markers.Count} {TodoMarkers.Marker} {(markers.Count == 1 ? "marker is" : "markers are")} left in src/ and tests/: "
            + "each one is work a generated element leaves to do. Replace it with the logic or the real assertion it asks for, "
            + "never with an empty test or one that asserts nothing.");
        foreach (var marker in markers)
            error.WriteLine($"{marker.Path}:{marker.Line}: {marker.Text}");
        return Failure;
    }

    private int Passed(string message)
    {
        output.WriteLine(message);
        return 0;
    }
}
