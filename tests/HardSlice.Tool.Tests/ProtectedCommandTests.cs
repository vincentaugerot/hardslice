namespace HardSlice.Tool.Tests;

// dotnet hardslice protected, which the CI of a generated solution runs on the files of a pull request
// (.github/workflows/hardslice.yml): git diff --name-only -z HEAD^1 HEAD | dotnet hardslice protected.
public sealed class ProtectedCommandTests : IDisposable
{
    private readonly TemporarySolution _solution = new();

    public void Dispose() => _solution.Dispose();

    [Theory]
    [InlineData("hardslice.json", "hardslice.json")]
    [InlineData("src/Demo/.editorconfig", ".editorconfig")]
    [InlineData("tests/Demo.Tests/Directory.Build.props", "Directory.Build.props")]
    [InlineData("Directory.Packages.props", "Directory.Packages.props")]
    [InlineData(".config/dotnet-tools.json", ".config/dotnet-tools.json")]
    [InlineData("scripts/check.sh", "scripts/")]
    [InlineData("tests/Demo.ArchitectureTests/SliceTests.cs", "tests/Demo.ArchitectureTests/")]
    [InlineData(".claude/settings.json", ".claude/")]
    [InlineData(".codex/hooks.json", ".codex/")]
    [InlineData(".github/hooks/hardslice.json", ".github/hooks/")]
    [InlineData(".githooks/pre-commit", ".githooks/")]
    [InlineData(".github/workflows/hardslice.yml", ".github/workflows/hardslice.yml")]
    [InlineData("src/Demo/.globalconfig", ".globalconfig")]
    [InlineData(".globalconfig", ".globalconfig")]
    [InlineData("Directory.Build.rsp", "Directory.Build.rsp")]
    [InlineData("src/Demo/Directory.Build.rsp", "Directory.Build.rsp")]
    [InlineData("Directory.Solution.props", "Directory.Solution.props")]
    [InlineData("Directory.Solution.targets", "Directory.Solution.targets")]
    [InlineData("nuget.config", "nuget.config")]
    [InlineData("NuGet.Config", "nuget.config")]
    [InlineData(".agents/skills/add-slice/SKILL.md", ".agents/")]
    public void A_protected_path_is_listed_with_its_entry_and_exits_1(string path, string entry)
    {
        var (exitCode, output, error) = Run($"src/Demo/Program.cs\n{path}\nREADME.md\n");

        exitCode.ShouldBe(ProtectedCommand.Found);
        output.ShouldBe($"HardSlice: '{path}' is protected ({entry}).{Environment.NewLine}");
        error.ShouldBeEmpty();
    }

    [Fact]
    public void Paths_without_protected_file_exit_0()
    {
        var (exitCode, output, error) = Run(
            "src/Demo/Program.cs\ntests/Demo.Tests/Features/Bookings/ConfirmBooking/ConfirmBookingTests.cs\n"
            + ".github/workflows/deploy.yml\ndocs/hardslice.json.md\nsrc/Demo/scripts/Seed.cs\ntests/Demo.ArchitectureTestsHelpers/Helper.cs\n"
            + "src/Demo/nuget.config\nsrc/Demo/.agents/notes.md\n");

        exitCode.ShouldBe(0);
        output.ShouldBe($"HardSlice: none of the 8 paths read is protected.{Environment.NewLine}");
        error.ShouldBeEmpty();
    }

    [Fact]
    public void No_path_exits_0()
    {
        var (exitCode, output, _) = Run("");

        exitCode.ShouldBe(0);
        output.ShouldBe($"HardSlice: none of the 0 paths read is protected.{Environment.NewLine}");
    }

    // git diff -z separates the names with NUL characters and quotes none; Windows ends lines with \r\n and names with backslashes.
    [Fact]
    public void Paths_are_read_separated_by_NUL_characters_or_lines_whatever_their_separators_and_case()
    {
        var (exitCode, output, _) = Run("src/Demo/Program.cs\0src/Café/Directory.Build.props\0SCRIPTS\\check.ps1\r\n./.githooks/pre-commit\0");

        exitCode.ShouldBe(ProtectedCommand.Found);
        output.ShouldBe(
            $"HardSlice: 'src/Café/Directory.Build.props' is protected (Directory.Build.props).{Environment.NewLine}"
            + $"HardSlice: 'SCRIPTS/check.ps1' is protected (scripts/).{Environment.NewLine}"
            + $"HardSlice: '.githooks/pre-commit' is protected (.githooks/).{Environment.NewLine}");
    }

    // Splitting on line breaks too cuts a name that holds one: it adds paths, and never hides a protected file.
    [Fact]
    public void A_name_with_a_line_break_still_reveals_a_protected_file()
    {
        var (exitCode, output, _) = Run("src/odd\nname/Directory.Build.targets\0");

        exitCode.ShouldBe(ProtectedCommand.Found);
        output.ShouldBe($"HardSlice: 'name/Directory.Build.targets' is protected (Directory.Build.targets).{Environment.NewLine}");
    }

    // Without -z, git quotes a name with a character other than printable ASCII and escapes it (core.quotePath): the name is no
    // longer the one of the file, which could hide a protected one, so the line is refused rather than read as safe.
    [Theory]
    [InlineData("src/Demo/Program.cs\n\"src/Modul\\303\\251/Directory.Build.props\"\n")]
    [InlineData("\"scripts/caf\\303\\251.sh\"\n")]
    public void A_line_quoted_by_git_exits_2_and_recommends_z(string input)
    {
        var (exitCode, output, error) = Run(input);

        exitCode.ShouldBe(Cli.UsageError);
        output.ShouldBeEmpty();
        error.ShouldStartWith($"error: {input.Split('\n').First(line => line.StartsWith('"'))} is quoted by git");
        error.ShouldContain("git diff --name-only -z");
    }

    // With -z, git quotes no name: a name that starts with a quote is a real one, read as is.
    [Fact]
    public void With_NUL_separators_a_name_starting_with_a_quote_is_read_as_is()
    {
        var (exitCode, output, _) = Run("\"odd/Directory.Build.rsp\0src/Demo/Program.cs\0");

        exitCode.ShouldBe(ProtectedCommand.Found);
        output.ShouldBe($"HardSlice: '\"odd/Directory.Build.rsp' is protected (Directory.Build.rsp).{Environment.NewLine}");
    }

    // The CI reads code 1 as a protected file that a maintainer may approve: any other failure has another code.
    [Fact]
    public void Outside_a_generated_solution_it_exits_2()
    {
        var directory = Directory.CreateTempSubdirectory("hardslice-no-solution-").FullName;
        try
        {
            var (exitCode, output, error) = Run("hardslice.json\n", directory);

            exitCode.ShouldBe(Cli.UsageError);
            output.ShouldBeEmpty();
            error.ShouldStartWith("error: no .config/dotnet-tools.json in");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void An_argument_exits_2()
    {
        var error = new StringWriter();

        var exitCode = Cli.Run(["protected", "HEAD^1"], _solution.Root, new FakeRunner(), new StringWriter(), error, new StringReader("hardslice.json"), _ => null, "");

        exitCode.ShouldBe(Cli.UsageError);
        error.ToString().ShouldBe(
            $"error: protected takes no argument: it reads the paths on its standard input. Unknown arguments: 'HEAD^1'.{Environment.NewLine}");
    }

    private (int ExitCode, string Output, string Error) Run(string input, string? directory = null)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = Cli.Run(["protected"], directory ?? _solution.Root, new FakeRunner(), output, error, new StringReader(input), _ => null, "");
        return (exitCode, output.ToString(), error.ToString());
    }
}
