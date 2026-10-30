using System.Text.RegularExpressions;

namespace HardSlice.Analyzers.Tests;

// The repository README is the entry point of a new user: its links and its list of rules must follow the template.
public sealed class ReadmeTests
{
    [Fact]
    public void Every_relative_link_of_the_readme_exists_in_the_repository()
    {
        var repositoryRoot = FindRepositoryRoot();

        RelativeLinks(Readme(repositoryRoot))
            .Where(link => !File.Exists(Path.Combine(repositoryRoot, link)) && !Directory.Exists(Path.Combine(repositoryRoot, link)))
            .ShouldBeEmpty("README.md links to paths that the repository does not contain.");
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

        RelativeLinks(Readme(repositoryRoot))
            .Where(link => link.StartsWith(rulesFolder.Replace('\\', '/') + "/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ShouldBe(shippedSheets);
    }

    private static string Readme(string repositoryRoot) => File.ReadAllText(Path.Combine(repositoryRoot, "README.md"));

    // Markdown links outside code, without anchors-only links or absolute URLs.
    private static string[] RelativeLinks(string markdown) =>
        Regex.Matches(Regex.Replace(markdown, "```.*?```", "", RegexOptions.Singleline), @"\]\((?<target>[^)#\s]+)(#[^)]*)?\)")
            .Select(match => match.Groups["target"].Value)
            .Where(target => !target.Contains("://", StringComparison.Ordinal))
            .Distinct()
            .ToArray();

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
