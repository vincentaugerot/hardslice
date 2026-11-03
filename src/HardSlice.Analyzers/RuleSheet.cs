using System.Reflection;

namespace HardSlice.Analyzers;

// The sheet of a rule on GitHub, at the tag of this version of the analyzers: the IDE opens it from the rule id.
// A generated solution carries the same sheet in docs/rules/, the path its diagnostics name after 'Doc:'.
internal static class RuleSheet
{
    private const string Repository = "https://github.com/vincentaugerot/hardslice";

    private const string SheetsPath = "src/HardSlice.Templates/content/hardslice-api/docs/rules";

    // 'v0.3.0-preview.1' for the informational version '0.3.0-preview.1+<commit>'.
    private static readonly string Tag = "v" + Version();

    public static string Uri(string ruleId) => $"{Repository}/blob/{Tag}/{SheetsPath}/{ruleId}.md";

    private static string Version()
    {
        var version = typeof(RuleSheet).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(RuleSheet).Assembly.GetName().Version?.ToString(3)
            ?? "0.0.0";
        var metadata = version.IndexOf('+');
        return metadata < 0 ? version : version.Substring(0, metadata);
    }
}
