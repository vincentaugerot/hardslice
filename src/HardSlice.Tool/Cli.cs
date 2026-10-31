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

        Options:
          --help          Prints this help.
          --version       Prints the version of the tool.
        """;

    public static int Run(string[] args, string currentDirectory, ICommandRunner runner, TextWriter output, TextWriter error)
    {
        switch (args)
        {
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
