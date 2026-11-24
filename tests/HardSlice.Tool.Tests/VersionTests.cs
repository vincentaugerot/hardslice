using System.Text.Json;
using System.Xml.Linq;

namespace HardSlice.Tool.Tests;

// The tool checks the rules of the analyzers it ships with: the repository packs both at one version, which the pack of the template writes.
public sealed class VersionTests
{
    private static readonly string PackageVersion = XDocument.Load(Path.Combine(Repository.Root, "Directory.Build.props"))
        .Descendants()
        .Single(element => element.Name.LocalName == "HardSliceVersion")
        .Value;

    [Fact]
    public void The_tool_has_the_version_of_the_HardSlice_packages()
    {
        ToolVersion.Current.ShouldBe(PackageVersion);
    }

    // PB-113: a local build packs <version>.dev.<n>, whose tag does not exist: its links of documentation name the tag of the
    // release that it precedes.
    [Theory]
    [InlineData("0.3.0-preview.1.dev.1795000000", "0.3.0-preview.1")]
    [InlineData("0.3.0.dev.7", "0.3.0")]
    [InlineData("0.3.0-preview.1", "0.3.0-preview.1")]
    [InlineData("0.3.0-preview.1.dev.", "0.3.0-preview.1.dev.")]
    [InlineData("0.3.0-preview.1.dev.x1", "0.3.0-preview.1.dev.x1")]
    public void A_local_build_names_the_tag_of_the_release_that_it_precedes(string version, string release)
    {
        ToolVersion.Release(version).ShouldBe(release);
        AnalyzerVersion.Doc(version).ShouldStartWith($"Doc: https://github.com/vincentaugerot/hardslice/blob/v{release}/");
    }

    // TemplatePackageTests checks that the pack writes the version of the HardSlice packages in place of the token.
    [Fact]
    public void The_template_manifest_pins_the_tool_at_the_version_of_the_HardSlice_packages_without_roll_forward()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(Repository.Template, ".config", "dotnet-tools.json")));
        var tool = manifest.RootElement.GetProperty("tools").GetProperty("hardslice.tool");

        tool.GetProperty("version").GetString().ShouldBe("HARDSLICE_VERSION");
        tool.GetProperty("rollForward").GetBoolean().ShouldBeFalse();
        tool.GetProperty("commands").EnumerateArray().Select(command => command.GetString()).ShouldBe(["dotnet-hardslice"]);
    }
}
