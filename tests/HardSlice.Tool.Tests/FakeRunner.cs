namespace HardSlice.Tool.Tests;

// Records the commands check runs, and answers with the exit code given for the first one that starts with the given text,
// after printing the output given with it to the writer of WithOutput.
internal sealed class FakeRunner : ICommandRunner
{
    private readonly Dictionary<string, int> _exitCodes = [];
    private readonly Dictionary<string, string> _outputs = [];
    private TextWriter? _writer;

    public List<string> Commands { get; } = [];

    public List<string> WorkingDirectories { get; } = [];

    public FakeRunner Failing(string command, int exitCode, string? output = null)
    {
        _exitCodes[command] = exitCode;
        if (output is not null)
            _outputs[command] = output;
        return this;
    }

    // Commands run while the writer is set print their output to it: the runner of a hook.
    public ICommandRunner WithOutput(TextWriter writer)
    {
        _writer = writer;
        return this;
    }

    public int Run(string fileName, IReadOnlyList<string> arguments, string workingDirectory) =>
        Record(fileName, arguments, workingDirectory);

    public bool Succeeds(string fileName, IReadOnlyList<string> arguments, string workingDirectory) =>
        Record(fileName, arguments, workingDirectory) == 0;

    private int Record(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
    {
        var command = string.Join(' ', [fileName, .. arguments]);
        Commands.Add(command);
        WorkingDirectories.Add(workingDirectory);
        var output = _outputs.FirstOrDefault(output => command.StartsWith(output.Key, StringComparison.Ordinal)).Value;
        if (output is not null)
            _writer?.WriteLine(output);
        return _exitCodes.FirstOrDefault(exitCode => command.StartsWith(exitCode.Key, StringComparison.Ordinal)).Value;
    }
}
