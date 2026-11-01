namespace HardSlice.Tool;

// The files of a generated solution that an agent never edits: they hold the rules, their severities, the versions of the
// analyzers and of the tool, the check scripts, the architecture tests and the hooks themselves. The section "Forbidden"
// of AGENTS.md lists the workarounds they would allow.
internal static class ProtectedFiles
{
    // Build and analyzer settings that a copy in any folder of the solution would override for the projects below it.
    private static readonly string[] AnyFolderFiles = [".editorconfig", "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props"];

    private static readonly string[] RootFiles = ["hardslice.json", ".config/dotnet-tools.json"];

    // The protected entries as AGENTS.md and the instruction files name them, for the solution of the given name.
    public static IReadOnlyList<string> Entries(string solutionName) =>
        [.. AnyFolderFiles, .. RootFiles, .. RootFolders(solutionName)];

    // The protected entry that the path, relative to the root of the solution with '/', falls under, or null.
    public static string? Match(string relativePath, string solutionName)
    {
        var fileName = relativePath[(relativePath.LastIndexOf('/') + 1)..];
        var file = AnyFolderFiles.FirstOrDefault(name => name.Equals(fileName, StringComparison.OrdinalIgnoreCase))
            ?? RootFiles.FirstOrDefault(name => name.Equals(relativePath, StringComparison.OrdinalIgnoreCase));
        return file ?? RootFolders(solutionName).FirstOrDefault(folder => relativePath.StartsWith(folder, StringComparison.OrdinalIgnoreCase));
    }

    private static string[] RootFolders(string solutionName) => ["scripts/", $"tests/{solutionName}.ArchitectureTests/", ".claude/"];
}
