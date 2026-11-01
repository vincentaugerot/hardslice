namespace HardSlice.Tool;

// The path of a file named by a hook payload, relative to the root of the solution. A payload may come from Windows
// ('C:\Users\dev\Demo\scripts\check.ps1') whatever the system the tool runs on: both separators are read, '.' and '..' are
// resolved, and paths compare without case, as on the file systems of Windows and macOS.
internal static class SolutionPath
{
    // The path relative to the root with '/', or null when the file is outside the root. A relative path is read from baseDirectory.
    public static string? Relative(string root, string path, string baseDirectory)
    {
        var file = Normalize(IsRooted(path) ? path : Combine(baseDirectory, path));
        var directory = Normalize(root)?.TrimEnd('/');
        if (file is null || string.IsNullOrEmpty(directory))
            return null;

        if (!file.StartsWith(directory + "/", StringComparison.OrdinalIgnoreCase))
            return null;

        var relative = file[(directory.Length + 1)..];
        return relative.Length == 0 ? null : relative;
    }

    private static string Combine(string directory, string path) => directory.TrimEnd('/', '\\') + "/" + path;

    private static bool IsRooted(string path) =>
        path.StartsWith('/') || path.StartsWith('\\') || (path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] is '/' or '\\');

    // Separators as '/', no '.' segment, each '..' removing the segment before it. Null for a path that climbs above its root.
    private static string? Normalize(string path)
    {
        var segments = new List<string>();
        var parts = path.Replace('\\', '/').Split('/');
        foreach (var part in parts.Skip(1))
        {
            if (part is "" or ".")
                continue;
            if (part == "..")
            {
                if (segments.Count == 0)
                    return null;
                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            segments.Add(part);
        }

        // The first part is empty for '/tmp/Demo' and the drive for 'C:/Users/dev/Demo'.
        return parts[0] + "/" + string.Join('/', segments);
    }
}
