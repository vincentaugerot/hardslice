namespace HardSlice.Tool.Tests;

// The files of a generated solution that check reads before running anything, in a temporary directory.
internal sealed class TemporarySolution : IDisposable
{
    private readonly string _name;

    public TemporarySolution(string name = "Demo", string? analyzerVersion = null)
    {
        _name = name;
        Root = Directory.CreateTempSubdirectory("hardslice-tool-").FullName;
        Directory.CreateDirectory(Path.Combine(Root, ".config"));
        File.WriteAllText(Path.Combine(Root, ".config", "dotnet-tools.json"), "{}");
        WritePackages($"""<GlobalPackageReference Include="HardSlice.Analyzers" Version="{analyzerVersion ?? ToolVersion.Current}" />""");
        WriteProjects([.. TestProjects.Select(project => (project, true))]);
    }

    // The test projects that the full check runs, which it requires in the .slnx.
    public IReadOnlyList<string> TestProjects =>
        [.. new[] { "Tests", "ArchitectureTests", "IntegrationTests" }.Select(suffix => $"tests/{_name}.{suffix}/{_name}.{suffix}.csproj")];

    public string Root { get; }

    public void WritePackages(string items) =>
        File.WriteAllText(Path.Combine(Root, "Directory.Packages.props"), $"<Project><ItemGroup>{items}</ItemGroup></Project>");

    // Lists the projects in the .slnx instead of those listed before, each with an assets file when restored, written after
    // the project file, and without one otherwise.
    public void WriteProjects(params (string Path, bool Restored)[] projects)
    {
        var restoredAt = DateTime.UtcNow;
        foreach (var (path, restored) in projects)
        {
            var file = Path.Combine(Root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            File.SetLastWriteTimeUtc(file, restoredAt.AddMinutes(-10));
            var assets = Path.Combine(Path.GetDirectoryName(file)!, "obj", "project.assets.json");
            if (restored)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(assets)!);
                File.WriteAllText(assets, "{}");
                File.SetLastWriteTimeUtc(assets, restoredAt);
            }
            else if (File.Exists(assets))
            {
                File.Delete(assets);
            }
        }

        var items = string.Concat(projects.Select(project => $"<Project Path=\"{project.Path}\" />"));
        File.WriteAllText(Path.Combine(Root, $"{_name}.slnx"), $"<Solution><Folder Name=\"/src/\">{items}</Folder></Solution>");
        File.SetLastWriteTimeUtc(Path.Combine(Root, "Directory.Packages.props"), restoredAt.AddMinutes(-10));
    }

    // Writes a file of the solution, relative to its root, with its folders.
    public string Write(string path, string text)
    {
        var file = Path.Combine(Root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, text);
        return file;
    }

    public void Dispose() => Directory.Delete(Root, recursive: true);
}
