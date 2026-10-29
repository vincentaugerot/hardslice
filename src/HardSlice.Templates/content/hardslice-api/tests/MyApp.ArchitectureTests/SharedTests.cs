namespace MyApp.ArchitectureTests;

// HS017: Shared stays cross-cutting. It never depends on Features and holds only the folders hardslice.json allows.
public sealed class SharedTests
{
    private const string Doc = "Doc: 'docs/rules/HS017.md'.";

    [Fact]
    public void Shared_never_depends_on_features()
    {
        Violations.ShouldBeNone(
            from type in Application.TypesOf(Application.WebAssembly)
            let source = Location.Of(type)
            where source.InShared
            from used in Application.TypesUsedBy(type)
            where Application.IsDeclaredIn(used, Application.WebAssembly)
            let target = Location.Of(used)
            where target.InFeatures
            select $"HS017: '{Application.DisplayName(type)}' {source.Describe()} uses '{Application.DisplayName(used)}' {target.Describe()}. "
                + "Shared is cross-cutting and never depends on Features. "
                + $"Fix: move the code that needs '{Application.DisplayName(used)}' into {(target.Module is { } module ? $"module '{module}'" : "Features")}, "
                + $"or declare in Shared an abstraction that the module implements and registers. {Doc}");
    }

    // Both the namespaces of the compiled types and the C# files on disk: a namespace may not match its folder.
    [Fact]
    public void Shared_holds_only_the_folders_allowed_by_hardslice_json()
    {
        var allowed = HardSliceConfiguration.Current.SharedFolders;
        var sharedPath = $"src/{Application.WebName}/Shared";
        var sharedRoot = Path.Combine(Application.SolutionRoot, sharedPath);

        var types =
            from type in Application.TypesOf(Application.WebAssembly)
            let location = Location.Of(type)
            where location.InShared && !allowed.Contains(location.SharedFolder)
            select Message($"'{Application.DisplayName(type)}' is {location.Describe()}", location.SharedFolder);

        var files =
            from file in Directory.Exists(sharedRoot) ? Directory.EnumerateFiles(sharedRoot, "*.cs", SearchOption.AllDirectories) : []
            let relative = Path.GetRelativePath(sharedRoot, file).Replace('\\', '/')
            let folder = relative.Contains('/') ? relative[..relative.IndexOf('/')] : null
            where !allowed.Contains(folder)
            select Message($"'{sharedPath}/{relative}' is in {(folder is null ? "'Shared'" : $"'Shared/{folder}'")}", folder);

        Violations.ShouldBeNone(types.Concat(files));

        string Message(string offender, string? folder) =>
            $"HS017: {offender}, {(folder is null ? "outside any folder" : "a folder hardslice.json does not allow")}. "
            + $"Shared holds only cross-cutting code, in the folders allowed by hardslice.json: {string.Join(", ", allowed)}. "
            + "Fix: move a use case into its slice and module-specific code into its module under 'Features'; "
            + $"keep in Shared only cross-cutting code, in an allowed folder. {Doc}";
    }
}
