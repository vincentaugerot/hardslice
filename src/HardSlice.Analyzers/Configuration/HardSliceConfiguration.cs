using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace HardSlice.Analyzers.Configuration;

// hardslice.json, added to every compilation by build/HardSlice.Analyzers.targets. The analyzers check its version and its root
// keys, and read 'contracts'; the architecture tests validate 'style' and 'shared'.
// The format is versioned: 'version' is required, and a version these analyzers do not know is reported before anything else,
// since a later format may add keys. '$schema' is accepted for editors. Any other unknown key is rejected: a misspelled key would
// otherwise turn a rule off without notice.
internal sealed class HardSliceConfiguration
{
    public const string FileName = "hardslice.json";

    // The only version of the format these analyzers read.
    public const int Version = 1;

    private static readonly string[] RootKeys = ["$schema", "version", "style", "contracts", "shared"];

    // '<metadata name>, <assembly>', such as 'MyApp.Shared.Messaging.ICommand`1, MyApp'.
    private static readonly Regex ContractValue = new(
        @"^(?<type>[A-Za-z_][A-Za-z0-9_]*(?:`[0-9]+)?(?:[.+][A-Za-z_][A-Za-z0-9_]*(?:`[0-9]+)?)*), (?<assembly>[A-Za-z_][A-Za-z0-9_.-]*)$",
        RegexOptions.CultureInvariant);

    private HardSliceConfiguration(ConfigurationProblem? fileProblem, ImmutableDictionary<string, ContractEntry> contracts)
    {
        FileProblem = fileProblem;
        Contracts = contracts;
    }

    // Set when the file is missing, ambiguous or outside the expected format: no contract can be trusted then.
    public ConfigurationProblem? FileProblem { get; }

    // Declared contracts by key, well-formed or not. A key absent from the file is absent here.
    public ImmutableDictionary<string, ContractEntry> Contracts { get; }

    public static HardSliceConfiguration Read(AnalyzerOptions options, CancellationToken cancellationToken)
    {
        var files = options.AdditionalFiles
            .Where(file => string.Equals(Path.GetFileName(file.Path), FileName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (files.Length == 0)
        {
            options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.MSBuildProjectDirectory", out var projectDirectory);
            var where = string.IsNullOrEmpty(projectDirectory) ? "for this project" : $"in '{projectDirectory}' or above it";
            return Failed(new ConfigurationProblem(
                $"{FileName} was not found {where}",
                $"restore {FileName} at the solution root from version control",
                Location.None));
        }

        if (files.Length > 1)
        {
            return Failed(new ConfigurationProblem(
                $"several {FileName} files are given to the compiler: {string.Join(", ", files.Select(file => $"'{file.Path}'"))}",
                $"keep the {FileName} of the solution root only, and remove the other AdditionalFiles entries",
                Location.None));
        }

        var file = files[0];
        if (file.GetText(cancellationToken) is not { } text)
        {
            return Failed(new ConfigurationProblem(
                $"{FileName} at '{file.Path}' cannot be read",
                $"restore {FileName} at the solution root from version control",
                Location.None));
        }

        return Parse(file.Path, text);
    }

    public static HardSliceConfiguration Parse(string path, SourceText text)
    {
        JsonNode root;
        try
        {
            root = JsonReader.Parse(text.ToString());
        }
        catch (JsonFormatException exception)
        {
            var position = text.Lines.GetLinePosition(Math.Min(exception.Position, text.Length));
            return Failed(new ConfigurationProblem(
                $"{FileName} is not valid JSON at line {position.Line + 1}, column {position.Character + 1}: {exception.Message}",
                $"restore {FileName} from version control",
                At(new TextSpan(Math.Min(exception.Position, text.Length), 0))));
        }

        if (root is not JsonObject rootObject)
            return Failed(Format("the root of {0} must be a JSON object", root.Span, "{ \"version\": 1, \"style\": \"slices\", \"contracts\": { ... }, \"shared\": { ... } }"));

        if (VersionProblem(rootObject) is { } versionProblem)
            return Failed(versionProblem);

        if (UnknownOrRepeatedKey(rootObject, "", RootKeys) is { } rootProblem)
            return Failed(rootProblem);

        if (rootObject.Properties.FirstOrDefault(property => property.Name == "contracts") is not { } contractsProperty)
            return new HardSliceConfiguration(null, ImmutableDictionary<string, ContractEntry>.Empty);

        if (contractsProperty.Value is not JsonObject contracts)
            return Failed(Format("'contracts' in {0} must be a JSON object", contractsProperty.Value.Span, "one '<metadata name>, <assembly>' string per contract key"));

        if (UnknownOrRepeatedKey(contracts, "contracts.", ContractKeys.All) is { } contractsProblem)
            return Failed(contractsProblem);

        var entries = ImmutableDictionary.CreateBuilder<string, ContractEntry>();
        foreach (var contract in contracts.Properties)
        {
            entries.Add(contract.Name, contract.Value is JsonString value && ContractValue.Match(value.Value) is { Success: true } match
                ? new ContractEntry(contract.Name, match.Groups["type"].Value, match.Groups["assembly"].Value, null, At(value.Span))
                : new ContractEntry(contract.Name, null, null, Format(
                    $"'contracts.{contract.Name}' in {{0}} is not '<metadata name>, <assembly>'",
                    contract.Value.Span,
                    $"the metadata name of the contract type then its declaring assembly, such as \"{ContractKeys.Example(contract.Name)}\""), At(contract.Value.Span)));
        }

        return new HardSliceConfiguration(null, entries.ToImmutable());

        Location At(TextSpan span) => Location.Create(path, span, text.Lines.GetLinePositionSpan(span));

        // Checked before the keys: a file of a later version may hold keys that this version does not know.
        ConfigurationProblem? VersionProblem(JsonObject node)
        {
            var versions = node.Properties.Where(property => property.Name == "version").ToArray();
            if (versions.Length == 0)
            {
                return new ConfigurationProblem(
                    $"{FileName} has no 'version'",
                    $"restore {FileName} from version control; it starts with \"version\": {Version}, the version of its format",
                    At(new TextSpan(node.Span.Start, 1)));
            }

            var value = versions[0].Value;
            var literal = text.ToString(value.Span);
            if (value is not JsonLiteral || !int.TryParse(literal, NumberStyles.None, CultureInfo.InvariantCulture, out var version))
                return Format("'version' in {0} must be a whole number", value.Span, $"\"version\": {Version}");

            if (version != Version)
            {
                return new ConfigurationProblem(
                    $"{FileName} declares version {literal}, which this version of HardSlice.Analyzers does not read: it reads version {Version}",
                    $"update HardSlice.Analyzers, and the hardslice.tool local tool with it, to the version that wrote {FileName}",
                    At(value.Span));
            }

            return null;
        }

        ConfigurationProblem Format(string description, TextSpan span, string expected) =>
            new(string.Format(description, FileName), $"restore {FileName} from version control; it expects {expected}", At(span));

        // Unknown keys are rejected: a misspelled key would otherwise be ignored without notice.
        ConfigurationProblem? UnknownOrRepeatedKey(JsonObject node, string prefix, IReadOnlyCollection<string> allowed)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in node.Properties)
            {
                if (!allowed.Contains(property.Name))
                {
                    return new ConfigurationProblem(
                        $"{FileName} has an unknown key '{prefix}{property.Name}'",
                        $"restore {FileName} from version control; allowed keys are {string.Join(", ", allowed.Select(key => $"'{prefix}{key}'"))}",
                        At(property.NameSpan));
                }

                if (!seen.Add(property.Name))
                {
                    return new ConfigurationProblem(
                        $"{FileName} declares '{prefix}{property.Name}' twice",
                        $"restore {FileName} from version control; each key appears once",
                        At(property.NameSpan));
                }
            }

            return null;
        }
    }

    private static HardSliceConfiguration Failed(ConfigurationProblem problem) =>
        new(problem, ImmutableDictionary<string, ContractEntry>.Empty);
}

// One entry of 'contracts': its metadata name and declaring assembly, or the problem that makes it unusable.
internal sealed class ContractEntry(string key, string? metadataName, string? assemblyName, ConfigurationProblem? problem, Location location)
{
    public string Key { get; } = key;

    public string? MetadataName { get; } = metadataName;

    public string? AssemblyName { get; } = assemblyName;

    public ConfigurationProblem? Problem { get; } = problem;

    public Location Location { get; } = location;

    public string Display => $"{MetadataName}, {AssemblyName}";
}

// What is wrong, how to fix it, and where in hardslice.json when the file was read.
internal sealed class ConfigurationProblem(string description, string fix, Location location)
{
    public string Description { get; } = description;

    public string Fix { get; } = fix;

    public Location Location { get; } = location;
}
