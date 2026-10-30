namespace HardSlice.Tool.Tests;

internal static class Repository
{
    public static string Root { get; } = FindRoot();

    public static string Template { get; } = Path.Combine(Root, "src", "HardSlice.Templates", "content", "hardslice-api");

    public static string RuleSheets { get; } = Path.Combine(Template, "docs", "rules");

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HardSlice.slnx")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not find the HardSlice repository root.");
    }
}
