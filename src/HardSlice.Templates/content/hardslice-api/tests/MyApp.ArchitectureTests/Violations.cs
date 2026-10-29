namespace MyApp.ArchitectureTests;

internal static class Violations
{
    // One line per violation, in the format of the HardSlice analyzers:
    // 'HSxxx: <offending symbol>. <the rule in one sentence>. Fix: <expected fix>. Doc: 'docs/rules/HSxxx.md'.'
    public static void ShouldBeNone(IEnumerable<string> violations)
    {
        var lines = violations.Distinct().Order(StringComparer.Ordinal).ToArray();
        if (lines.Length > 0)
            Assert.Fail(string.Join(Environment.NewLine, lines));
    }
}
