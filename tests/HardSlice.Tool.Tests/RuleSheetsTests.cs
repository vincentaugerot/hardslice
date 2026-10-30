using System.Text.RegularExpressions;

namespace HardSlice.Tool.Tests;

// The template ships the sheets and the tool embeds them: both come from docs/rules/ of the template and must never differ.
public sealed class RuleSheetsTests
{
    [Fact]
    public void Each_embedded_sheet_is_the_text_the_template_ships()
    {
        RuleSheets.Ids.ShouldNotBeEmpty();
        foreach (var id in RuleSheets.Ids)
            RuleSheets.Find(id).ShouldBe(File.ReadAllText(Path.Combine(Repository.RuleSheets, $"{id}.md")), $"The embedded sheet of {id} differs from the template.");
    }

    [Fact]
    public void The_tool_embeds_every_sheet_the_template_ships_and_no_other()
    {
        Directory.GetFiles(Repository.RuleSheets, "HS*.md")
            .Select(Path.GetFileNameWithoutExtension)
            .Order(StringComparer.Ordinal)
            .ShouldBe(RuleSheets.Ids);
    }

    // The shipped rules: those of the analyzers, from their release tracking, and those of the template's architecture tests, from the sheets their messages announce.
    [Fact]
    public void The_embedded_sheets_are_the_shipped_rules()
    {
        var analyzers = Path.Combine(Repository.Root, "src", "HardSlice.Analyzers");
        var analyzerRules = new[] { "AnalyzerReleases.Shipped.md", "AnalyzerReleases.Unshipped.md" }
            .SelectMany(releases => File.ReadAllLines(Path.Combine(analyzers, releases)))
            .Select(line => Regex.Match(line, @"^(?<id>HS\d{3})\s*\|"))
            .Where(match => match.Success)
            .Select(match => match.Groups["id"].Value);
        var architectureRules = Directory.GetFiles(Path.Combine(Repository.Template, "tests", "MyApp.ArchitectureTests"), "*.cs")
            .SelectMany(source => Regex.Matches(File.ReadAllText(source), @"docs/rules/(?<id>HS\d{3})\.md"))
            .Select(match => match.Groups["id"].Value);

        analyzerRules.Union(architectureRules)
            .Order(StringComparer.Ordinal)
            .ShouldBe(RuleSheets.Ids);
    }

    [Fact]
    public void A_rule_is_found_whatever_the_case_of_its_id()
    {
        RuleSheets.Find("hs016").ShouldBe(RuleSheets.Find("HS016"));
        RuleSheets.Find("HS016")!.ShouldStartWith("# HS016:");
    }

    [Fact]
    public void An_unknown_rule_has_no_sheet()
    {
        RuleSheets.Find("HS999").ShouldBeNull();
        RuleSheets.Find("HS016.md").ShouldBeNull();
    }
}
