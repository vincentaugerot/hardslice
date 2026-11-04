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
        WriteReference($"""<GlobalPackageReference Include="HardSlice.Analyzers" Version="{analyzerVersion ?? ToolVersion.Current}" />""");
        WritePackages(ImportOfTheReference);
        WriteProjects([.. TestProjects.Select(project => (project, true))]);
    }

    // The import of the protected file that references HardSlice.Analyzers, as the template writes it in Directory.Packages.props.
    public const string ImportOfTheReference = """<Import Project="$(MSBuildThisFileDirectory)Directory.HardSlice.props" />""";

    // The test projects that the full check runs, which it requires in the .slnx.
    public IReadOnlyList<string> TestProjects =>
        [.. new[] { "Tests", "ArchitectureTests", "IntegrationTests" }.Select(suffix => $"tests/{_name}.{suffix}/{_name}.{suffix}.csproj")];

    public string Root { get; }

    // Directory.HardSlice.props, the protected file that holds the reference to HardSlice.Analyzers.
    public void WriteReference(string items) =>
        File.WriteAllText(Path.Combine(Root, "Directory.HardSlice.props"), $"<Project><ItemGroup>{items}</ItemGroup></Project>");

    // Directory.Packages.props, which the application owns: its elements, the import of Directory.HardSlice.props among them.
    public void WritePackages(string elements) =>
        File.WriteAllText(Path.Combine(Root, "Directory.Packages.props"), $"<Project>{elements}<ItemGroup><PackageVersion Include=\"CsvHelper\" Version=\"33.1.0\" /></ItemGroup></Project>");

    // The assets file of a project after a restore that brought HardSlice.Analyzers at the version of the tool, as NuGet writes
    // it for a GlobalPackageReference. The arguments change what the restore brought.
    public static string Assets(string? version = null, string include = "\"include\": \"Runtime, Build, Native, ContentFiles, Analyzers\",", bool referenced = true) =>
        $$"""
        {
          "version": 4,
          "libraries": { "HardSlice.Analyzers/{{version ?? ToolVersion.Current}}": { "type": "package" }, "CsvHelper/33.1.0": { "type": "package" } },
          "project": {
            "frameworks": {
              "net10.0": {
                "dependencies": {
                  {{(referenced ? $$"""
                  "HardSlice.Analyzers": { {{include}} "suppressParent": "All", "target": "Package", "version": "[{{version ?? ToolVersion.Current}}, )" },
                  """ : "")}}
                  "CsvHelper": { "target": "Package", "version": "[33.1.0, )" }
                }
              }
            }
          }
        }
        """;

    // Writes the assets file of a project of the solution, newer than the files its restore read.
    public void WriteAssets(string project, string assets)
    {
        var file = Path.Combine(Root, Path.GetDirectoryName(project)!, "obj", "project.assets.json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, assets);
    }

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
                WriteAssets(path, Assets());
                File.SetLastWriteTimeUtc(assets, restoredAt);
            }
            else if (File.Exists(assets))
            {
                File.Delete(assets);
            }
        }

        var items = string.Concat(projects.Select(project => $"<Project Path=\"{project.Path}\" />"));
        File.WriteAllText(Path.Combine(Root, $"{_name}.slnx"), $"<Solution><Folder Name=\"/src/\">{items}</Folder></Solution>");
        foreach (var rootFile in new[] { "Directory.Packages.props", "Directory.HardSlice.props" })
            File.SetLastWriteTimeUtc(Path.Combine(Root, rootFile), restoredAt.AddMinutes(-10));
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
