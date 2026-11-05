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
    [InlineData("HS016", "--help")]
    [InlineData("--help", "-h")]
    public void Explain_takes_exactly_one_rule(params string[] rules)
    {
        var (exitCode, output, error) = Run(["explain", .. rules]);

        exitCode.ShouldBe(2);
        output.ShouldBeEmpty();
        error.ShouldContain("Rules with a sheet: HS000, HS001");
    }

    // No option skips a step of the full check: --build, the mode of the hook after an edit, is the only argument.
    [Theory]
    [InlineData("--skip-integration-tests")]
    [InlineData("--no-build")]
    [InlineData("--affected")]
    [InlineData("--build", "--build")]
    [InlineData("--build", "--no-restore")]
    [InlineData("build")]
    [InlineData("--build", "--help")]
    [InlineData("-h", "--build")]
    public void Check_refuses_any_argument_but_build_with_exit_code_2(params string[] arguments)
    {
        var runner = new FakeRunner();
        var error = new StringWriter();

        var exitCode = Cli.Run(["check", .. arguments], Repository.Root, runner, new StringWriter(), error);

        exitCode.ShouldBe(2);
        runner.Commands.ShouldBeEmpty();
        error.ToString().ShouldBe(
            $"error: check takes no argument, or --build for the incremental build alone. Unknown arguments: '{string.Join(' ', arguments)}'.{Environment.NewLine}");
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
        output.ShouldContain("check --build");
        output.ShouldContain("explain <rule>");
        output.ShouldContain("upgrade");
        output.ShouldContain("Exit codes of upgrade: 0 nothing left to merge (merged files listed, to review), 1 the upgrade failed");
    }

    // PB-086: upgrade takes the variant of the template, or deduces it, and nothing else. A wrong command line runs nothing.
    [Theory]
    [InlineData("--force")]
    [InlineData("--no-sample", "--sample")]
    [InlineData("0.3.0-preview.2")]
    [InlineData("--sample", "--help")]
    [InlineData("--help", "--no-sample")]
    [InlineData("help")]
    public void Upgrade_refuses_any_argument_but_the_variant_with_exit_code_2(params string[] arguments)
    {
        var runner = new FakeRunner();
        var error = new StringWriter();

        var exitCode = Cli.Run(["upgrade", .. arguments], Repository.Root, runner, new StringWriter(), error);

        exitCode.ShouldBe(2);
        runner.Commands.ShouldBeEmpty();
        error.ToString().ShouldBe(
            $"error: upgrade takes no argument, or --sample or --no-sample to choose the variant of the template. Unknown arguments: '{string.Join(' ', arguments)}'.{Environment.NewLine}");
    }

    [Fact]
    public void Short_help_prints_the_usage_as_help_does()
    {
        var (exitCode, output, error) = Run("-h");

        exitCode.ShouldBe(0);
        output.ShouldBe(Cli.Usage + Environment.NewLine);
        output.ShouldBe(Run("--help").Output);
        error.ShouldBeEmpty();
    }

    // PB-091: Ghostline read "error: upgrade takes no argument ... Unknown arguments: '--help'." with code 2, before a command that
    // rewrites protected files. The help of a command is its paragraphs of the usage with its exit codes, and runs nothing.
    [Theory]
    [InlineData("check", "--help")]
    [InlineData("check", "-h")]
    [InlineData("explain", "--help")]
    [InlineData("explain", "-h")]
    [InlineData("protected", "--help")]
    [InlineData("protected", "-h")]
    [InlineData("upgrade", "--help")]
    [InlineData("upgrade", "-h")]
    [InlineData("hook", "--help")]
    [InlineData("hook", "-h")]
    public void Help_of_a_command_prints_its_paragraph_and_exit_codes_and_exits_0(string command, string option)
    {
        var runner = new FakeRunner();
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = Cli.Run([command, option], Repository.Root, runner, output, error);

        exitCode.ShouldBe(0);
        output.ToString().ShouldBe(HelpOf[command].ReplaceLineEndings() + Environment.NewLine);
        error.ToString().ShouldBeEmpty();
        runner.Commands.ShouldBeEmpty();
    }

    // The help never looks at the tree: without it, a tree with changes stops the upgrade with code 1.
    [Fact]
    public void Help_of_upgrade_leaves_a_git_tree_with_changes_as_it_is()
    {
        using var solution = new TemporarySolution();
        var git = new ProcessRunner(Environment.GetEnvironmentVariable);
        void Git(params string[] arguments) =>
            git.Capture("git", arguments, solution.Root).ExitCode.ShouldBe(0, $"git {string.Join(' ', arguments)}");
        solution.Write("src/Demo/Program.cs", "var app = builder.Build();\napp.Run();\n");
        Git("init", "--quiet");
        Git("add", "--all");
        Git("-c", "user.name=Test", "-c", "user.email=test@example.com", "-c", "commit.gpgsign=false", "commit", "--quiet", "-m", "solution");
        solution.Write("src/Demo/Program.cs", "app.Run();\n");
        var status = git.Capture("git", ["status", "--porcelain", "--untracked-files=all"], solution.Root).Output;
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = Cli.Run(["upgrade", "--help"], solution.Root, git, output, error);

        exitCode.ShouldBe(0);
        output.ToString().ShouldBe(HelpOf["upgrade"].ReplaceLineEndings() + Environment.NewLine);
        error.ToString().ShouldBeEmpty();
        git.Capture("git", ["status", "--porcelain", "--untracked-files=all"], solution.Root).Output.ShouldBe(status);
        File.ReadAllText(Path.Combine(solution.Root, "src/Demo/Program.cs")).ShouldBe("app.Run();\n");
    }

    private static readonly Dictionary<string, string> HelpOf = new()
    {
        ["check"] = """
            Usage: dotnet hardslice check

              check           Builds the solution, then runs its unit, architecture and integration tests,
                              and fails while a TODO(hardslice): marker is left in src/ or tests/.
                              Stops at the first step that fails, its output left as is. The only verdict on the work.
              check --build   The incremental build alone, errors only, then the TODO(hardslice): markers:
                              the verification after an edit. Its success does not mean the work is done.

            Exit codes of check and check --build: 0 the check passed, 1 a step failed, 2 a wrong command line only.
            """,
        ["explain"] = """
            Usage: dotnet hardslice explain <rule>

              explain <rule>  Prints the sheet of a HardSlice rule, such as HS016.

            Exit codes of explain: 0 the sheet printed, 2 a rule without a sheet or a wrong command line.
            """,
        ["protected"] = """
            Usage: dotnet hardslice protected

              protected       Reads paths relative to the root of the solution on the standard input, separated by NUL
                              characters (git diff --name-only -z, recommended) or one per line, and lists those that agents
                              never edit. Exits 1 when one is protected, 0 when none is, 2 on a line that git quoted, which
                              only -z avoids: the CI of the solution runs it on the files of a pull request, which a
                              maintainer approves with the label hardslice-approved.
            """,
        ["upgrade"] = """
            Usage: dotnet hardslice upgrade

              upgrade         Brings the solution to the template of the version of the tool, from a git tree without changes
                              but the tool manifest: generates hardslice-api anew under its name in a temporary folder, rewrites
                              the files the template owns (protected files and rule sheets), adds the files the solution lacks,
                              and merges the files of the application in three ways from the generation recorded in
                              .hardslice/generation/: updates those the application did not change, merges with git merge-file
                              those both changed, writing a conflict between git markers, and lists with their diff those
                              without a recorded generation, to merge, and the files the template may have moved with their
                              diff against the new file. Each diff has the headers of git and applies with git apply. Then
                              records the new generation there. Leaves out the sample when the solution has no
                              src/<name>/Features/Bookings/ nor src/<name>.Domain/Bookings/, unless --sample or --no-sample
                              says. Writes protected files: a maintainer runs it, never an agent.

            Exit codes of upgrade: 0 nothing left to merge (merged files listed, to review), 1 the upgrade failed (with a change in the
            git tree, nothing is written), 2 a wrong command line, 3 conflicts to resolve or files left to merge or to review, listed.
            """,
        ["hook"] = """
            Usage: dotnet hardslice hook <cli> <event>

              hook <cli> <event>
                              The hooks of an agent: reads the payload of the CLI on the standard input, refuses the edit
                              of a protected file, runs check --build after the edit of a .cs file and check before the
                              agent hands back, and answers the way the CLI hands it to its agent.
                              hook claude PreToolUse|PostToolUse|Stop     Claude Code, .claude/settings.json: code 2,
                                                                          and code 0 at once under Copilot CLI.
                              hook codex PreToolUse|PostToolUse|Stop      Codex, .codex/hooks.json: code 2.
                              hook copilot preToolUse|postToolUse|agentStop
                                                                          Copilot CLI, .github/hooks/hardslice.json:
                                                                          JSON on the standard output, code 0.

            Exit codes of hook: those of its CLI above, 1 an unknown CLI or event, or a payload that is not JSON.
            """,
    };

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
