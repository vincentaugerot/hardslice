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

internal sealed class ProcessRunner(Func<string, string?> environment, TextWriter? writer = null) : ICommandRunner
{
    // Set by the dotnet muxer for the processes it starts: the muxer that runs this tool, even when 'dotnet' is not on the PATH.
    public const string HostPathVariable = "DOTNET_HOST_PATH";

    // The program to start for the file name: dotnet is the muxer of DOTNET_HOST_PATH when that names a file.
    public static string Resolve(string fileName, Func<string, string?> environment) =>
        fileName == "dotnet" && environment(HostPathVariable) is { Length: > 0 } host && File.Exists(host) ? host : fileName;

    public int Run(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
    {
        try
        {
            return writer is null ? RunInConsole(fileName, arguments, workingDirectory) : RunCaptured(fileName, arguments, workingDirectory);
        }
        catch (Win32Exception exception)
        {
            // The command cannot start, for example dotnet absent from the PATH: a failed step, never a crash of the tool.
            var message = $"error: '{fileName}' cannot start: {exception.Message}";
            if (writer is null)
                Console.Error.WriteLine(message);
            else
                Write(message);
            return CheckCommand.Failure;
        }
    }

    private int RunInConsole(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
    {
        using var console = Process.Start(StartInfo(fileName, arguments, workingDirectory))!;
        console.WaitForExit();
        return console.ExitCode;
    }

    private int RunCaptured(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
    {
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

    public ICommandRunner WithOutput(TextWriter output) => new ProcessRunner(environment, output);

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

    private ProcessStartInfo StartInfo(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo(Resolve(fileName, environment)) { WorkingDirectory = workingDirectory, UseShellExecute = false };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);
        return startInfo;
    }
}
