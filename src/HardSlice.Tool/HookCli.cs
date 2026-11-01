using System.Text.Json;

namespace HardSlice.Tool;

// The three steps of the loop of an agent that a hook can take part in.
internal enum HookStep
{
    BeforeEdit,
    AfterEdit,
    BeforeHandingBack,
}

// What a CLI sends its hooks and how it reads their answer, as recorded by PB-034 (docs/agents/support.md): the names of its events,
// where its payload names the edited files and the session, and how a hook refuses an edit, hands errors to the agent after an
// edit, keeps the agent working or lets it finish. Each answer writes to the hook's output and returns its exit code.
internal abstract class HookCli
{
    public static IReadOnlyList<HookCli> All { get; } = [new ClaudeCodeHooks(), new CodexHooks(), new CopilotHooks()];

    // The name in dotnet hardslice hook <name> <event>.
    public abstract string Name { get; }

    public abstract string DisplayName { get; }

    // The events of the CLI, in the order of the steps of HookStep.
    public abstract IReadOnlyList<string> Events { get; }

    public static HookCli? Find(string name) => All.FirstOrDefault(cli => cli.Name == name);

    public HookStep Step(string eventName) => (HookStep)Events.ToList().IndexOf(eventName);

    // A hook that does nothing in this environment exits 0 at once.
    public virtual bool IsInactive(Func<string, string?> environment) => false;

    // The files the tool call edits, as the payload names them, absolute or relative to its cwd; none for another tool.
    public abstract IReadOnlyList<string> EditedFiles(JsonElement payload);

    public abstract string? SessionId(JsonElement payload);

    public abstract int Refuse(string reason, TextWriter output, TextWriter error);

    public abstract int ReportAfterEdit(string errors, TextWriter output, TextWriter error);

    public abstract int KeepWorking(string reason, TextWriter output, TextWriter error);

    // Lets the agent finish, showing the message to the user when the CLI can.
    public abstract int LetFinish(string message, TextWriter output);

    public static string? Text(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    protected static JsonElement Property(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) ? value : default;

    protected static void WriteJson(TextWriter output, Dictionary<string, string> answer) =>
        output.WriteLine(JsonSerializer.Serialize(answer));
}

// Claude Code and Codex read the same answers: the code 2 hands the standard error to the agent, as the refusal of the edit, the
// errors to fix after it, or the instruction to go on at the end of the turn; a JSON systemMessage on the standard output with the
// code 0 is shown to the user.
internal abstract class ExitCodeHookCli : HookCli
{
    public const int Block = 2;

    public override IReadOnlyList<string> Events { get; } = ["PreToolUse", "PostToolUse", "Stop"];

    public override string? SessionId(JsonElement payload) => Text(payload, "session_id");

    public override int Refuse(string reason, TextWriter output, TextWriter error) => ToAgent(reason, error);

    public override int ReportAfterEdit(string errors, TextWriter output, TextWriter error) => ToAgent(errors, error);

    public override int KeepWorking(string reason, TextWriter output, TextWriter error) => ToAgent(reason, error);

    public override int LetFinish(string message, TextWriter output)
    {
        WriteJson(output, new() { ["systemMessage"] = message });
        return 0;
    }

    private static int ToAgent(string message, TextWriter error)
    {
        error.Write(message);
        if (!message.EndsWith('\n'))
            error.WriteLine();
        return Block;
    }
}

// Claude Code, .claude/settings.json: the edited file is tool_input.file_path of Edit and Write.
internal sealed class ClaudeCodeHooks : ExitCodeHookCli
{
    // Copilot CLI runs the hooks of .claude/settings.json too, sets this variable in their environment and does not hand their
    // code 2 to the agent: under Copilot, these hooks would only double its own, those of .github/hooks/hardslice.json.
    public const string CopilotVariable = "COPILOT_CLI";

    public override string Name => "claude";

    public override string DisplayName => "Claude Code";

    public override bool IsInactive(Func<string, string?> environment) => !string.IsNullOrEmpty(environment(CopilotVariable));

    public override IReadOnlyList<string> EditedFiles(JsonElement payload) =>
        Text(Property(payload, "tool_input"), "file_path") is { Length: > 0 } file ? [file] : [];
}

// Codex, .codex/hooks.json: its edits go through apply_patch, which the matcher Edit|Write covers, and the payload keeps the whole
// patch in tool_input.command, one header line per file it adds, updates, deletes or moves, several files per call.
internal sealed class CodexHooks : ExitCodeHookCli
{
    private static readonly string[] FileHeaders = ["*** Add File: ", "*** Update File: ", "*** Delete File: ", "*** Move to: "];

    public override string Name => "codex";

    public override string DisplayName => "Codex";

    public override IReadOnlyList<string> EditedFiles(JsonElement payload)
    {
        var input = Property(payload, "tool_input");
        var files = new List<string>();
        if (Text(input, "file_path") is { Length: > 0 } file)
            files.Add(file);

        foreach (var line in (Text(input, "command") ?? "").Split('\n'))
        {
            var header = FileHeaders.FirstOrDefault(header => line.StartsWith(header, StringComparison.Ordinal));
            if (header is not null && line[header.Length..].Trim() is { Length: > 0 } path)
                files.Add(path);
        }

        return files;
    }
}

// Copilot CLI, .github/hooks/hardslice.json: a camelCase payload, the edited file in toolArgs.path of edit and create, and answers
// in JSON on the standard output with the code 0. Its code 2 after a tool is only shown to the user, and it has no message to the
// user, so a passing check stays silent.
internal sealed class CopilotHooks : HookCli
{
    // Copilot appends additionalContext to the result of the tool up to 10 KB: longer errors are cut, with a pointer to the rest.
    public const int AdditionalContextLimit = 9_000;

    private static readonly string[] EditTools = ["edit", "create"];

    public override string Name => "copilot";

    public override string DisplayName => "Copilot CLI";

    public override IReadOnlyList<string> Events { get; } = ["preToolUse", "postToolUse", "agentStop"];

    public override string? SessionId(JsonElement payload) => Text(payload, "sessionId");

    public override IReadOnlyList<string> EditedFiles(JsonElement payload) =>
        Text(payload, "toolName") is { } tool && EditTools.Contains(tool) && Text(Property(payload, "toolArgs"), "path") is { Length: > 0 } file ? [file] : [];

    public override int Refuse(string reason, TextWriter output, TextWriter error)
    {
        WriteJson(output, new() { ["permissionDecision"] = "deny", ["permissionDecisionReason"] = reason });
        return 0;
    }

    public override int ReportAfterEdit(string errors, TextWriter output, TextWriter error)
    {
        var context = errors.Length <= AdditionalContextLimit
            ? errors
            : errors[..AdditionalContextLimit] + "\n[HardSlice: errors cut here. Run scripts/check.sh --build to see all of them.]";
        WriteJson(output, new() { ["additionalContext"] = context });
        return 0;
    }

    public override int KeepWorking(string reason, TextWriter output, TextWriter error)
    {
        WriteJson(output, new() { ["decision"] = "block", ["reason"] = reason });
        return 0;
    }

    public override int LetFinish(string message, TextWriter output) => 0;
}
