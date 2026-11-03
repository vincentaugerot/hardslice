using System.Diagnostics;
using System.Text;

namespace HardSlice.Tool.Tests;

// PB-055: what the tool needs from the machine, outside the logic of check and of the hooks.
public sealed class ProcessRunnerTests
{
    // The muxer that runs the tool, even when dotnet is not on the PATH.
    [Fact]
    public void Dotnet_is_the_muxer_of_DOTNET_HOST_PATH_when_it_names_a_file()
    {
        var muxer = typeof(ProcessRunnerTests).Assembly.Location;

        ProcessRunner.Resolve("dotnet", name => name == ProcessRunner.HostPathVariable ? muxer : null).ShouldBe(muxer);
        ProcessRunner.Resolve("dotnet", _ => null).ShouldBe("dotnet");
        ProcessRunner.Resolve("dotnet", _ => "/no/such/dotnet").ShouldBe("dotnet");
        ProcessRunner.Resolve("docker", _ => muxer).ShouldBe("docker");
    }

    // A command that cannot start is a failed step with its reason, never a crash of the tool.
    [Fact]
    public void A_command_that_cannot_start_fails_with_code_1_and_its_reason()
    {
        var output = new StringWriter();

        var exitCode = new ProcessRunner(_ => null).WithOutput(output).Run("hardslice-no-such-command", [], Path.GetTempPath());

        exitCode.ShouldBe(CheckCommand.Failure);
        output.ToString().ShouldStartWith("error: 'hardslice-no-such-command' cannot start: ");
    }

    // A folder of the user: the first account that runs a hook never owns the state of the others.
    [Fact]
    public void The_state_of_the_hooks_lives_in_a_folder_of_the_user()
    {
        Cli.UserStateDirectory("/home/dev/.local/share", "/tmp/", "dev").ShouldBe(Path.Combine("/home/dev/.local/share", "hardslice", "hooks"));
        Cli.UserStateDirectory("", "/tmp/", "dev.ops").ShouldBe(Path.Combine("/tmp/", "hardslice-dev_ops", "hooks"));
        Cli.HookStateDirectory.ShouldNotStartWith(Path.Combine(Path.GetTempPath(), "hardslice", "hooks"));
    }

    [Theory]
    [InlineData("booking 3f2b8c1e-4d5a-4b6c-9d7e-8f9a0b1c2d3e not found", "booking 7a1c2e3f-0b9d-4e8a-a6c5-1d2e3f4a5b6c not found")]
    [InlineData("container testcontainers-3f2b8c1e4d5a not ready", "container testcontainers-a9b8c7d6e5f4 not ready")]
    [InlineData("error CS0103 at 0x7ffd3a2b1c40", "error CS0103 at 0x55aa12bc34de")]
    public void The_fingerprint_of_a_failure_leaves_its_identifiers_out(string failure, string sameFailure) =>
        StopRetries.Fingerprint("error: " + failure).ShouldBe(StopRetries.Fingerprint("error: " + sameFailure));

    [Fact]
    public void The_fingerprint_of_a_failure_keeps_its_words() =>
        StopRetries.Fingerprint("error: the name 'deadbeefcafe' is unknown").ShouldNotBe(StopRetries.Fingerprint("error: the name 'facadebeaded' is unknown"));

    // Claude Code and Copilot CLI write the payload in UTF-8. Decoded in the code page of the console, here a Latin-1 locale, the
    // path of a solution under C:\Users\Clément\ or /home/clément/ was no longer found, and its protected files were not refused.
    [Fact]
    public async Task A_hook_reads_its_payload_in_UTF8_whatever_the_encoding_of_the_console()
    {
        if (OperatingSystem.IsWindows())
            Assert.Skip("The locale that sets the encoding of the console is read on Linux and macOS.");

        var cancellationToken = TestContext.Current.CancellationToken;
        var parent = Directory.CreateTempSubdirectory("hardslice-utf8-").FullName;
        try
        {
            var root = Path.Combine(parent, "Clément", "Demo");
            Directory.CreateDirectory(Path.Combine(root, ".config"));
            File.WriteAllText(Path.Combine(root, ".config", "dotnet-tools.json"), "{}");
            File.WriteAllText(Path.Combine(root, "Demo.slnx"), "<Solution />");
            var payload = $$$"""{"session_id":"utf8","cwd":"{{{root}}}","tool_name":"Edit","tool_input":{"file_path":"{{{root}}}/scripts/check.sh"}}""";

            var startInfo = new ProcessStartInfo(Environment.GetEnvironmentVariable(ProcessRunner.HostPathVariable) is { Length: > 0 } host ? host : "dotnet")
            {
                WorkingDirectory = root,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false),
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(typeof(Cli).Assembly.Location);
            foreach (var argument in new[] { "hook", "claude", "PreToolUse" })
                startInfo.ArgumentList.Add(argument);
            startInfo.Environment["LANG"] = "en_US.ISO-8859-1";
            startInfo.Environment["LC_ALL"] = "en_US.ISO-8859-1";

            using var process = Process.Start(startInfo)!;
            await process.StandardInput.WriteAsync(payload);
            process.StandardInput.Close();
            var error = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            process.ExitCode.ShouldBe(2, await error);
            (await error).ShouldStartWith("HardSlice: 'scripts/check.sh' is protected (scripts/)");
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }
}
