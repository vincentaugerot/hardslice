using System.Diagnostics;

namespace HardSlice.Analyzers.Tests;

// Runs the dotnet CLI of the tests in a directory, with its own NuGet package folder when one is given.
internal static class Dotnet
{
    public static async Task<DotnetResult> RunAsync(
        string workingDirectory,
        string? nugetPackagesDirectory,
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        if (nugetPackagesDirectory is not null)
            startInfo.Environment["NUGET_PACKAGES"] = nugetPackagesDirectory;
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
            throw new InvalidOperationException("The dotnet process could not be started.");

        try
        {
            var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            return new DotnetResult(process.ExitCode, await standardOutput, await standardError);
        }
        finally
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) when (process.HasExited)
            {
                // The process exited between the state check and Kill.
            }

            await process.WaitForExitAsync(CancellationToken.None);
        }
    }
}

internal sealed record DotnetResult(int ExitCode, string StandardOutput, string StandardError)
{
    public string Output => $"{StandardOutput}{Environment.NewLine}{StandardError}";
}
