namespace HardSlice.Tool.Tests;

// check keeps the contract of the former check scripts: same steps, same order, same messages, exit code of the first failure.
public sealed class CheckCommandTests
{
    private const string Build = "dotnet build Demo.slnx";
    private const string UnitTests = "dotnet test --project tests/Demo.Tests/Demo.Tests.csproj --no-build";
    private const string ArchitectureTests = "dotnet test --project tests/Demo.ArchitectureTests/Demo.ArchitectureTests.csproj --no-build";
    private const string ContainerRuntime = "docker info";
    private const string IntegrationTests = "dotnet test --project tests/Demo.IntegrationTests/Demo.IntegrationTests.csproj --no-build";

    [Fact]
    public void A_passing_check_runs_every_step_in_order_from_the_solution_root()
    {
        using var solution = new TemporarySolution();
        var runner = new FakeRunner();

        var (exitCode, output, error) = Check(runner, solution.Root);

        exitCode.ShouldBe(0);
        runner.Commands.ShouldBe([Build, UnitTests, ArchitectureTests, ContainerRuntime, IntegrationTests]);
        runner.WorkingDirectories.ShouldAllBe(directory => directory == solution.Root);
        Lines(output).ShouldBe([
            "==> Building Demo.slnx",
            "==> Running unit tests",
            "==> Running architecture tests",
            "==> Checking the container runtime",
            "==> Running integration tests",
            "==> Check passed",
        ]);
        error.ShouldBeEmpty();
    }

    [Fact]
    public void Check_finds_the_solution_root_from_one_of_its_folders()
    {
        using var solution = new TemporarySolution("Rooms");
        var folder = Directory.CreateDirectory(Path.Combine(solution.Root, "src", "Rooms")).FullName;
        var runner = new FakeRunner();

        Check(runner, folder).ExitCode.ShouldBe(0);

        runner.Commands[0].ShouldBe("dotnet build Rooms.slnx");
        runner.WorkingDirectories.ShouldAllBe(directory => directory == solution.Root);
    }

    [Theory]
    [InlineData(Build, 1, 1)]
    [InlineData(UnitTests, 2, 2)]
    [InlineData(ArchitectureTests, 8, 3)]
    [InlineData(IntegrationTests, 5, 5)]
    public void Check_stops_at_the_first_failing_step_with_its_exit_code(string failingStep, int stepExitCode, int commandsRun)
    {
        using var solution = new TemporarySolution();
        var runner = new FakeRunner().Failing(failingStep, stepExitCode);

        var (exitCode, output, _) = Check(runner, solution.Root);

        exitCode.ShouldBe(stepExitCode);
        runner.Commands.Count.ShouldBe(commandsRun);
        runner.Commands[^1].ShouldBe(failingStep);
        output.ShouldNotContain("Check passed");
    }

    [Fact]
    public void Without_a_container_runtime_check_names_the_prerequisite_and_exits_1_before_the_integration_tests()
    {
        using var solution = new TemporarySolution();
        var runner = new FakeRunner().Failing(ContainerRuntime, 1);

        var (exitCode, _, error) = Check(runner, solution.Root);

        exitCode.ShouldBe(1);
        runner.Commands.ShouldNotContain(IntegrationTests);
        error.ShouldBe(
            "error: integration tests need a Docker-compatible container runtime reachable by the 'docker' CLI (Docker Desktop, Docker Engine, Podman, Colima...). "
            + "Start one, check that 'docker info' succeeds, then run this script again." + Environment.NewLine);
    }

    [Fact]
    public void Check_fails_before_building_when_the_analyzers_have_another_version()
    {
        using var solution = new TemporarySolution(analyzerVersion: "9.9.9");
        var runner = new FakeRunner();

        var (exitCode, output, error) = Check(runner, solution.Root);

        exitCode.ShouldBe(1);
        runner.Commands.ShouldBeEmpty();
        output.ShouldBeEmpty();
        error.ShouldBe(
            $"error: Directory.Packages.props references HardSlice.Analyzers 9.9.9, but dotnet hardslice is version {ToolVersion.Current}. "
            + "The tool and the analyzers must have the same version: set the 'hardslice.tool' entry of .config/dotnet-tools.json "
            + "and the HardSlice.Analyzers reference of Directory.Packages.props to the same version, then run 'dotnet tool restore'."
            + Environment.NewLine);
    }

    [Theory]
    [InlineData("""<PackageVersion Include="HardSlice.Analyzers" Version="0.1.0" />""")]
    [InlineData("""<GlobalPackageReference Include="HardSlice.Analyzers" />""")]
    [InlineData("")]
    public void Check_fails_before_building_without_a_versioned_global_reference_to_the_analyzers(string items)
    {
        using var solution = new TemporarySolution();
        solution.WritePackages(items);
        var runner = new FakeRunner();

        var (exitCode, _, error) = Check(runner, solution.Root);

        exitCode.ShouldBe(1);
        runner.Commands.ShouldBeEmpty();
        error.ShouldStartWith("error: Directory.Packages.props must hold one GlobalPackageReference to HardSlice.Analyzers with its version");
    }

    [Fact]
    public void Check_fails_without_Directory_Packages_props()
    {
        using var solution = new TemporarySolution();
        File.Delete(Path.Combine(solution.Root, "Directory.Packages.props"));
        var runner = new FakeRunner();

        var (exitCode, _, error) = Check(runner, solution.Root);

        exitCode.ShouldBe(1);
        runner.Commands.ShouldBeEmpty();
        error.ShouldStartWith("error: Directory.Packages.props was not found in ");
    }

    [Fact]
    public void Check_fails_when_the_root_holds_no_single_solution()
    {
        using var solution = new TemporarySolution();
        File.WriteAllText(Path.Combine(solution.Root, "Other.slnx"), "<Solution />");
        var runner = new FakeRunner();

        var (exitCode, _, error) = Check(runner, solution.Root);

        exitCode.ShouldBe(1);
        runner.Commands.ShouldBeEmpty();
        error.ShouldContain("holds 2 .slnx solutions instead of one");
    }

    [Fact]
    public void Check_fails_outside_a_solution_with_a_tool_manifest()
    {
        var directory = Directory.CreateTempSubdirectory("hardslice-tool-").FullName;
        try
        {
            var runner = new FakeRunner();

            var (exitCode, _, error) = Check(runner, directory);

            exitCode.ShouldBe(1);
            runner.Commands.ShouldBeEmpty();
            error.ShouldStartWith($"error: no .config/dotnet-tools.json in '{directory}' or above it.");
        }
        finally
        {
            Directory.Delete(directory);
        }
    }

    private static (int ExitCode, string Output, string Error) Check(FakeRunner runner, string currentDirectory)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = Cli.Run(["check"], currentDirectory, runner, output, error);
        return (exitCode, output.ToString(), error.ToString());
    }

    private static string[] Lines(string text) => text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
}
