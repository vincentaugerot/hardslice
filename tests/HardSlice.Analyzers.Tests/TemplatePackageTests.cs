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
