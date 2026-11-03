using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.Diagnostics;

namespace HardSlice.Analyzers.Tests;

// PB-044: every HardSlice failure ends with 'Skill: <skill>. Doc: 'docs/rules/HSxxx.md'.', the skill ships with the template, and
// the sheet of the rule names the same skill. One skill per rule.
public sealed partial class RuleSkillTests
{
    private static readonly Dictionary<string, string> SkillOfRule = new(StringComparer.Ordinal)
    {
        ["HS000"] = "fix-architecture-violation",
        ["HS001"] = "fix-architecture-violation",
        ["HS003"] = "fix-architecture-violation",
        ["HS004"] = "add-slice",
        ["HS005"] = "fix-architecture-violation",
        ["HS006"] = "fix-architecture-violation",
        ["HS007"] = "extract-to-domain",
        ["HS008"] = "add-aggregate",
        ["HS009"] = "add-domain-event",
        ["HS010"] = "fix-architecture-violation",
        ["HS011"] = "fix-architecture-violation",
        ["HS012"] = "add-domain-event",
        ["HS013"] = "add-slice",
        ["HS014"] = "add-slice",
        ["HS015"] = "add-slice",
        ["HS016"] = "extract-to-domain",
        ["HS017"] = "fix-architecture-violation",
        ["HS018"] = "add-slice",
        ["HS019"] = "add-domain-event",
        ["HS900"] = "fix-architecture-violation",
        ["HS901"] = "fix-architecture-violation",
        ["HS902"] = "add-aggregate",
    };

    // The rules that the architecture tests check, and the configuration they read (HS900).
    private static readonly string[] ArchitectureTestRules = ["HS001", "HS012", "HS013", "HS014", "HS016", "HS017", "HS018", "HS900"];

    [Fact]
    public void Every_rule_of_the_template_has_one_skill_that_the_template_ships()
    {
        var templateRoot = TemplateRoot();
        Directory.EnumerateFiles(Path.Combine(templateRoot, "docs", "rules"), "HS*.md")
            .Select(file => Path.GetFileNameWithoutExtension(file))
            .Order(StringComparer.Ordinal)
            .ShouldBe(SkillOfRule.Keys.Order(StringComparer.Ordinal));

        foreach (var skill in SkillOfRule.Values.Distinct())
            File.Exists(Path.Combine(templateRoot, ".claude", "skills", skill, "SKILL.md")).ShouldBeTrue($"The template does not ship the skill {skill}.");
    }

    [Fact]
    public void Every_diagnostic_names_the_skill_of_its_rule_before_its_sheet()
    {
        var descriptors = typeof(SliceReferenceAnalyzer).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(DiagnosticAnalyzer).IsAssignableFrom(type))
            .SelectMany(type => ((DiagnosticAnalyzer)Activator.CreateInstance(type)!).SupportedDiagnostics)
            .ToArray();

        descriptors.ShouldNotBeEmpty();
        foreach (var descriptor in descriptors)
        {
            SkillOfRule.ShouldContainKey(descriptor.Id);
            descriptor.MessageFormat.ToString().ShouldEndWith(
                $" Skill: {SkillOfRule[descriptor.Id]}. Doc: 'docs/rules/{descriptor.Id}.md'.",
                Case.Sensitive,
                $"The message of {descriptor.Id} does not end with its skill and its sheet.");
            Regex.IsMatch(descriptor.MessageFormat.ToString(), @" Fix: .+\. Skill: [a-z-]+\. Doc: ")
                .ShouldBeTrue($"The message of {descriptor.Id} does not name its skill between Fix: and Doc:.");
        }
    }

    // The IDE opens the sheet of a rule from its id: the sheet of the template, on GitHub, at the tag of this version.
    [Fact]
    public void Every_descriptor_links_to_the_sheet_of_its_rule_at_the_tag_of_its_version()
    {
        var version = typeof(SliceReferenceAnalyzer).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .Cast<System.Reflection.AssemblyInformationalVersionAttribute>()
            .Single().InformationalVersion.Split('+')[0];
        var descriptors = typeof(SliceReferenceAnalyzer).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(DiagnosticAnalyzer).IsAssignableFrom(type))
            .SelectMany(type => ((DiagnosticAnalyzer)Activator.CreateInstance(type)!).SupportedDiagnostics)
            .ToArray();

        descriptors.ShouldNotBeEmpty();
        foreach (var descriptor in descriptors)
        {
            descriptor.HelpLinkUri.ShouldBe(
                $"https://github.com/vincentaugerot/hardslice/blob/v{version}/src/HardSlice.Templates/content/hardslice-api/docs/rules/{descriptor.Id}.md");
            File.Exists(Path.Combine(TemplateRoot(), "docs", "rules", $"{descriptor.Id}.md")).ShouldBeTrue($"The sheet of {descriptor.Id} does not exist.");
        }
    }

    // Each message of the architecture tests is a string of their sources, comments aside: every 'Doc:' follows the skill of its rule.
    [Fact]
    public void Every_architecture_test_message_names_the_skill_of_its_rule_before_its_sheet()
    {
        var sources = Directory.EnumerateFiles(Path.Combine(TemplateRoot(), "tests", "MyApp.ArchitectureTests"), "*.cs")
            .ToDictionary(file => Path.GetFileName(file), file => string.Join('\n', File.ReadLines(file).Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal))));
        var rules = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (file, text) in sources)
        {
            var footers = Footer().Matches(text);
            foreach (Match footer in footers)
            {
                var rule = footer.Groups["rule"].Value;
                SkillOfRule.ShouldContainKey(rule);
                footer.Groups["skill"].Value.ShouldBe(SkillOfRule[rule], $"{file} names the wrong skill for {rule}.");
                rules.Add(rule);
            }

            // A 'Doc:' outside a footer, such as a sheet path built at run time, would escape the check above.
            var inFooters = footers.Select(footer => footer.Groups["doc"].Index).ToHashSet();
            Regex.Matches(text, "Doc: ").Where(doc => !inFooters.Contains(doc.Index))
                .Select(doc => text.Substring(doc.Index, Math.Min(60, text.Length - doc.Index)))
                .ShouldBeEmpty($"{file} holds a 'Doc:' without the skill and the sheet of a rule.");
        }

        rules.Order(StringComparer.Ordinal).ShouldBe(ArchitectureTestRules);
    }

    [Fact]
    public void Every_sheet_names_the_skill_of_its_rule_in_its_Skill_section()
    {
        var templateRoot = TemplateRoot();
        foreach (var (rule, skill) in SkillOfRule)
        {
            var sheet = File.ReadAllText(Path.Combine(templateRoot, "docs", "rules", $"{rule}.md"));
            var section = Regex.Match(sheet, @"\n## Skill\n(?<content>.*?)(?=\n## |\z)", RegexOptions.Singleline);
            section.Success.ShouldBeTrue($"The sheet of {rule} has no Skill section.");
            Regex.Matches(section.Groups["content"].Value, @"`(?<skill>[a-z]+(?:-[a-z]+)+)`")
                .Select(match => match.Groups["skill"].Value)
                .Where(SkillOfRule.ContainsValue)
                .Distinct()
                .ShouldBe([skill], $"The Skill section of {rule} does not name {skill} alone.");

            // Every diagnostic that the sheet shows in full, not cut with '...', ends as the analyzer or the architecture test writes it.
            foreach (Match example in Regex.Matches(sheet, $@"^{rule}: .*(?<!\.\.\.)$", RegexOptions.Multiline))
                example.Value.ShouldEndWith($" Skill: {skill}. Doc: 'docs/rules/{rule}.md'.", Case.Sensitive, $"The sheet of {rule} shows a diagnostic without its skill.");
        }
    }

    [GeneratedRegex(@"Skill: (?<skill>[a-z-]+)\. (?<doc>Doc: )'docs/rules/(?<rule>HS\d{3})\.md'\.")]
    private static partial Regex Footer();

    private static string TemplateRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HardSlice.slnx")))
                return Path.Combine(directory.FullName, "src", "HardSlice.Templates", "content", "hardslice-api");
        }

        throw new DirectoryNotFoundException("Could not find the HardSlice repository root.");
    }
}
