using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace HardSlice.Analyzers.Tests;

public sealed class TemplatePackageTests
{
    [Fact]
    public void Template_analyzer_version_matches_distributable_package_version()
    {
        var repositoryRoot = FindRepositoryRoot();
        var buildProperties = XDocument.Load(Path.Combine(repositoryRoot, "Directory.Build.props"));
        var packageVersion = buildProperties
            .Descendants()
            .Single(element => element.Name.LocalName == "VersionPrefix")
            .Value;
        var templatePropertiesPath = Path.Combine(
            repositoryRoot,
            "src",
            "HardSlice.Templates",
            "content",
            "hardslice-api",
            "Directory.Packages.props");
        var templateProperties = File.ReadAllText(templatePropertiesPath);
        var analyzerReference = Regex.Match(
            templateProperties,
            """<GlobalPackageReference\s+Include="HardSlice\.Analyzers"\s+Version="(?<version>[^"]+)"\s*/>""");

        analyzerReference.Success.ShouldBeTrue(
            $"Could not find the HardSlice.Analyzers global package reference in {templatePropertiesPath}.");
        analyzerReference.Groups["version"].Value.ShouldBe(
            packageVersion,
            "The template must reference the same HardSlice.Analyzers version that the repository packs.");
    }

    [Fact]
    public void Agent_guidance_is_short_and_every_path_it_cites_exists_in_the_template()
    {
        var templateRoot = Path.Combine(FindRepositoryRoot(), "src", "HardSlice.Templates", "content", "hardslice-api");
        var agentsPath = Path.Combine(templateRoot, "AGENTS.md");
        var agents = File.ReadAllText(agentsPath);

        File.ReadAllLines(agentsPath).Length.ShouldBeLessThan(150, "AGENTS.md must stay short enough for an agent to keep in context.");

        // Placeholder paths such as 'src/MyApp/Features/<Module>/' are conventions, not files.
        var citedPaths = Regex.Matches(agents, @"`(?<path>(?:src|tests|docs|scripts)/[^`<>\s]*|[\w.]+\.props)`")
            .Select(match => match.Groups["path"].Value)
            .Distinct()
            .ToArray();

        citedPaths.ShouldContain("scripts/check.sh");
        citedPaths.ShouldContain("scripts/check.ps1");
        citedPaths.ShouldContain("docs/rules/HS016.md");
        citedPaths
            .Where(path => !File.Exists(Path.Combine(templateRoot, path)) && !Directory.Exists(Path.Combine(templateRoot, path)))
            .ShouldBeEmpty("AGENTS.md cites paths that the generated solution does not contain.");
    }

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
