using System.Reflection;

namespace HardSlice.Tool;

// The sheets of docs/rules/ in the hardslice-api template, embedded at build time by HardSlice.Tool.csproj.
internal static class RuleSheets
{
    private const string ResourcePrefix = "HardSlice.Tool.Rules.";
    private const string ResourceSuffix = ".md";

    private static readonly Assembly Assembly = typeof(RuleSheets).Assembly;

    public static IReadOnlyList<string> Ids { get; } = Assembly.GetManifestResourceNames()
        .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal) && name.EndsWith(ResourceSuffix, StringComparison.Ordinal))
        .Select(name => name[ResourcePrefix.Length..^ResourceSuffix.Length])
        .Order(StringComparer.Ordinal)
        .ToArray();

    // The text of the sheet, or null when no rule has this id. The id is matched without regard to case: hs016 is HS016.
    public static string? Find(string ruleId)
    {
        var id = Ids.FirstOrDefault(id => string.Equals(id, ruleId, StringComparison.OrdinalIgnoreCase));
        if (id is null)
            return null;

        using var stream = Assembly.GetManifestResourceStream(ResourcePrefix + id + ResourceSuffix)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
