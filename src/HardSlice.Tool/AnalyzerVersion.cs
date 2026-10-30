using System.Xml;
using System.Xml.Linq;

namespace HardSlice.Tool;

// The tool verifies the rules of the analyzers it ships with: both must have the same version.
internal static class AnalyzerVersion
{
    public const string PackageId = "HardSlice.Analyzers";
    public const string PackagesFile = "Directory.Packages.props";

    // Writes why the solution cannot be verified by this version of the tool, or returns true.
    public static bool Matches(Solution solution, string toolVersion, TextWriter error)
    {
        var path = Path.Combine(solution.Root, PackagesFile);
        if (!File.Exists(path))
        {
            error.WriteLine($"error: {PackagesFile} was not found in '{solution.Root}': it must reference {PackageId} {toolVersion}, the version of dotnet hardslice.");
            return false;
        }

        XDocument packages;
        try
        {
            packages = XDocument.Load(path);
        }
        catch (XmlException exception)
        {
            error.WriteLine($"error: {PackagesFile} is not valid XML: {exception.Message}");
            return false;
        }

        var versions = packages.Descendants()
            .Where(element => element.Name.LocalName == "GlobalPackageReference"
                && string.Equals((string?)element.Attribute("Include"), PackageId, StringComparison.OrdinalIgnoreCase))
            .Select(element => (string?)element.Attribute("Version"))
            .ToArray();

        if (versions.Length != 1 || string.IsNullOrEmpty(versions[0]))
        {
            error.WriteLine($"error: {PackagesFile} must hold one GlobalPackageReference to {PackageId} with its version, {toolVersion} for this dotnet hardslice. Restore it: the HardSlice rules come from this package.");
            return false;
        }

        if (!string.Equals(versions[0], toolVersion, StringComparison.OrdinalIgnoreCase))
        {
            error.WriteLine(
                $"error: {PackagesFile} references {PackageId} {versions[0]}, but dotnet hardslice is version {toolVersion}. "
                + $"The tool and the analyzers must have the same version: set the 'hardslice.tool' entry of {Solution.ToolManifest} "
                + $"and the {PackageId} reference of {PackagesFile} to the same version, then run 'dotnet tool restore'.");
            return false;
        }

        return true;
    }
}
