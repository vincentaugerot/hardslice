namespace HardSlice.Tool;

// The single verification for humans, agents, hooks and CI: a task is not done until it exits 0.
// Builds the solution (restoring if needed), runs unit and architecture tests, then integration tests,
// which need a Docker-compatible container runtime. Compiler and test output reach the console as is.
internal sealed class CheckCommand(ICommandRunner runner, TextWriter output, TextWriter error)
{
    public const string ContainerRuntimeMissing =
        "error: integration tests need a Docker-compatible container runtime reachable by the 'docker' CLI (Docker Desktop, Docker Engine, Podman, Colima...). "
        + "Start one, check that 'docker info' succeeds, then run this script again.";

    // Exit code when the solution cannot be verified: no solution, another analyzer version, no container runtime.
    public const int Failure = 1;

    public int Run(string currentDirectory)
    {
        var solution = Solution.Find(currentDirectory, error);
        if (solution is null || !AnalyzerVersion.Matches(solution, ToolVersion.Current, error))
            return Failure;

        return Step(solution, $"Building {solution.File}", "build", solution.File)
            ?? Step(solution, "Running unit tests", "test", "--project", solution.TestProject("Tests"), "--no-build")
            ?? Step(solution, "Running architecture tests", "test", "--project", solution.TestProject("ArchitectureTests"), "--no-build")
            ?? ContainerRuntime(solution)
            ?? Step(solution, "Running integration tests", "test", "--project", solution.TestProject("IntegrationTests"), "--no-build")
            ?? Passed();
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

    private int Passed()
    {
        output.WriteLine("==> Check passed");
        return 0;
    }
}
