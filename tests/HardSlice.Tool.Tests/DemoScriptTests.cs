using System.Diagnostics;
using System.Text.RegularExpressions;

namespace HardSlice.Tool.Tests;

// PB-057: the verifiers of demos/ fail when a step fails. Bash ignores set -e in a command that an if or a list tests, functions
// and subshells included: verify-recorded and verify-reference used to accept a candidate whose trusted check failed.
public sealed class DemoScriptTests
{
    [Fact]
    public void Run_logged_sets_the_status_of_the_first_failed_step()
    {
        var log = Path.Combine(Path.GetTempPath(), $"hardslice-demo-{Guid.NewGuid():N}.log");
        var script = $$"""
            set -euo pipefail
            source scripts/demo-common.sh
            failing() { false; echo "after the failed step"; }
            passing() { true; echo "every step passed"; }
            run_logged '{{log}}' failing
            echo "failing $verification_status $(cat '{{log}}')"
            run_logged '{{log}}' passing
            echo "passing $verification_status $(cat '{{log}}')"
            """;

        var (exitCode, output) = Bash(script);

        exitCode.ShouldBe(0, output);
        output.Split('\n', StringSplitOptions.RemoveEmptyEntries).ShouldBe(["failing 1 ", "passing 0 every step passed"]);
    }

    [Fact]
    public void No_demonstration_script_tests_a_verifier_as_a_condition()
    {
        var condition = new Regex(@"(^\s*(if|elif|while|until)\b|!|&&|\|\|)\s*\(?\s*(run_logged\s+\S+\s+)?\w*verify_candidate\b");

        Directory.GetFiles(Path.Combine(Repository.Root, "scripts"), "demo-*.sh")
            .SelectMany(script => File.ReadAllLines(script).Select((line, index) => (script, line, number: index + 1)))
            .Where(line => condition.IsMatch(line.line))
            .Select(line => $"{Path.GetFileName(line.script)}:{line.number}: {line.line.Trim()}")
            .ShouldBeEmpty();
    }

    private static (int ExitCode, string Output) Bash(string script)
    {
        var start = new ProcessStartInfo("bash")
        {
            WorkingDirectory = Repository.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add(script);

        using var process = Process.Start(start) ?? throw new InvalidOperationException("bash did not start.");
        var error = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output + error.Result);
    }
}
