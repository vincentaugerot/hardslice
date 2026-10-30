using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace HardSlice.Analyzers;

internal static class RuleActivation
{
    // Rules that do not apply to test projects read IsTestProject, exposed by build/HardSlice.Analyzers.props.
    public static bool IsTestProject(AnalyzerOptions options) =>
        options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.IsTestProject", out var value)
        && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    // A rule is active unless /nowarn, a global config or the .editorconfig of every source file sets it to none.
    // A rule turned off in some files only stays active: it still needs its contracts in the others.
    // A rule off by default, such as HS019, is active only where a setting turns it on.
    public static bool IsActive(Compilation compilation, string ruleId, CancellationToken cancellationToken, bool enabledByDefault = true)
    {
        var options = compilation.Options;
        var projectWide = enabledByDefault;
        if (options.SpecificDiagnosticOptions.TryGetValue(ruleId, out var specific))
        {
            if (specific == ReportDiagnostic.Suppress)
                return false;

            projectWide |= specific != ReportDiagnostic.Default;
        }

        var provider = options.SyntaxTreeOptionsProvider;
        if (provider is not null && provider.TryGetGlobalDiagnosticValue(ruleId, cancellationToken, out var global))
            projectWide = global != ReportDiagnostic.Suppress;

        if (provider is null || !compilation.SyntaxTrees.Any())
            return projectWide;

        foreach (var tree in compilation.SyntaxTrees)
        {
            var active = provider.TryGetDiagnosticValue(tree, ruleId, cancellationToken, out var severity)
                ? severity != ReportDiagnostic.Suppress
                : projectWide;
            if (active)
                return true;
        }

        return false;
    }
}
