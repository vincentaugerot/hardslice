namespace HardSlice.Tool.Tests;

// check keeps the contract of the former check scripts: same steps, same order, same messages, exit code of the first failure,
// then fails on the TODO(hardslice): markers left. check --build is the incremental build alone, then the same markers.
public sealed class CheckCommandTests
{
    private const string Build = "dotnet build Demo.slnx";
    private const string UnitTests = "dotnet test --project tests/Demo.Tests/Demo.Tests.csproj --no-build";
    private const string ArchitectureTests = "dotnet test --project tests/Demo.ArchitectureTests/Demo.ArchitectureTests.csproj --no-build";
    private const string ContainerRuntime = "docker info";
    private const string IntegrationTests = "dotnet test --project tests/Demo.IntegrationTests/Demo.IntegrationTests.csproj --no-build";
    private const string IncrementalBuild = "dotnet build Demo.slnx --no-restore -nologo -tl:off -noconsolelogger -logger:Microsoft.Build.Logging.ConsoleLogger,Microsoft.Build;ErrorsOnly;NoSummary;Verbosity=minimal";
    private const string Restore = "dotnet restore Demo.slnx -nologo -v:q";

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
            "==> Looking for TODO(hardslice): markers in src/ and tests/",
            "==> Check passed",
        ]);
        error.ShouldBeEmpty();
    }

    [Fact]
    public void Check_fails_after_every_step_while_a_TODO_hardslice_marker_is_left_and_names_each_file_and_line()
    {
        using var solution = new TemporarySolution();
        solution.Write("src/Demo/Features/Rooms/BookRoom/BookRoom.cs",
            "internal sealed class BookRoomHandler\n{\n    // TODO(hardslice): implement BookRoomHandler.\n}\n");
        solution.Write("tests/Demo.IntegrationTests/Features/Rooms/BookRoom/BookRoomTests.cs",
            "public sealed class BookRoomTests\r\n{\r\n    // A trivial assertion does not remove the marker.\r\n    // TODO(hardslice): assert the business outcome.\r\n}\r\n");
        var runner = new FakeRunner();

        var (exitCode, output, error) = Check(runner, solution.Root);

        exitCode.ShouldBe(1);
        runner.Commands.ShouldBe([Build, UnitTests, ArchitectureTests, ContainerRuntime, IntegrationTests]);
        output.ShouldNotContain("Check passed");
        Lines(error).ShouldBe([
            "error: 2 TODO(hardslice): markers are left in src/ and tests/: each one is work a generated element leaves to do. "
                + "Replace it with the logic or the real assertion it asks for, never with an empty test or one that asserts nothing.",
            "src/Demo/Features/Rooms/BookRoom/BookRoom.cs:3: // TODO(hardslice): implement BookRoomHandler.",
            "tests/Demo.IntegrationTests/Features/Rooms/BookRoom/BookRoomTests.cs:4: // TODO(hardslice): assert the business outcome.",
        ]);
    }

    [Fact]
    public void Markers_in_build_output_or_outside_src_and_tests_do_not_fail_the_check()
    {
        using var solution = new TemporarySolution();
        solution.Write("src/Demo/obj/Debug/net10.0/Demo.Tests.cs", "// TODO(hardslice): a copy made by the build.");
        solution.Write("tests/Demo.Tests/bin/Debug/net10.0/Demo.Tests.dll", "\0Assert.Fail(\"TODO(hardslice): compiled.\")");
        solution.Write("tests/Demo.Tests/Binary.dat", "\0TODO(hardslice): not a source file.");
        solution.Write("docs/notes.md", "TODO(hardslice): the documentation explains the markers.");
        solution.Write("src/Demo/Todo.cs", "// TODO: an ordinary marker. todo(hardslice): another case.");

        var (exitCode, _, error) = Check(new FakeRunner(), solution.Root);

        exitCode.ShouldBe(0);
        error.ShouldBeEmpty();
    }

    [Fact]
    public void One_marker_left_is_named_in_the_singular()
    {
        using var solution = new TemporarySolution();
        solution.Write("tests/Demo.Tests/Domain/Rooms/RoomTests.cs", "Assert.Fail(\"TODO(hardslice): assert the state.\");");

        var (_, _, error) = Check(new FakeRunner(), solution.Root);

        error.ShouldStartWith("error: 1 TODO(hardslice): marker is left in src/ and tests/: ");
        error.ShouldContain("tests/Demo.Tests/Domain/Rooms/RoomTests.cs:1: Assert.Fail(\"TODO(hardslice): assert the state.\");");
    }

    [Fact]
    public void Check_build_runs_the_incremental_build_alone_and_says_that_only_the_full_check_decides()
    {
        using var solution = new TemporarySolution();
        solution.WriteProjects(("src/Demo/Demo.csproj", true), ("tests/Demo.Tests/Demo.Tests.csproj", true));
        var runner = new FakeRunner();

        var (exitCode, output, error) = Check(runner, solution.Root, "--build");

        exitCode.ShouldBe(0);
        runner.Commands.ShouldBe([IncrementalBuild]);
        runner.WorkingDirectories.ShouldAllBe(directory => directory == solution.Root);
        Lines(output).ShouldBe([
            CheckCommand.BuildModeReduction,
            "==> Building Demo.slnx",
            "==> Looking for TODO(hardslice): markers in src/ and tests/",
            "==> Build passed. Tests did not run: only the full check decides that the work is done.",
        ]);
        output.ShouldContain("Only the full check, 'dotnet hardslice check' or 'scripts/check.sh' without argument, decides that the work is done.");
        error.ShouldBeEmpty();
    }

    [Fact]
    public void Check_build_restores_first_when_a_project_has_never_been_restored()
    {
        using var solution = new TemporarySolution();
        solution.WriteProjects(("src/Demo/Demo.csproj", true), ("tests/Demo.Tests/Demo.Tests.csproj", false));
        var runner = new FakeRunner();

        var (exitCode, output, _) = Check(runner, solution.Root, "--build");

        exitCode.ShouldBe(0);
        runner.Commands.ShouldBe([Restore, IncrementalBuild]);
        output.ShouldContain("==> Restoring Demo.slnx: tests/Demo.Tests/Demo.Tests.csproj has never been restored");
    }

    [Theory]
    [InlineData("src/Demo/Demo.csproj")]
    [InlineData("Directory.Packages.props")]
    [InlineData("Directory.Build.props")]
    [InlineData("NuGet.Config")]
    public void Check_build_restores_first_when_a_file_read_by_the_restore_changed_since(string changed)
    {
        using var solution = new TemporarySolution();
        solution.WriteProjects(("src/Demo/Demo.csproj", true));
        var file = Path.Combine(solution.Root, changed);
        if (!File.Exists(file))
            File.WriteAllText(file, "<configuration />");
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddMinutes(5));
        var runner = new FakeRunner();

        var (exitCode, output, _) = Check(runner, solution.Root, "--build");

        exitCode.ShouldBe(0);
        runner.Commands.ShouldBe([Restore, IncrementalBuild]);
        output.ShouldContain($"==> Restoring Demo.slnx: {changed} changed since the last restore of src/Demo/Demo.csproj");
    }

    [Fact]
    public void Check_build_stops_at_a_failing_restore_with_its_exit_code()
    {
        using var solution = new TemporarySolution();
        solution.WriteProjects(("src/Demo/Demo.csproj", false));
        var runner = new FakeRunner().Failing(Restore, 1);

        var (exitCode, _, _) = Check(runner, solution.Root, "--build");

        exitCode.ShouldBe(1);
        runner.Commands.ShouldBe([Restore]);
    }

    // The compiler output reaches the console as is: the tool never captures nor rewrites it.
    [Fact]
    public void Check_build_exits_with_the_code_of_a_failing_build_before_looking_for_markers()
    {
        using var solution = new TemporarySolution();
        solution.WriteProjects(("src/Demo/Demo.csproj", true));
        solution.Write("src/Demo/Todo.cs", "// TODO(hardslice): left.");
        var runner = new FakeRunner().Failing(IncrementalBuild, 1);

        var (exitCode, output, error) = Check(runner, solution.Root, "--build");

        exitCode.ShouldBe(1);
        runner.Commands.ShouldBe([IncrementalBuild]);
        output.ShouldNotContain("TODO(hardslice): markers");
        output.ShouldNotContain("Build passed");
        error.ShouldBeEmpty();
    }

    [Fact]
    public void Check_build_fails_on_a_TODO_hardslice_marker_after_a_passing_build()
    {
        using var solution = new TemporarySolution();
        solution.WriteProjects(("src/Demo/Demo.csproj", true));
        solution.Write("src/Demo/Features/Rooms/BookRoom/BookRoom.cs", "throw new NotImplementedException(\"TODO(hardslice): implement.\");");

        var (exitCode, output, error) = Check(new FakeRunner(), solution.Root, "--build");

        exitCode.ShouldBe(1);
        output.ShouldNotContain("Build passed");
        error.ShouldContain("src/Demo/Features/Rooms/BookRoom/BookRoom.cs:1: throw new NotImplementedException(\"TODO(hardslice): implement.\");");
    }

    [Fact]
    public void Check_build_fails_before_building_when_the_analyzers_have_another_version()
    {
        using var solution = new TemporarySolution(analyzerVersion: "9.9.9");
        var runner = new FakeRunner();

        var (exitCode, output, error) = Check(runner, solution.Root, "--build");

        exitCode.ShouldBe(1);
        runner.Commands.ShouldBeEmpty();
        output.ShouldBeEmpty();
        error.ShouldStartWith("error: Directory.Packages.props references HardSlice.Analyzers 9.9.9");
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

    // PB-053: dotnet test --no-build would run the old binaries of a test project that the build of the .slnx skips.
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Check_fails_before_building_when_a_test_project_is_left_out_of_the_solution_and_names_it(int left)
    {
        using var solution = new TemporarySolution();
        var kept = solution.TestProjects.Where((_, index) => index != left).ToArray();
        solution.WriteProjects([("src/Demo/Demo.csproj", true), .. kept.Select(project => (project, true))]);
        var runner = new FakeRunner();

        var (exitCode, output, error) = Check(runner, solution.Root);

        exitCode.ShouldBe(CheckCommand.Failure);
        runner.Commands.ShouldBeEmpty();
        output.ShouldBeEmpty();
        error.ShouldBe(
            $"error: Demo.slnx does not build {solution.TestProjects[left]}: its tests would run on the binaries of an older build. "
            + $"Add it back to Demo.slnx, as the template generated it.{Environment.NewLine}");
    }

    [Fact]
    public void Check_fails_when_the_solution_is_not_valid_XML()
    {
        using var solution = new TemporarySolution();
        File.WriteAllText(Path.Combine(solution.Root, "Demo.slnx"), "<Solution>");
        var runner = new FakeRunner();

        var (exitCode, _, error) = Check(runner, solution.Root);

        exitCode.ShouldBe(CheckCommand.Failure);
        runner.Commands.ShouldBeEmpty();
        error.ShouldStartWith("error: Demo.slnx is not valid XML");
    }

    // The incremental build after an edit runs no test: it never reads the test projects of the .slnx.
    [Fact]
    public void Check_build_does_not_require_the_test_projects_in_the_solution()
    {
        using var solution = new TemporarySolution();
        solution.WriteProjects(("src/Demo/Demo.csproj", true));
        var runner = new FakeRunner();

        Check(runner, solution.Root, "--build").ExitCode.ShouldBe(0);

        runner.Commands.ShouldBe([IncrementalBuild]);
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

    private static (int ExitCode, string Output, string Error) Check(FakeRunner runner, string currentDirectory, params string[] mode)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = Cli.Run(["check", .. mode], currentDirectory, runner, output, error);
        return (exitCode, output.ToString(), error.ToString());
    }

    private static string[] Lines(string text) => text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
}
