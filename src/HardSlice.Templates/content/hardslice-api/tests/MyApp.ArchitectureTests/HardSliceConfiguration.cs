using System.Text.Json;
using System.Text.RegularExpressions;

namespace MyApp.ArchitectureTests;

// hardslice.json at the solution root: the architecture style, the contracts that classify types (read by the analyzers, HS900)
// and the folders Shared may contain (HS017).
// A missing or invalid file fails the architecture tests instead of silently falling back to defaults.
internal sealed partial record HardSliceConfiguration(string Style, IReadOnlyDictionary<string, string> Contracts, IReadOnlyList<string> SharedFolders)
{
    public const string FileName = "hardslice.json";

    private const string Fix = "Fix: restore hardslice.json from version control; changing it requires a maintainer decision.";

    // The skill and the sheet that end each message: HS017 for the file and its Shared folders, HS900 for its contracts.
    private const string SharedRule = "Skill: fix-architecture-violation. Doc: 'docs/rules/HS017.md'.";
    private const string ContractsRule = "Skill: fix-architecture-violation. Doc: 'docs/rules/HS900.md'.";

    private static readonly string[] ContractKeys = ["entity", "aggregateRoot", "domainEvent", "command", "query", "requestHandler", "domainEventHandler"];

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
            ExpectObject(root, "the root", "style", "contracts", "shared");

            if (!root.TryGetProperty("style", out var style) || style.ValueKind != JsonValueKind.String || style.GetString() != "slices")
                throw Invalid("'style' must be \"slices\", the only architecture style these tests check.");

            var contracts = ReadContracts(root);

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

            return new HardSliceConfiguration(style.GetString()!, contracts, names);
        }
    }

    // Each contract is '<metadata name>, <declaring assembly>'. The analyzers resolve them: HS900 reports a contract they cannot find.
    private static Dictionary<string, string> ReadContracts(JsonElement root)
    {
        if (!root.TryGetProperty("contracts", out var contracts))
            throw Invalid("'contracts' is missing: it declares the types that classify entities, aggregate roots, domain events, commands, queries, request handlers and domain event handlers.", ContractsRule);
        ExpectObjectWithKeys(contracts, "'contracts'", ContractKeys, ContractsRule);

        var values = new Dictionary<string, string>();
        foreach (var key in ContractKeys)
        {
            if (!contracts.TryGetProperty(key, out var contract))
                throw Invalid($"'contracts.{key}' is missing.", ContractsRule);
            if (contract.ValueKind != JsonValueKind.String || !ContractName().IsMatch(contract.GetString()!))
                throw Invalid($"'contracts.{key}' is {contract.GetRawText()}, not \"<metadata name>, <assembly>\" such as \"MyApp.Domain.IEntity, MyApp.Domain\".", ContractsRule);
            values.Add(key, contract.GetString()!);
        }

        return values;
    }

    // Unknown keys are rejected: a misspelled key would otherwise be ignored without notice.
    private static void ExpectObject(JsonElement element, string name, params string[] keys) =>
        ExpectObjectWithKeys(element, name, keys, SharedRule);

    private static void ExpectObjectWithKeys(JsonElement element, string name, string[] keys, string rule)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw Invalid($"{name} must be a JSON object.", rule);

        foreach (var property in element.EnumerateObject())
        {
            if (!keys.Contains(property.Name))
                throw Invalid($"unknown key '{property.Name}' in {name}. Allowed keys: {string.Join(", ", keys.Select(key => $"'{key}'"))}.", rule);
        }
    }

    private static InvalidOperationException Invalid(string reason, string rule = SharedRule) =>
        new($"{FileName}: {reason} {Fix} {rule}");

    // A namespace segment, so that the folder matches the namespace of the code it holds.
    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex FolderName();

    // A metadata name ('MyApp.Shared.Messaging.ICommand`1'), a comma, then the simple name of its declaring assembly.
    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*(`[0-9]+)?([.+][A-Za-z_][A-Za-z0-9_]*(`[0-9]+)?)*, [A-Za-z_][A-Za-z0-9_.-]*$")]
    private static partial Regex ContractName();
}
