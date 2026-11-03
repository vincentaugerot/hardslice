using System.Xml;
using System.Xml.Linq;

namespace HardSlice.Tool;

// Whether check --build must restore before building with --no-restore: a restore costs seconds even when it has nothing to do.
internal static class RestoreState
{
    private const string AssetsFile = "obj/project.assets.json";

    // The files of the root that every restore reads, besides the project files.
    private static readonly string[] RootFiles = ["Directory.Packages.props", "Directory.Build.props", "Directory.Build.targets", "nuget.config"];

    // Why a restore is needed, or null when every project of the solution has an assets file newer than what it was restored from.
    // A restore with nothing to change does not rewrite the assets files: after a change to a root file that changes no package,
    // every build restores again until the next restore that changes them. It costs time, never a wrong build.
    public static string? Outdated(Solution solution)
    {
        var projects = Projects(solution);
        if (projects is null)
            return $"{solution.File} could not be read";

        var rootFiles = new DirectoryInfo(solution.Root).EnumerateFiles()
            .Where(file => RootFiles.Contains(file.Name, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        foreach (var project in projects)
        {
            var projectFile = new FileInfo(Path.Combine(solution.Root, project));
            var assets = new FileInfo(Path.Combine(projectFile.DirectoryName!, AssetsFile));
            if (!assets.Exists)
                return $"{project} has never been restored";

            var newer = rootFiles.Append(projectFile).FirstOrDefault(file => file.Exists && file.LastWriteTimeUtc > assets.LastWriteTimeUtc);
            if (newer is not null)
                return $"{Path.GetRelativePath(solution.Root, newer.FullName).Replace('\\', '/')} changed since the last restore of {project}";
        }

        return null;
    }

    // The project paths of the .slnx, relative to its root, or null when it is not valid XML.
    public static IReadOnlyList<string>? Projects(Solution solution)
    {
        try
        {
            return XDocument.Load(Path.Combine(solution.Root, solution.File)).Descendants()
                .Where(element => element.Name.LocalName == "Project")
                .Select(element => (string?)element.Attribute("Path"))
                .OfType<string>()
                .Select(path => path.Replace('\\', '/'))
                .ToArray();
        }
        catch (XmlException)
        {
            return null;
        }
    }
}
