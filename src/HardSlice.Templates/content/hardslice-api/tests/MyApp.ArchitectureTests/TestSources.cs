using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MyApp.ArchitectureTests;

// The tests of the solution as they sit on disk. 'tests/<App>.Tests/' and 'tests/<App>.IntegrationTests/' mirror 'src/<App>/':
// the tests of slice 'Orders/PlaceOrder' are in 'Features/Orders/PlaceOrder/' of either project (HS014, HS018).
// Read from the C# files rather than the compiled test assemblies, which the architecture tests neither build nor reference.
internal sealed class TestSources(
    string solutionRoot,
    string appName,
    IReadOnlyDictionary<string, string[]>? projectSymbols = null,
    IReadOnlyDictionary<string, string[]>? projectSources = null)
{
    public static readonly TestSources Solution = new(
        Application.SolutionRoot, Application.WebName, InputsFromBuild("TestSymbols:", [';', ',']), InputsFromBuild("TestSources:", [';']));

    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public string UnitTests { get; } = $"{appName}.Tests";
    public string IntegrationTests { get; } = $"{appName}.IntegrationTests";

    private IEnumerable<string> Projects => [UnitTests, IntegrationTests];

    // 'tests/MyApp.Tests/Features/Orders/PlaceOrder/' for that folder of 'src/MyApp/' in the unit test project.
    public static string PathOf(string project, string folder) => folder.Length == 0 ? $"tests/{project}/" : $"tests/{project}/{folder}/";

    // "'tests/MyApp.Tests/Features/Orders/PlaceOrder/' or 'tests/MyApp.IntegrationTests/Features/Orders/PlaceOrder/'".
    public string Describe(string folder) => string.Join(" or ", Projects.Select(project => $"'{PathOf(project, folder)}'"));

    // Whether a test project holds a C# file in that folder of 'src/<App>/', such as 'Features/Orders/PlaceOrder', or below it.
    public bool HasFilesIn(string folder) => FilesIn(folder, SearchOption.AllDirectories).Any();

    // Whether a test project declares a test method in that folder, or below it when 'recursive'.
    public bool HasTestsIn(string folder, bool recursive) =>
        FilesIn(folder, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
            .GroupBy(file => file.Project)
            .Any(group => HasCompiledTests(group.Key, group.Select(file => Path.GetFullPath(file.Path)).ToHashSet(PathComparer)));

    private bool HasCompiledTests(string project, HashSet<string> candidates)
    {
        var files = projectSources is not null
            ? projectSources[project]
            : Directory.EnumerateFiles(Path.Combine(solutionRoot, "tests", project), "*.cs", SearchOption.AllDirectories);
        var options = new CSharpParseOptions(preprocessorSymbols: projectSymbols?[project]);
        var sources = files.Where(File.Exists)
            .Select(path => (Path: Path.GetFullPath(path), Root: CSharpSyntaxTree.ParseText(File.ReadAllText(path), options).GetCompilationUnitRoot()))
            .ToArray();
        var globalUsings = sources.SelectMany(source => source.Root.Usings).Where(usingDirective => usingDirective.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword)).ToArray();

        return sources.Any(source => candidates.Contains(source.Path) && TestMethodsIn(source.Root, globalUsings).Any());
    }

    // The module and slice folders of 'Features' in each test project, 'Orders' and 'Orders/PlaceOrder', that hold C# files.
    public IEnumerable<(string Project, string Folder)> FeatureFolders() =>
        from project in Projects
        from folder in FeatureFoldersUnder(Path.Combine(solutionRoot, "tests", project, "Features"))
        select (project, folder);

    // The module and slice folders under a 'Features' folder, at depth one and two, that hold C# files.
    public static IEnumerable<string> FeatureFoldersUnder(string features) =>
        from module in SubfoldersWithFiles(features)
        from folder in SubfoldersWithFiles(module).Select(slice => $"{Path.GetFileName(module)}/{Path.GetFileName(slice)}")
            .Prepend(Path.GetFileName(module))
        select folder;

    // The methods marked [Fact], [Theory] or a custom xUnit attribute named like them, such as [RetryFact].
    // Read from the syntax tree, so commented-out and disabled code never counts.
    public static IEnumerable<string> TestMethodsIn(string source, IEnumerable<string>? symbols = null) =>
        TestMethodsIn(CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(preprocessorSymbols: symbols)).GetCompilationUnitRoot(), []);

    private static IEnumerable<string> TestMethodsIn(CompilationUnitSyntax root, IEnumerable<UsingDirectiveSyntax> globalUsings) =>
        from method in root.DescendantNodes().OfType<MethodDeclarationSyntax>()
        let usings = method.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().SelectMany(ns => ns.Usings)
            .Concat(root.Usings).Concat(globalUsings)
        where method.AttributeLists.SelectMany(list => list.Attributes).Any(attribute => IsTestAttribute(attribute, usings))
        select method.Identifier.Text;

    private IEnumerable<(string Project, string Path)> FilesIn(string folder, SearchOption search) =>
        from project in Projects
        let directory = Path.Combine([solutionRoot, "tests", project, .. folder.Split('/', StringSplitOptions.RemoveEmptyEntries)])
        where Directory.Exists(directory)
        from file in Directory.EnumerateFiles(directory, "*.cs", search)
        select (project, file);

    private static IReadOnlyDictionary<string, string[]> InputsFromBuild(string prefix, char[] separators) =>
        typeof(TestSources).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Where(attribute => attribute.Key.StartsWith(prefix, StringComparison.Ordinal))
            .ToDictionary(attribute => attribute.Key[prefix.Length..],
                attribute => (attribute.Value ?? "").Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static IEnumerable<string> SubfoldersWithFiles(string directory) =>
        Directory.Exists(directory)
            ? Directory.EnumerateDirectories(directory).Where(sub => Directory.EnumerateFiles(sub, "*.cs", SearchOption.AllDirectories).Any())
            : [];

    private static bool IsTestAttribute(AttributeSyntax attribute, IEnumerable<UsingDirectiveSyntax> usings)
    {
        NameSyntax name = attribute.Name;
        if (name is IdentifierNameSyntax identifier)
        {
            // Nearest namespace first, then file and project aliases. A shadowing alias must also override a Fact-like name.
            var aliases = usings.Where(usingDirective => usingDirective.Alias is not null).ToArray();
            var alias = aliases.FirstOrDefault(usingDirective => usingDirective.Alias!.Name.Identifier.ValueText == identifier.Identifier.ValueText);
            if (alias is null && !identifier.Identifier.Text.StartsWith('@'))
                alias = aliases.FirstOrDefault(usingDirective => usingDirective.Alias!.Name.Identifier.ValueText == identifier.Identifier.ValueText + "Attribute");
            name = alias?.Name ?? name;
        }

        var shortName = name switch
        {
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax aliased => aliased.Name.Identifier.ValueText,
            SimpleNameSyntax simple => simple.Identifier.ValueText,
            _ => ""
        };
        if (shortName.EndsWith("Attribute", StringComparison.Ordinal))
            shortName = shortName[..^"Attribute".Length];

        return shortName.EndsWith("Fact", StringComparison.Ordinal) || shortName.EndsWith("Theory", StringComparison.Ordinal);
    }
}
