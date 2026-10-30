using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace HardSlice.Tool.Tests;

// The tool checks the rules of the analyzers it ships with: the repository packs both at one version, which the template pins.
public sealed class VersionTests
{
    private static readonly string PackageVersion = XDocument.Load(Path.Combine(Repository.Root, "Directory.Build.props"))
        .Descendants()
        .Single(element => element.Name.LocalName == "VersionPrefix")
        .Value;

    [Fact]
    public void The_tool_has_the_version_of_the_HardSlice_packages()
    {
        ToolVersion.Current.ShouldBe(PackageVersion);
    }

    [Fact]
    public void The_template_manifest_pins_the_tool_at_the_version_of_the_analyzers_without_roll_forward()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(Repository.Template, ".config", "dotnet-tools.json")));
        var tool = manifest.RootElement.GetProperty("tools").GetProperty("hardslice.tool");

        tool.GetProperty("version").GetString().ShouldBe(PackageVersion);
        tool.GetProperty("version").GetString().ShouldBe(TemplateAnalyzerVersion());
        tool.GetProperty("rollForward").GetBoolean().ShouldBeFalse();
        tool.GetProperty("commands").EnumerateArray().Select(command => command.GetString()).ShouldBe(["dotnet-hardslice"]);
    }

    // The template keeps the reference in a comment that dotnet new uncomments: the repository builds the template content without the package.
    private static string TemplateAnalyzerVersion()
    {
        var packages = File.ReadAllText(Path.Combine(Repository.Template, "Directory.Packages.props"));
        var reference = Regex.Match(packages, """<GlobalPackageReference\s+Include="HardSlice\.Analyzers"\s+Version="(?<version>[^"]+)"\s*/>""");
        reference.Success.ShouldBeTrue("The template no longer references HardSlice.Analyzers.");
        return reference.Groups["version"].Value;
    }
}
