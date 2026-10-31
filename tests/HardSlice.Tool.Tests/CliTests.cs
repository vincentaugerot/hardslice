namespace HardSlice.Tool.Tests;

public sealed class CliTests
{
    [Fact]
    public void Explain_prints_the_sheet_of_the_rule_and_exits_0()
    {
        var (exitCode, output, error) = Run("explain", "HS016");

        exitCode.ShouldBe(0);
        output.ShouldBe(File.ReadAllText(Path.Combine(Repository.RuleSheets, "HS016.md")));
        error.ShouldBeEmpty();
    }

    [Fact]
    public void Explain_of_an_unknown_rule_exits_2_and_lists_the_rules_with_a_sheet()
    {
        var (exitCode, output, error) = Run("explain", "HS999");

        exitCode.ShouldBe(2);
        output.ShouldBeEmpty();
        error.ShouldBe($"error: 'HS999' is not a HardSlice rule with a sheet. Rules with a sheet: {string.Join(", ", RuleSheets.Ids)}.{Environment.NewLine}");
        error.ShouldContain("HS016");
    }

    [Theory]
    [InlineData]
    [InlineData("HS016", "HS017")]
    public void Explain_takes_exactly_one_rule(params string[] rules)
    {
        var (exitCode, output, error) = Run(["explain", .. rules]);

        exitCode.ShouldBe(2);
        output.ShouldBeEmpty();
        error.ShouldContain("Rules with a sheet: HS000, HS001");
    }

    // No option skips a step of the verification.
    [Theory]
    [InlineData("--skip-integration-tests")]
    [InlineData("--no-build")]
    public void Check_takes_no_argument(string argument)
    {
        var runner = new FakeRunner();

        var exitCode = Cli.Run(["check", argument], Repository.Root, runner, new StringWriter(), new StringWriter());

        exitCode.ShouldBe(2);
        runner.Commands.ShouldBeEmpty();
    }

    [Theory]
    [InlineData]
    [InlineData("init")]
    [InlineData("map")]
    public void An_unknown_or_missing_command_exits_2_with_the_usage(params string[] args)
    {
        var (exitCode, output, error) = Run(args);

        exitCode.ShouldBe(2);
        output.ShouldBeEmpty();
        error.ShouldContain(Cli.Usage);
    }

    [Fact]
    public void Help_prints_the_usage_and_exits_0()
    {
        var (exitCode, output, _) = Run("--help");

        exitCode.ShouldBe(0);
        output.ShouldContain("check");
        output.ShouldContain("explain <rule>");
    }

    [Fact]
    public void Version_prints_the_version_of_the_tool()
    {
        var (exitCode, output, _) = Run("--version");

        exitCode.ShouldBe(0);
        output.Trim().ShouldBe(ToolVersion.Current);
    }

    private static (int ExitCode, string Output, string Error) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = Cli.Run(args, Repository.Root, new FakeRunner(), output, error);
        return (exitCode, output.ToString(), error.ToString());
    }
}
