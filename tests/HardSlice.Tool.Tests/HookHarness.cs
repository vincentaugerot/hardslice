namespace HardSlice.Tool.Tests;

// Runs dotnet hardslice hook <cli> <event> in a temporary solution with a payload recorded during the trials of PB-034, its
// solution moved to the temporary one, and returns what the CLI would read: the exit code, the standard output and error.
internal sealed class HookHarness : IDisposable
{
    public const string IncrementalBuild = "dotnet build Demo.slnx --no-restore";
    public const string FullBuild = "dotnet build Demo.slnx";
    public const string RecordedRoot = "/home/dev/Demo";

    public const string Hs010 =
        "/home/dev/Demo/src/Demo.Domain/Bookings/BookingStatus.cs(5,12): error HS010: 'BookingStatus.Today' in domain project 'Demo.Domain' reads 'DateTime.Today'. "
        + "Fix: pass the instant as a parameter. Skill: fix-architecture-violation. Doc: 'docs/rules/HS010.md'.";

    private static readonly string Fixtures = Path.Combine(Repository.Root, "tests", "HardSlice.Tool.Tests", "Fixtures", "hooks");

    public TemporarySolution Solution { get; } = new();

    public string State { get; } = Directory.CreateTempSubdirectory("hardslice-hook-state-").FullName;

    public string Root => Solution.Root;

    public (int ExitCode, string Output, string Error) Run(string cli, string eventName, string payload, FakeRunner runner, params string[] environment)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = Cli.Run(
            ["hook", cli, eventName], Root, runner, output, error, new StringReader(payload),
            name => environment.Contains(name) ? "1" : null, State);
        return (exitCode, output.ToString(), error.ToString());
    }

    // A recorded payload, with the paths of its solution moved to the temporary one.
    public string Payload(string fixture) =>
        File.ReadAllText(Path.Combine(Fixtures, fixture)).Replace(RecordedRoot + "/", Root + "/").Replace($"\"{RecordedRoot}\"", $"\"{Root}\"");

    public void Dispose()
    {
        Solution.Dispose();
        Directory.Delete(State, recursive: true);
    }
}
