namespace HardSlice.Tool;

// The files of a generated solution that an agent never edits: they hold the rules, their severities, the versions of the
// analyzers and of the tool, the check scripts, the architecture tests, the hooks of the agents and the pre-commit hook and CI
// that run the check, and the generation that dotnet hardslice upgrade merges from. The section "Forbidden" of AGENTS.md lists the workarounds they would allow. The hooks refuse their edit,
// and the CI of the solution fails a pull request that changes one without the approval of a maintainer (dotnet hardslice protected).
internal static class ProtectedFiles
{
    // Build and analyzer settings that a copy in any folder of the solution would override for the projects below it. The SDK
    // reads every .globalconfig of a parent folder of a compiled file, above HardSlice.All.globalconfig (global_level -1), and
    // MSBuild every Directory.Build.rsp of the folder it builds and its parents, whose properties, such as RunAnalyzers=false,
    // are global: no project file can set them back.
    private static readonly string[] AnyFolderFiles =
        [".editorconfig", ".globalconfig", "Directory.Build.props", "Directory.Build.targets", "Directory.Build.rsp"];

    // Directory.HardSlice.props references HardSlice.Analyzers for every project, through Directory.Packages.props, which the
    // application owns for its own packages: check fails when the import is gone or a project no longer receives the package.
    // Directory.Solution.props and .targets are imported by the build of the .slnx, and nuget.config chooses where
    // HardSlice.Analyzers and the tool are restored from.
    private static readonly string[] RootFiles =
        ["hardslice.json", "Directory.HardSlice.props", ".config/dotnet-tools.json", ".github/workflows/hardslice.yml", "Directory.Solution.props", "Directory.Solution.targets", "nuget.config"];

    // The protected entries as AGENTS.md and the instruction files name them, for the solution of the given name.
    public static IReadOnlyList<string> Entries(string solutionName) =>
        [.. AnyFolderFiles, .. RootFiles, .. RootFolders(solutionName)];

    // The folders of the root whose every file is protected, each ending with '/', for the solution of the given name.
    public static IReadOnlyList<string> Folders(string solutionName) => RootFolders(solutionName);

    // The protected entry that the path, relative to the root of the solution with '/', falls under, or null.
    public static string? Match(string relativePath, string solutionName)
    {
        var fileName = relativePath[(relativePath.LastIndexOf('/') + 1)..];
        var file = AnyFolderFiles.FirstOrDefault(name => name.Equals(fileName, StringComparison.OrdinalIgnoreCase))
            ?? RootFiles.FirstOrDefault(name => name.Equals(relativePath, StringComparison.OrdinalIgnoreCase));
        return file ?? RootFolders(solutionName).FirstOrDefault(folder => relativePath.StartsWith(folder, StringComparison.OrdinalIgnoreCase));
    }

    // The globs of dotnet new that match the protected entries, relative to the root of the template, for the solution of the given
    // name: the generation of the template that the solution records leaves them out (UpgradeCommand.GenerationFolder).
    public static IReadOnlyList<string> Globs(string solutionName) =>
        [.. AnyFolderFiles.Select(name => $"**/{name}"), .. RootFiles, .. RootFolders(solutionName).Select(folder => $"{folder}**")];

    // .claude/, .codex/ and .github/hooks/ hold the hooks of Claude Code, Codex and Copilot CLI, .claude/ the skills too and
    // .agents/ their copy, which Codex, Gemini CLI and Copilot CLI read; .githooks/ holds the pre-commit hook. .hardslice/generation/
    // holds the files of the application as the template generated them, which dotnet hardslice upgrade merges from and replaces.
    private static string[] RootFolders(string solutionName) =>
        ["scripts/", $"tests/{solutionName}.ArchitectureTests/", ".claude/", ".agents/", ".codex/", ".github/hooks/", ".githooks/", UpgradeCommand.GenerationFolder];
}
