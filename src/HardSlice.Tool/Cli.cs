namespace HardSlice.Tool;

internal static class Cli
{
    // Exit code of a command line the tool does not understand, as for an unknown rule.
    public const int UsageError = 2;

    public const string Usage = """
        Usage: dotnet hardslice <command>

        Commands:
          check           Builds the solution, then runs its unit, architecture and integration tests,
                          and fails while a TODO(hardslice): marker is left in src/ or tests/.
                          Exits with the code of the first step that fails. The only verdict on the work.
          check --build   The incremental build alone, errors only, then the TODO(hardslice): markers:
                          the verification after an edit. Its success does not mean the work is done.
          explain <rule>  Prints the sheet of a HardSlice rule, such as HS016.
          hook claude <event>
                          The hooks of Claude Code in .claude/settings.json, for PreToolUse, PostToolUse and Stop:
                          reads the payload of Claude Code on the standard input, refuses the edit of a protected file,
                          runs check --build after the edit of a .cs file and check before the agent hands back.
                          Exits with code 2 to hand the failure to the agent, and with code 0 under Copilot CLI.

        Options:
          --help          Prints this help.
          --version       Prints the version of the tool.
        """;

    // Where the end hook counts its relaunches for each failure, one file per session.
    public static string HookStateDirectory { get; } = Path.Combine(Path.GetTempPath(), "hardslice", "hooks");

    public static int Run(string[] args, string currentDirectory, ICommandRunner runner, TextWriter output, TextWriter error) =>
        Run(args, currentDirectory, runner, output, error, TextReader.Null, _ => null, HookStateDirectory);

    public static int Run(
        string[] args, string currentDirectory, ICommandRunner runner, TextWriter output, TextWriter error,
        TextReader input, Func<string, string?> environment, string hookStateDirectory)
    {
        switch (args)
        {
            case ["hook", "claude", var eventName] when ClaudeHook.Events.Contains(eventName):
                return new ClaudeHook(runner, output, error, environment, hookStateDirectory).Run(eventName, input, currentDirectory);
            case ["hook", ..]:
                // Never 2, which Claude Code would read as a refusal of the edit or of the end of the turn: it shows another code to the user.
                error.WriteLine($"error: hook takes a CLI and one of its events: 'hook claude {string.Join("|", ClaudeHook.Events)}'. Unknown arguments: '{string.Join(' ', args[1..])}'.");
                return CheckCommand.Failure;
            case ["check"]:
                return new CheckCommand(runner, output, error).Run(currentDirectory, CheckMode.Full);
            case ["check", "--build"]:
                return new CheckCommand(runner, output, error).Run(currentDirectory, CheckMode.Build);
            case ["check", ..]:
                // Only the modes of the hooks reduce the verification; no option skips a step of the full check.
                error.WriteLine($"error: check takes no argument, or --build for the incremental build alone. Unknown arguments: '{string.Join(' ', args[1..])}'.");
                return UsageError;
            case ["explain", var ruleId]:
                return Explain(ruleId, output, error);
            case ["explain", ..]:
                error.WriteLine($"error: explain takes one rule, such as HS016. Rules with a sheet: {string.Join(", ", RuleSheets.Ids)}.");
                return UsageError;
            case ["--help" or "-h" or "help"]:
                output.WriteLine(Usage);
                return 0;
            case ["--version"]:
                output.WriteLine(ToolVersion.Current);
                return 0;
            case []:
                error.WriteLine(Usage);
                return UsageError;
            default:
                error.WriteLine($"error: unknown command '{string.Join(' ', args)}'.");
                error.WriteLine(Usage);
                return UsageError;
        }
    }

    private static int Explain(string ruleId, TextWriter output, TextWriter error)
    {
        var sheet = RuleSheets.Find(ruleId);
        if (sheet is null)
        {
            error.WriteLine($"error: '{ruleId}' is not a HardSlice rule with a sheet. Rules with a sheet: {string.Join(", ", RuleSheets.Ids)}.");
            return UsageError;
        }

        output.Write(sheet);
        return 0;
    }
}
