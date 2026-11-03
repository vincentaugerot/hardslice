using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace HardSlice.Analyzers.Tests;

// The README is the entry point of a new user, docs/ holds the detail and the root guides say how to contribute: their links,
// anchors included, must lead somewhere, the README must list exactly the rules the template ships, and the links that the
// package READMEs freeze on NuGet.org must lead to the tag of their version, which never moves.
public sealed partial class ReadmeTests
{
    private static readonly string[] RootGuides = ["README.md", "CONTRIBUTING.md", "SECURITY.md", "CHANGELOG.md", "AGENTS.md"];

    [Fact]
    public void Every_relative_link_of_the_readme_exists_in_the_repository()
    {
        var repositoryRoot = FindRepositoryRoot();

        BrokenLinks(repositoryRoot, "README.md")
            .ShouldBeEmpty("README.md links to paths or anchors that the repository does not contain.");
    }

    [Fact]
    public void Every_relative_link_of_the_documentation_exists_in_the_repository()
    {
        var repositoryRoot = FindRepositoryRoot();

        Documentation(repositoryRoot)
            .SelectMany(document => BrokenLinks(repositoryRoot, document).Select(link => $"{document}: {link}"))
            .ShouldBeEmpty("The documentation links to paths or anchors that the repository does not contain.");
    }

    [Fact]
    public void Documentation_holds_the_guides_and_pages_the_readme_points_to()
    {
        var repositoryRoot = FindRepositoryRoot();

        foreach (var page in new[] { "docs/README.md", "docs/getting-started.md", "docs/item-templates.md", "docs/agent-loop.md", "docs/rules.md", "docs/design/specifications.md", "docs/design/plan.md", "docs/design/product-backlog.md" }
            .Concat(RootGuides))
            File.Exists(Path.Combine(repositoryRoot, page)).ShouldBeTrue($"{page} is missing.");

        Directory.Exists(Path.Combine(repositoryRoot, "docs", "plans")).ShouldBeFalse("docs/plans/ held internal session notes and was removed.");
    }

    [Fact]
    public void Documentation_has_no_em_dash()
    {
        var repositoryRoot = FindRepositoryRoot();

        Documentation(repositoryRoot)
            .SelectMany(document => File.ReadAllLines(Path.Combine(repositoryRoot, document))
                .Select((line, index) => (document, line, number: index + 1)))
            .Where(entry => entry.line.Contains('\u2014'))
            .Select(entry => $"{entry.document}:{entry.number}")
            .ShouldBeEmpty("The documentation must not contain an em dash.");
    }

    // PB-059: a package README is frozen on NuGet.org with its version, so it links to the tag of that version, where the path
    // and its anchor exist in this tree, never to main, which moves. The license alone stays on main.
    [Fact]
    public void Links_the_package_readmes_freeze_on_NuGet_org_lead_to_the_tag_of_their_version()
    {
        var repositoryRoot = FindRepositoryRoot();
        var tag = "v" + PackageVersion(repositoryRoot);
        var packageReadmes = PackageReadmes(repositoryRoot);

        var links = packageReadmes
            .SelectMany(readme => RepositoryLink().Matches(File.ReadAllText(readme)).Select(match => (readme, match)))
            .ToArray();
        links.ShouldNotBeEmpty();

        links
            .Where(link => !Resolves(repositoryRoot, link.match.Groups["path"].Value, link.match.Groups["anchor"].Value)
                || link.match.Groups["ref"].Value != (link.match.Groups["path"].Value == "LICENSE" ? "main" : tag))
            .Select(link => $"{Path.GetRelativePath(repositoryRoot, link.readme)}: {link.match.Value}")
            .ShouldBeEmpty($"A package README links to a path or an anchor that no longer exists, or not at the tag {tag} (the license at main).");

        packageReadmes
            .SelectMany(readme => RepositoryUrl().Matches(File.ReadAllText(readme))
                .Where(match => !RepositoryLink().IsMatch(match.Value) && match.Value.TrimEnd('/') != "https://github.com/vincentaugerot/hardslice")
                .Select(match => $"{Path.GetRelativePath(repositoryRoot, readme)}: {match.Value}"))
            .ShouldBeEmpty("A package README links into the repository other than by a file at a tag.");
    }

    // PB-059: the first command a visitor copies installs the version that the README describes.
    [Theory]
    [InlineData("README.md")]
    [InlineData("src/HardSlice.Templates/README.md")]
    public void Install_line_names_the_version_of_the_packages(string readme)
    {
        var repositoryRoot = FindRepositoryRoot();
        var version = PackageVersion(repositoryRoot);

        InstallLine().Matches(File.ReadAllText(Path.Combine(repositoryRoot, readme)))
            .Select(match => match.Groups["version"].Value)
            .ShouldBe([version], $"{readme} must hold one install line, at the version {version} of Directory.Build.props.");
    }

    // PB-059: the page of HardSlice.Analyzers on NuGet.org names every rule the package reports, HS000 included, alone or in a
    // range 'HSxxx to HSyyy', and says that HS000 escapes the severities of .editorconfig.
    [Fact]
    public void Analyzers_package_readme_names_every_rule_the_analyzers_report()
    {
        var repositoryRoot = FindRepositoryRoot();
        var readme = File.ReadAllText(Path.Combine(repositoryRoot, "src", "HardSlice.Analyzers", "README.md"));
        var reported = TrackedRules(repositoryRoot);
        var named = new HashSet<string>(RuleId().Matches(readme).Select(match => match.Value), StringComparer.Ordinal);
        foreach (Match range in RuleRange().Matches(readme))
            named.UnionWith(reported.Where(rule =>
                string.CompareOrdinal(rule, range.Groups["from"].Value) >= 0 && string.CompareOrdinal(rule, range.Groups["to"].Value) <= 0));

        reported.Where(rule => !named.Contains(rule)).ShouldBeEmpty("The README of HardSlice.Analyzers omits rules that the package reports.");
        readme.ShouldContain("except for HS000");
    }

    // PB-059: the page of HardSlice.Templates names the hooks that make the agents follow the rules, and the approval Codex needs.
    [Fact]
    public void Templates_package_readme_names_the_hooks_of_each_CLI_and_the_approval_of_Codex()
    {
        var repositoryRoot = FindRepositoryRoot();
        var readme = File.ReadAllText(Path.Combine(repositoryRoot, "src", "HardSlice.Templates", "README.md"));

        foreach (var expected in new[] { "`.claude/settings.json`", "`.codex/hooks.json`", "`.github/hooks/hardslice.json`", "`/hooks`", "`.githooks/pre-commit`", "`.github/workflows/hardslice.yml`" })
            readme.ShouldContain(expected);
    }

    // PB-059: the rules of a version are shipped in the commit that sets it, so that the release tracking reports any later
    // change of their category or default severity (docs/releasing.md).
    [Fact]
    public void Analyzer_releases_ship_the_version_of_the_packages()
    {
        var repositoryRoot = FindRepositoryRoot();
        var shipped = File.ReadAllLines(Path.Combine(repositoryRoot, "src", "HardSlice.Analyzers", "AnalyzerReleases.Shipped.md"));

        // The release tracking reads a System.Version: '0.3.0' for every preview of 0.3.0.
        var release = PackageVersion(repositoryRoot).Split('-')[0];

        shipped.ShouldContain($"## Release {release}",
            $"Move the rules of AnalyzerReleases.Unshipped.md under '## Release {release}' of AnalyzerReleases.Shipped.md in the commit that sets the version.");
    }

    [Fact]
    public void Readme_lists_every_rule_sheet_the_template_ships_and_no_other()
    {
        var repositoryRoot = FindRepositoryRoot();
        var rulesFolder = Path.Combine("src", "HardSlice.Templates", "content", "hardslice-api", "docs", "rules");
        var shippedSheets = Directory.GetFiles(Path.Combine(repositoryRoot, rulesFolder), "HS*.md")
            .Select(sheet => Path.Combine(rulesFolder, Path.GetFileName(sheet)).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

        RelativeLinks(File.ReadAllText(Path.Combine(repositoryRoot, "README.md")))
            .Select(link => link.Target)
            .Distinct()
            .Where(link => link.StartsWith(rulesFolder.Replace('\\', '/') + "/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ShouldBe(shippedSheets);
    }

    // The root guides and every Markdown page of docs/, as paths relative to the repository root.
    private static string[] Documentation(string repositoryRoot) =>
        RootGuides
            .Concat(Directory.GetFiles(Path.Combine(repositoryRoot, "docs"), "*.md", SearchOption.AllDirectories)
                .Select(file => Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/')))
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static IEnumerable<string> BrokenLinks(string repositoryRoot, string document)
    {
        var folder = Path.GetDirectoryName(document)!.Replace('\\', '/');
        var markdown = File.ReadAllText(Path.Combine(repositoryRoot, document));

        foreach (var (target, anchor) in RelativeLinks(markdown))
        {
            var path = target.Length == 0 ? document : Path.GetRelativePath(repositoryRoot, Path.GetFullPath(Path.Combine(repositoryRoot, folder, target))).Replace('\\', '/');
            if (!Resolves(repositoryRoot, path, anchor))
                yield return target + (anchor.Length > 0 ? "#" + anchor : "");
        }
    }

    // A path of the repository that exists, and, for an anchor in a Markdown file, a heading whose GitHub slug it is.
    private static bool Resolves(string repositoryRoot, string path, string anchor)
    {
        var fullPath = Path.Combine(repositoryRoot, path);
        if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
            return false;
        if (anchor.Length == 0 || !path.EndsWith(".md", StringComparison.Ordinal) || !File.Exists(fullPath))
            return true;

        return Headings(File.ReadAllText(fullPath)).Contains(anchor);
    }

    private static HashSet<string> Headings(string markdown) =>
        Heading().Matches(WithoutCode(markdown))
            .Select(match => Slug(match.Groups["title"].Value))
            .ToHashSet(StringComparer.Ordinal);

    // GitHub's slug: lowercase, without punctuation or inline code marks, spaces turned into hyphens.
    private static string Slug(string title) =>
        Punctuation().Replace(title.Trim().ToLowerInvariant(), "").Replace(' ', '-');

    // Markdown links outside code, without absolute URLs: the target, empty for an anchor of the same page, and the anchor.
    private static IEnumerable<(string Target, string Anchor)> RelativeLinks(string markdown) =>
        Link().Matches(WithoutCode(markdown))
            .Select(match => (Target: match.Groups["target"].Value, Anchor: match.Groups["anchor"].Value))
            .Where(link => !link.Target.Contains("://", StringComparison.Ordinal) && !link.Target.StartsWith("mailto:", StringComparison.Ordinal))
            .Where(link => link.Target.Length > 0 || link.Anchor.Length > 0);

    private static string WithoutCode(string markdown) => Regex.Replace(markdown, "```.*?```", "", RegexOptions.Singleline);

    [GeneratedRegex(@"\]\((?<target>[^)#\s]*)(#(?<anchor>[^)\s]*))?\)")]
    private static partial Regex Link();

    [GeneratedRegex(@"^#{1,6}\s+(?<title>.+?)\s*$", RegexOptions.Multiline)]
    private static partial Regex Heading();

    [GeneratedRegex(@"[^\p{L}\p{N}\s_-]")]
    private static partial Regex Punctuation();

    [GeneratedRegex(@"https://github\.com/vincentaugerot/hardslice/blob/(?<ref>[^/)\s]+)/(?<path>[^)#\s]+)(#(?<anchor>[^)\s]*))?")]
    private static partial Regex RepositoryLink();

    [GeneratedRegex(@"https://github\.com/vincentaugerot/hardslice[^)\s]*")]
    private static partial Regex RepositoryUrl();

    [GeneratedRegex(@"dotnet new install HardSlice\.Templates@(?<version>\S+)")]
    private static partial Regex InstallLine();

    [GeneratedRegex(@"\bHS\d{3}\b")]
    private static partial Regex RuleId();

    [GeneratedRegex(@"\b(?<from>HS\d{3}) to (?<to>HS\d{3})\b")]
    private static partial Regex RuleRange();

    private static string[] PackageReadmes(string repositoryRoot)
    {
        var readmes = Directory.GetDirectories(Path.Combine(repositoryRoot, "src"))
            .Select(project => Path.Combine(project, "README.md"))
            .Where(File.Exists)
            .ToArray();
        readmes.Length.ShouldBe(3);
        return readmes;
    }

    // The rules of the release tracking of the analyzers, shipped or not: the rules the package reports.
    private static string[] TrackedRules(string repositoryRoot) =>
        new[] { "AnalyzerReleases.Shipped.md", "AnalyzerReleases.Unshipped.md" }
            .SelectMany(file => File.ReadAllLines(Path.Combine(repositoryRoot, "src", "HardSlice.Analyzers", file)))
            .Select(line => Regex.Match(line, @"^(HS\d{3})\s*\|"))
            .Where(match => match.Success)
            .Select(match => match.Groups[1].Value)
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static string PackageVersion(string repositoryRoot) =>
        XDocument.Load(Path.Combine(repositoryRoot, "Directory.Build.props"))
            .Descendants()
            .Single(element => element.Name.LocalName == "Version")
            .Value;

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HardSlice.slnx")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not find the HardSlice repository root.");
    }
}
