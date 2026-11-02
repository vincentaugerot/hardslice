using System.Diagnostics;

namespace HardSlice.Analyzers.Tests;

// A stack overflow terminates its process instead of failing a test. Run these regression cases in a child runner,
// with a deadline and a method filter, so a broken analyzer cannot crash or hang the rest of the test suite.
internal static class IsolatedAnalyzerTest
{
    private const string ChildTest = "HARDSLICE_ISOLATED_ANALYZER_TEST";

    public static async Task RunAsync(Type testClass, string method, Func<Task> test)
    {
        var name = $"{testClass.FullName}.{method}";
        if (Environment.GetEnvironmentVariable(ChildTest) == name)
        {
            await test();
            return;
        }

        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add(testClass.Assembly.Location);
        start.ArgumentList.Add("-method");
        start.ArgumentList.Add(name);
        start.ArgumentList.Add("-noLogo");
        start.ArgumentList.Add("-noColor");
        start.Environment[ChildTest] = name;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(deadline.Token);
            var text = await output + await error;
            process.ExitCode.ShouldBe(0, text[..Math.Min(text.Length, 12_000)]);
        }
        finally
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            await Task.WhenAll(output, error);
        }
    }
}
