namespace HardSlice.Tool.Tests;

// The files of a generated solution that check reads before running anything, in a temporary directory.
internal sealed class TemporarySolution : IDisposable
{
    public TemporarySolution(string name = "Demo", string? analyzerVersion = null)
    {
        Root = Directory.CreateTempSubdirectory("hardslice-tool-").FullName;
        Directory.CreateDirectory(Path.Combine(Root, ".config"));
        File.WriteAllText(Path.Combine(Root, ".config", "dotnet-tools.json"), "{}");
        File.WriteAllText(Path.Combine(Root, $"{name}.slnx"), "<Solution />");
        WritePackages($"""<GlobalPackageReference Include="HardSlice.Analyzers" Version="{analyzerVersion ?? ToolVersion.Current}" />""");
    }

    public string Root { get; }

    public void WritePackages(string items) =>
        File.WriteAllText(Path.Combine(Root, "Directory.Packages.props"), $"<Project><ItemGroup>{items}</ItemGroup></Project>");

    public void Dispose() => Directory.Delete(Root, recursive: true);
}
