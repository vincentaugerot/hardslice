using System.Text.RegularExpressions;

namespace HardSlice.Analyzers.Tests;

// The README is the entry point of a new user, docs/ holds the detail and the root guides say how to contribute: their links,
// anchors included, must lead somewhere, the README must list exactly the rules the template ships, and the anchors that the
// package READMEs freeze on NuGet.org must keep existing.
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

    [Fact]
    public void Links_the_package_readmes_freeze_on_NuGet_org_lead_to_the_repository()
    {
        var repositoryRoot = FindRepositoryRoot();
        var packageReadmes = Directory.GetDirectories(Path.Combine(repositoryRoot, "src"))
            .Select(project => Path.Combine(project, "README.md"))
            .Where(File.Exists)
            .ToArray();
        packageReadmes.Length.ShouldBe(3);

        var links = packageReadmes
            .SelectMany(readme => RepositoryLink().Matches(File.ReadAllText(readme)).Select(match => (readme, match)))
            .ToArray();
        links.ShouldNotBeEmpty();

        links
            .Where(link => !Resolves(repositoryRoot, link.match.Groups["path"].Value, link.match.Groups["anchor"].Value))
            .Select(link => $"{Path.GetRelativePath(repositoryRoot, link.readme)}: {link.match.Value}")
            .ShouldBeEmpty("A package README links to a path or an anchor of the repository that no longer exists.");
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

    [GeneratedRegex(@"https://github\.com/vincentaugerot/hardslice/blob/main/(?<path>[^)#\s]+)(#(?<anchor>[^)\s]*))?")]
    private static partial Regex RepositoryLink();

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
