namespace HardSlice.Tool.Tests;

// Records the commands check runs, and answers with the exit code given for the first one that starts with the given text.
internal sealed class FakeRunner : ICommandRunner
{
    private readonly Dictionary<string, int> _exitCodes = [];

    public List<string> Commands { get; } = [];

    public List<string> WorkingDirectories { get; } = [];

    public FakeRunner Failing(string command, int exitCode)
    {
        _exitCodes[command] = exitCode;
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
        return _exitCodes.FirstOrDefault(exitCode => command.StartsWith(exitCode.Key, StringComparison.Ordinal)).Value;
    }
}
