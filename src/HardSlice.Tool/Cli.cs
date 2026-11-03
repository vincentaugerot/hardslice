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
                          Stops at the first step that fails, its output left as is. The only verdict on the work.
          check --build   The incremental build alone, errors only, then the TODO(hardslice): markers:
                          the verification after an edit. Its success does not mean the work is done.
          explain <rule>  Prints the sheet of a HardSlice rule, such as HS016.
          protected       Reads paths relative to the root of the solution on the standard input, separated by NUL
                          characters (git diff --name-only -z, recommended) or one per line, and lists those that agents
                          never edit. Exits 1 when one is protected, 0 when none is, 2 on a line that git quoted, which
                          only -z avoids: the CI of the solution runs it on the files of a pull request, which a
                          maintainer approves with the label hardslice-approved.
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

        Options:
          --help          Prints this help.
          --version       Prints the version of the tool.

        Exit codes of check and check --build: 0 the check passed, 1 a step failed, 2 a wrong command line only.
        """;

    // Where the end hook counts its relaunches for each failure, one file per session, in a folder of the user: a shared folder,
    // created by the first account of the machine that ran a hook, would refuse the writes of every other account.
    public static string HookStateDirectory { get; } = UserStateDirectory(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Path.GetTempPath(), Environment.UserName);

    public static string UserStateDirectory(string localApplicationData, string temporary, string userName) =>
        string.IsNullOrEmpty(localApplicationData)
            ? Path.Combine(temporary, $"hardslice-{string.Concat(userName.Select(c => char.IsAsciiLetterOrDigit(c) ? c : '_'))}", "hooks")
            : Path.Combine(localApplicationData, "hardslice", "hooks");

    public static int Run(string[] args, string currentDirectory, ICommandRunner runner, TextWriter output, TextWriter error) =>
        Run(args, currentDirectory, runner, output, error, TextReader.Null, _ => null, HookStateDirectory);

    public static int Run(
        string[] args, string currentDirectory, ICommandRunner runner, TextWriter output, TextWriter error,
        TextReader input, Func<string, string?> environment, string hookStateDirectory)
    {
        switch (args)
        {
            case ["hook", var name, var eventName] when HookCli.Find(name) is { } cli && cli.Events.Contains(eventName):
                return new AgentHook(cli, runner, output, error, environment, hookStateDirectory).Run(eventName, input, currentDirectory);
            case ["hook", ..]:
                // Never 2, which Claude Code and Codex would read as a refusal of the edit or of the end of the turn: they show another code to the user.
                var forms = string.Join(", ", HookCli.All.Select(cli => $"'hook {cli.Name} {string.Join("|", cli.Events)}'"));
                error.WriteLine($"error: hook takes a CLI and one of its events: {forms}. Unknown arguments: '{string.Join(' ', args[1..])}'.");
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
            case ["protected"]:
                return ProtectedCommand.Run(currentDirectory, input, output, error);
            case ["protected", ..]:
                error.WriteLine($"error: protected takes no argument: it reads the paths on its standard input. Unknown arguments: '{string.Join(' ', args[1..])}'.");
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
