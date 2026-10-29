using System.Text.Json;
using System.Text.RegularExpressions;

namespace MyApp.ArchitectureTests;

// hardslice.json at the solution root: the architecture style and the folders Shared may contain (HS017).
// A missing or invalid file fails the architecture tests instead of silently falling back to defaults.
internal sealed partial record HardSliceConfiguration(string Style, IReadOnlyList<string> SharedFolders)
{
    public const string FileName = "hardslice.json";

    private const string Fix = "Fix: restore hardslice.json from version control; changing it requires a maintainer decision. Doc: 'docs/rules/HS017.md'.";

    private static readonly Lazy<HardSliceConfiguration> Solution =
        new(() => Load(Path.Combine(Application.SolutionRoot, FileName)));

    public static HardSliceConfiguration Current => Solution.Value;

    public static HardSliceConfiguration Load(string path)
    {
        if (!File.Exists(path))
            throw Invalid($"no file at '{path}'. The architecture tests read the HardSlice configuration from the solution root.");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        }
        catch (JsonException exception)
        {
            throw Invalid($"not valid JSON: {exception.Message}");
        }

        using (document)
        {
            var root = document.RootElement;
            ExpectObject(root, "the root", "style", "shared");

            if (!root.TryGetProperty("style", out var style) || style.ValueKind != JsonValueKind.String || style.GetString() != "slices")
                throw Invalid("'style' must be \"slices\", the only architecture style these tests check.");

            if (!root.TryGetProperty("shared", out var shared))
                throw Invalid("'shared' is missing: it lists the folders Shared may contain.");
            ExpectObject(shared, "'shared'", "allowedFolders");

            if (!shared.TryGetProperty("allowedFolders", out var folders) || folders.ValueKind != JsonValueKind.Array || folders.GetArrayLength() == 0)
                throw Invalid("'shared.allowedFolders' must be a non-empty array of folder names, such as [\"Messaging\", \"Persistence\", \"Behaviors\"].");

            var names = new List<string>();
            foreach (var folder in folders.EnumerateArray())
            {
                if (folder.ValueKind != JsonValueKind.String || !FolderName().IsMatch(folder.GetString()!))
                    throw Invalid($"'shared.allowedFolders' contains {folder.GetRawText()}, which is not a single folder name of Shared.");
                if (names.Contains(folder.GetString()!))
                    throw Invalid($"'shared.allowedFolders' lists \"{folder.GetString()}\" twice.");
                names.Add(folder.GetString()!);
            }

            return new HardSliceConfiguration(style.GetString()!, names);
        }
    }

    // Unknown keys are rejected: a misspelled key would otherwise be ignored without notice.
    private static void ExpectObject(JsonElement element, string name, params string[] keys)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw Invalid($"{name} must be a JSON object.");

        foreach (var property in element.EnumerateObject())
        {
            if (!keys.Contains(property.Name))
                throw Invalid($"unknown key '{property.Name}' in {name}. Allowed keys: {string.Join(", ", keys.Select(key => $"'{key}'"))}.");
        }
    }

    private static InvalidOperationException Invalid(string reason) => new($"{FileName}: {reason} {Fix}");

    // A namespace segment, so that the folder matches the namespace of the code it holds.
    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex FolderName();
}
