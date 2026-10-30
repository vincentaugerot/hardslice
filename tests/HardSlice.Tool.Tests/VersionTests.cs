using System.Text.Json;
using System.Xml.Linq;

namespace HardSlice.Tool.Tests;

// The tool checks the rules of the analyzers it ships with: the repository packs both at one version, which the pack of the template writes.
public sealed class VersionTests
{
    private static readonly string PackageVersion = XDocument.Load(Path.Combine(Repository.Root, "Directory.Build.props"))
        .Descendants()
        .Single(element => element.Name.LocalName == "Version")
        .Value;

    [Fact]
    public void The_tool_has_the_version_of_the_HardSlice_packages()
    {
        ToolVersion.Current.ShouldBe(PackageVersion);
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
