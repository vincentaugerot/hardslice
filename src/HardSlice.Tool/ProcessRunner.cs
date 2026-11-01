using System.ComponentModel;
using System.Diagnostics;

namespace HardSlice.Tool;

internal interface ICommandRunner
{
    // Runs a command in the console of the tool: its output is never captured nor reinterpreted.
    int Run(string fileName, IReadOnlyList<string> arguments, string workingDirectory);

    // Runs a command with its output discarded: true if it exits 0, false if it fails or cannot start.
    bool Succeeds(string fileName, IReadOnlyList<string> arguments, string workingDirectory);

    // The same runner, except that Run writes the output and the errors of its commands to the writer, line by line, instead of
    // the console: a hook hands them to the agent on its own streams.
    ICommandRunner WithOutput(TextWriter writer);
}

internal sealed class ProcessRunner(TextWriter? writer = null) : ICommandRunner
{
    public int Run(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
    {
        if (writer is null)
        {
            using var console = Process.Start(StartInfo(fileName, arguments, workingDirectory))!;
            console.WaitForExit();
            return console.ExitCode;
        }

        var startInfo = StartInfo(fileName, arguments, workingDirectory);
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        // A hook has read its payload from the standard input: no command may wait on it.
        startInfo.RedirectStandardInput = true;
        using var process = Process.Start(startInfo)!;
        process.StandardInput.Close();
        process.OutputDataReceived += (_, line) => Write(line.Data);
        process.ErrorDataReceived += (_, line) => Write(line.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
        return process.ExitCode;
    }

    public ICommandRunner WithOutput(TextWriter output) => new ProcessRunner(output);

    private void Write(string? line)
    {
        if (line is null)
            return;
        lock (writer!)
            writer.WriteLine(line);
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
