namespace HardSlice.Tool;

// dotnet hardslice protected: the protected files among the paths read on the standard input, the list of the hooks
// (ProtectedFiles). The CI of the solution runs it on the files that a pull request changes, and fails it on a protected one
// unless a maintainer approved it: the same protection as the hook before an edit, for every CLI and for humans.
internal static class ProtectedCommand
{
    // A path read is protected. Any other failure, such as no solution, exits with Cli.UsageError, so that the CI never reads it
    // as a protected file that a maintainer could approve.
    public const int Found = 1;

    // Paths relative to the root of the solution, as git diff --name-only prints them from the root of the repository:
    // one per line, or separated by NUL characters with -z, which keeps the name of a file as is. Splitting on both can
    // cut a name that holds a line break, which only adds paths: a protected file is never missed.
    public static int Run(string currentDirectory, TextReader input, TextWriter output, TextWriter error)
    {
        var solution = Solution.Find(currentDirectory, error);
        if (solution is null)
            return Cli.UsageError;

        var paths = input.ReadToEnd()
            .Split(['\0', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalize)
            .Where(path => path.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var found = 0;
        foreach (var path in paths)
        {
            if (ProtectedFiles.Match(path, solution.Name) is not { } entry)
                continue;

            output.WriteLine($"HardSlice: '{path}' is protected ({entry}).");
            found++;
        }

        if (found > 0)
            return Found;

        output.WriteLine($"HardSlice: none of the {paths.Length} paths read is protected.");
        return 0;
    }

    private static string Normalize(string path)
    {
        var normalized = path.Replace('\\', '/');
        while (normalized.StartsWith("./", StringComparison.Ordinal))
            normalized = normalized[2..];
        return normalized.TrimStart('/');
    }
}
