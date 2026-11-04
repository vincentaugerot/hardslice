using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;

namespace HardSlice.Tool;

// Whether check --build must restore before building with --no-restore: a restore costs seconds even when it has nothing to do.
// Read before the step that restores, so that the trace written after it holds the files as that restore found them.
internal sealed class RestoreState
{
    private const string AssetsFile = "obj/project.assets.json";

    // What check writes in obj/ of each project after a restore that passed: the hash of every file the restore read.
    private const string TraceFile = "obj/hardslice.restore";
    private const string TraceHeader = "hardslice-restore 1";

    // The files of the root that every restore reads, besides the project files. Directory.HardSlice.props holds the reference
    // to HardSlice.Analyzers, imported by Directory.Packages.props.
    private static readonly string[] RootFiles = ["Directory.Packages.props", "Directory.HardSlice.props", "Directory.Build.props", "Directory.Build.targets", "nuget.config"];

    private readonly Solution _solution;
    private readonly IReadOnlyList<Project>? _projects;

    private RestoreState(Solution solution, IReadOnlyList<Project>? projects)
    {
        _solution = solution;
        _projects = projects;
    }

    // A project of the .slnx and the files its restore reads, each with the hash of its content as it is now (null when unreadable).
    private sealed record Project(string Path, IReadOnlyList<(string Path, string? Hash)> Inputs);

    // The projects of the .slnx and the files their restore reads, as they are now.
    public static RestoreState Read(Solution solution)
    {
        var projects = Projects(solution);
        if (projects is null)
            return new RestoreState(solution, null);

        var rootInputs = new DirectoryInfo(solution.Root).EnumerateFiles()
            .Where(file => RootFiles.Contains(file.Name, StringComparer.OrdinalIgnoreCase))
            .OrderBy(file => file.Name, StringComparer.Ordinal)
            .Select(file => (file.Name, Hash(file.FullName)))
            .ToArray();

        return new RestoreState(solution, [.. projects.Select(project => new Project(project, [.. rootInputs, (project, Hash(Path.Combine(solution.Root, project)))]))]);
    }

    // Why a restore is needed, or null when every project of the solution was restored from the files as they are now.
    // A file is up to date for a project when its content is the one the trace of the last restore by check recorded, or when
    // it is older than the assets file, which a restore that changed a package rewrote after reading it. A restore with nothing
    // to change does not rewrite the assets file: without the trace, a project file edited without changing a package, by
    // dotnet user-secrets init for instance, would be newer than it for good, and every build would restore again.
    public string? Outdated
    {
        get
        {
            if (_projects is null)
                return $"{_solution.File} could not be read";

            foreach (var project in _projects)
            {
                var assets = new FileInfo(Path.Combine(ProjectDirectory(project), AssetsFile));
                if (!assets.Exists)
                    return $"{project.Path} has never been restored";

                var trace = ReadTrace(project);
                foreach (var (path, hash) in project.Inputs)
                {
                    if (hash is not null && trace is not null && trace.TryGetValue(path, out var recorded) && recorded == hash)
                        continue;
                    if (File.GetLastWriteTimeUtc(Path.Combine(_solution.Root, path)) > assets.LastWriteTimeUtc)
                        return $"{path} changed since the last restore of {project.Path}";
                }

                var removed = trace?.Keys.FirstOrDefault(path => !project.Inputs.Any(input => input.Path == path));
                if (removed is not null)
                    return $"{removed} was removed since the last restore of {project.Path}";
            }

            return null;
        }
    }

    // Writes the trace of every restored project after a restore that passed, with the files as they were read before it:
    // a file changed while the restore ran does not match its trace, and the next build restores again. The trace only saves
    // restores: when it cannot be written, the next build restores as it would without it.
    public void Record()
    {
        foreach (var project in _projects ?? [])
        {
            var directory = ProjectDirectory(project);
            if (!File.Exists(Path.Combine(directory, AssetsFile)) || project.Inputs.Any(input => input.Hash is null))
                continue;

            try
            {
                File.WriteAllLines(Path.Combine(directory, TraceFile), [TraceHeader, .. project.Inputs.Select(input => $"{input.Hash} {input.Path}")]);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
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

    private string ProjectDirectory(Project project) => Path.GetDirectoryName(Path.Combine(_solution.Root, project.Path))!;

    // The hashes recorded by the last restore of check, by path, or null without a readable trace.
    private Dictionary<string, string>? ReadTrace(Project project)
    {
        string[] lines;
        try
        {
            lines = File.ReadAllLines(Path.Combine(ProjectDirectory(project), TraceFile));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        if (lines.Length == 0 || lines[0] != TraceHeader)
            return null;

        var trace = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in lines.Skip(1))
        {
            var separator = line.IndexOf(' ');
            if (separator <= 0)
                return null;
            trace[line[(separator + 1)..]] = line[..separator];
        }

        return trace;
    }

    private static string? Hash(string file)
    {
        try
        {
            using var stream = File.OpenRead(file);
            return Convert.ToHexString(SHA256.HashData(stream));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
