using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;

namespace HardSlice.Tool.Tests;

// PB-057: scripts/agent-trace.py, which records the traces of demos/add-feature/ and reads the commands they run.
public sealed class AgentTraceTests
{
    private static readonly string Script = Path.Combine(Repository.Root, "scripts", "agent-trace.py");

    public static TheoryData<string> RecordedTraces() =>
        [.. Directory.GetDirectories(Path.Combine(Repository.Root, "demos", "add-feature", "runs"))
            .Where(run => File.Exists(Path.Combine(run, "trace.jsonl")))
            .Select(run => Path.GetFileName(run))
            .Order(StringComparer.Ordinal)];

    // A published trace holds neither the inventory of the CLI nor encrypted reasoning: filtering it again changes nothing.
    [Theory]
    [MemberData(nameof(RecordedTraces))]
    public void A_recorded_trace_is_kept_as_filter_prints_it(string run)
    {
        var trace = Path.Combine(Repository.Root, "demos", "add-feature", "runs", run, "trace.jsonl");

        var result = Run("filter", trace);

        result.ExitCode.ShouldBe(0, result.Error);
        result.Output.ShouldBe(File.ReadAllText(trace));
        result.Output.ShouldNotContain("\"signature\":");
        result.Output.ShouldNotContain("\"reasoningOpaque\":");
        result.Output.ShouldNotContain("\"rate_limit_event\"");
    }

    [Fact]
    public void Filter_keeps_of_the_claude_code_initialization_only_what_identifies_the_run()
    {
        var trace = Trace(
            """{"type":"system","subtype":"init","cwd":"/work/x/Shop","session_id":"s1","tools":["Bash","DesignSync"],"mcp_servers":[{"name":"private"}],"model":"claude-opus-5-5","permissionMode":"acceptEdits","slash_commands":["send-to-project"],"claude_code_version":"2.1.316","skills":["add-module","design"],"plugins":[{"name":"p","path":"/home/me/.claude/plugins/p"}],"memory_paths":{"auto":"/home/me/.claude/projects/-work-x-Shop/memory/"},"messaging_socket_path":"/run/user/0/cc-socks/1.sock"}""",
            """{"type":"rate_limit_event","rate_limit_info":{"status":"allowed","rateLimitType":"seven_day"},"session_id":"s1"}""",
            """{"type":"assistant","message":{"content":[{"type":"thinking","thinking":"","signature":"CAQSngUK"},{"type":"redacted_thinking","data":"EvUE"},{"type":"text","text":"Reading /work/x/Shop/AGENTS.md"}]},"session_id":"s1"}""",
            """{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"t1","content":"done by Clément"}]},"session_id":"s1"}""");

        var result = Run("filter", trace, "/work/x", "/home/me");

        result.ExitCode.ShouldBe(0, result.Error);
        Lines(result.Output).ShouldBe(
        [
            """{"type":"system","subtype":"init","session_id":"s1","model":"claude-opus-5-5","cwd":"/home/dev/work/Shop","claude_code_version":"2.1.316","permissionMode":"acceptEdits"}""",
            """{"type":"assistant","message":{"content":[{"type":"thinking","thinking":""},{"type":"text","text":"Reading /home/dev/work/Shop/AGENTS.md"}]},"session_id":"s1"}""",
            // A line the filter does not change is kept byte for byte.
            """{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"t1","content":"done by Clément"}]},"session_id":"s1"}""",
        ]);
        Run("format", WriteTrace(result.Output)).Output.Trim().ShouldBe("claude");
    }

    [Fact]
    public void Filter_leaves_out_the_streaming_events_the_tool_list_and_the_opaque_reasoning_of_copilot()
    {
        var trace = Trace(
            """{"type":"assistant.message_delta","data":{"deltaContent":"x"},"ephemeral":true}""",
            """{"type":"assistant.message","data":{"content":"","reasoningText":"Let me add the module.","reasoningOpaque":"EvUE","reasoningBlocks":{"provider":"anthropic-messages","blocks":[{"signature":"EvUE","thinking":"Let me add the module.","type":"thinking"}]}}}""",
            """{"type":"session.usage_checkpoint","data":{"totalNanoAiu":0,"totalPremiumRequests":9,"promptCacheBreakState":[{"models":{"m":{"request_id":"r","tools":[{"name":"bash"}]}}}]}}""");

        var result = Run("filter", trace);

        result.ExitCode.ShouldBe(0, result.Error);
        Lines(result.Output).ShouldBe(
        [
            """{"type":"assistant.message","data":{"content":"","reasoningText":"Let me add the module.","reasoningBlocks":{"provider":"anthropic-messages","blocks":[{"thinking":"Let me add the module.","type":"thinking"}]}}}""",
            """{"type":"session.usage_checkpoint","data":{"totalPremiumRequests":9,"totalNanoAiu":0}}""",
        ]);
    }

    // Only a dotnet new that ran whenever the command succeeded counts, and only from a command that exited with 0.
    [Theory]
    [InlineData("dotnet new hardslice-module --name Catalog", 0, true)]
    [InlineData("/bin/bash -lc 'cd src && dotnet new hardslice-module --name Catalog 2>&1 | tail -3'", 0, true)]
    [InlineData("dotnet build; dotnet new hardslice-module --project src/Shop/Shop.csproj \\\n  --name Catalog > /dev/null", 0, true)]
    [InlineData("A=1 dotnet new hardslice-module --name=Catalog && ls", 0, true)]
    [InlineData("dotnet new hardslice-module --name Catalog", null, false)]
    [InlineData("dotnet new hardslice-module --name Catalog", 1, false)]
    [InlineData("true || dotnet new hardslice-module --name Catalog", 0, false)]
    [InlineData("true ||\n  dotnet new hardslice-module --name Catalog | tail -3", 0, false)]
    [InlineData("true || (ls; dotnet new hardslice-module --name Catalog)", 0, false)]
    [InlineData("echo \"done; dotnet new hardslice-module --name Catalog\"", 0, false)]
    [InlineData("echo dotnet new hardslice-module --name Catalog", 0, false)]
    [InlineData("ls # dotnet new hardslice-module --name Catalog", 0, false)]
    [InlineData("cat > notes.md <<'EOF'\nIt's done:\ndotnet new hardslice-module --name Catalog\nEOF", 0, false)]
    public void Expect_counts_a_dotnet_new_only_when_it_ran(string command, int? exitCode, bool counted)
    {
        var item = new JsonObject
        {
            ["id"] = "item_1",
            ["type"] = "command_execution",
            ["command"] = command,
            ["aggregated_output"] = "",
            ["exit_code"] = exitCode,
            ["status"] = "completed",
        };
        var trace = Trace(
            """{"type":"thread.started","thread_id":"t"}""",
            new JsonObject { ["type"] = "item.completed", ["item"] = item }.ToJsonString());

        var result = Run("expect", trace, "hardslice-module:--name=Catalog");

        result.ExitCode.ShouldBe(counted ? 0 : 1, result.Output + result.Error);
    }

    private static string[] Lines(string output) => output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static string Trace(params string[] lines) => WriteTrace(string.Join('\n', lines) + "\n");

    private static string WriteTrace(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"hardslice-trace-{Guid.NewGuid():N}.jsonl");
        File.WriteAllText(path, content, new UTF8Encoding(false));
        return path;
    }

    private static (int ExitCode, string Output, string Error) Run(params string[] arguments)
    {
        var start = new ProcessStartInfo("python3")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        start.Environment["PYTHONIOENCODING"] = "utf-8";
        start.ArgumentList.Add("-I");
        start.ArgumentList.Add(Script);
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start) ?? throw new InvalidOperationException("python3 did not start.");
        var error = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output, error.Result);
    }
}
