using System.Text.Json;
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
        var templatePropertiesPath = Path.Combine(TemplateRoot(repositoryRoot), "Directory.Packages.props");
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

    // BookingsReferenceTests compiles the template against the repository's versions: they must not drift apart.
    [Fact]
    public void Packages_declared_by_both_the_repository_and_the_template_share_the_same_version()
    {
        var repositoryRoot = FindRepositoryRoot();
        var repositoryVersions = PackageVersions(Path.Combine(repositoryRoot, "Directory.Packages.props"));
        var templateVersions = PackageVersions(Path.Combine(TemplateRoot(repositoryRoot), "Directory.Packages.props"));

        repositoryVersions.Keys.Intersect(templateVersions.Keys).ShouldNotBeEmpty();
        repositoryVersions
            .Where(package => templateVersions.TryGetValue(package.Key, out var templateVersion) && templateVersion != package.Value)
            .Select(package => $"{package.Key}: repository {package.Value}, template {templateVersions[package.Key]}")
            .ShouldBeEmpty();
    }

    // dotnet-ef must match the EF Core design-time and runtime packages it drives, or migrations may not load.
    [Fact]
    public void Template_dotnet_ef_tool_matches_its_EF_Core_packages()
    {
        var templateRoot = TemplateRoot(FindRepositoryRoot());
        var templateVersions = PackageVersions(Path.Combine(templateRoot, "Directory.Packages.props"));
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(templateRoot, ".config", "dotnet-tools.json")));
        var toolVersion = manifest.RootElement.GetProperty("tools").GetProperty("dotnet-ef").GetProperty("version").GetString();

        templateVersions["Microsoft.EntityFrameworkCore.Design"].ShouldBe(toolVersion);
        templateVersions["Microsoft.EntityFrameworkCore.Relational"].ShouldBe(toolVersion);
    }

    [Fact]
    public void Agent_guidance_is_short_and_every_path_it_cites_exists_in_the_template()
    {
        var templateRoot = TemplateRoot(FindRepositoryRoot());
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

    private static string TemplateRoot(string repositoryRoot) =>
        Path.Combine(repositoryRoot, "src", "HardSlice.Templates", "content", "hardslice-api");

    private static Dictionary<string, string> PackageVersions(string packagesPropsPath) =>
        XDocument.Load(packagesPropsPath)
            .Descendants()
            .Where(element => element.Name.LocalName == "PackageVersion")
            .ToDictionary(element => element.Attribute("Include")!.Value, element => element.Attribute("Version")!.Value);

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
