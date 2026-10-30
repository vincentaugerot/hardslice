using System.ComponentModel;
using System.Diagnostics;

namespace HardSlice.Tool;

internal interface ICommandRunner
{
    // Runs a command in the console of the tool: its output is never captured nor reinterpreted.
    int Run(string fileName, IReadOnlyList<string> arguments, string workingDirectory);

    // Runs a command with its output discarded: true if it exits 0, false if it fails or cannot start.
    bool Succeeds(string fileName, IReadOnlyList<string> arguments, string workingDirectory);
}

internal sealed class ProcessRunner : ICommandRunner
{
    public int Run(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
    {
        using var process = Process.Start(StartInfo(fileName, arguments, workingDirectory))!;
        process.WaitForExit();
        return process.ExitCode;
    }

    public bool Succeeds(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
    {
        var startInfo = StartInfo(fileName, arguments, workingDirectory);
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        try
        {
            using var process = Process.Start(startInfo)!;
            // Both streams are drained so that a verbose command never blocks on a full pipe.
            process.OutputDataReceived += (_, _) => { };
            process.ErrorDataReceived += (_, _) => { };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            // The command is not on the PATH.
            return false;
        }
    }

    private static ProcessStartInfo StartInfo(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo(fileName) { WorkingDirectory = workingDirectory, UseShellExecute = false };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);
        return startInfo;
    }
}
