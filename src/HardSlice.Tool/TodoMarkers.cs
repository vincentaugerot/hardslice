namespace HardSlice.Tool;

// The TODO(hardslice): markers the element templates leave in the code and the tests they generate.
internal static class TodoMarkers
{
    public const string Marker = "TODO(hardslice):";

    public static IReadOnlyList<string> Folders { get; } = ["src", "tests"];

    // Build output holds copies of the sources and assemblies that embed the markers of the tests: it is never read.
    private static readonly HashSet<string> SkippedFolders = new(StringComparer.OrdinalIgnoreCase) { "bin", "obj", "TestResults" };

    public sealed record Location(string Path, int Line, string Text);

    // Every marker of the text files under src/ and tests/, by path then line. Paths are relative to the root, with '/'.
    public static IReadOnlyList<Location> Find(string root)
    {
        var locations = new List<Location>();
        foreach (var folder in Folders)
        {
            var directory = new DirectoryInfo(Path.Combine(root, folder));
            if (directory.Exists)
                Collect(root, directory, locations);
        }

        return locations
            .OrderBy(location => location.Path, StringComparer.Ordinal)
            .ThenBy(location => location.Line)
            .ToArray();
    }

    private static void Collect(string root, DirectoryInfo directory, List<Location> locations)
    {
        foreach (var file in directory.EnumerateFiles())
        {
            var text = File.ReadAllText(file.FullName);
            // A binary file is not code: a NUL character never appears in a source file.
            if (!text.Contains(Marker, StringComparison.Ordinal) || text.Contains('\0', StringComparison.Ordinal))
                continue;

            var path = Path.GetRelativePath(root, file.FullName).Replace('\\', '/');
            var lines = text.Split('\n');
            for (var index = 0; index < lines.Length; index++)
            {
                if (lines[index].Contains(Marker, StringComparison.Ordinal))
                    locations.Add(new Location(path, index + 1, lines[index].Trim()));
            }
        }

        foreach (var child in directory.EnumerateDirectories())
        {
            if (!SkippedFolders.Contains(child.Name) && !child.Name.StartsWith('.'))
                Collect(root, child, locations);
        }
    }
}
