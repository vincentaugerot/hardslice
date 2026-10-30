using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MyApp.ArchitectureTests;

// The tests of the solution as they sit on disk. 'tests/<App>.Tests/' and 'tests/<App>.IntegrationTests/' mirror 'src/<App>/':
// the tests of slice 'Bookings/ConfirmBooking' are in 'Features/Bookings/ConfirmBooking/' of either project (HS014, HS018).
// Read from the C# files rather than the compiled test assemblies, which the architecture tests neither build nor reference.
internal sealed class TestSources(string solutionRoot, string appName, IReadOnlyDictionary<string, string[]>? projectSymbols = null)
{
    public static readonly TestSources Solution = new(Application.SolutionRoot, Application.WebName, SymbolsFromBuild());

    public string UnitTests { get; } = $"{appName}.Tests";
    public string IntegrationTests { get; } = $"{appName}.IntegrationTests";

    private IEnumerable<string> Projects => [UnitTests, IntegrationTests];

    // 'tests/MyApp.Tests/Features/Bookings/ConfirmBooking/' for that folder of 'src/MyApp/' in the unit test project.
    public static string PathOf(string project, string folder) => folder.Length == 0 ? $"tests/{project}/" : $"tests/{project}/{folder}/";

    // "'tests/MyApp.Tests/Features/Bookings/ConfirmBooking/' or 'tests/MyApp.IntegrationTests/Features/Bookings/ConfirmBooking/'".
    public string Describe(string folder) => string.Join(" or ", Projects.Select(project => $"'{PathOf(project, folder)}'"));

    // Whether a test project holds a C# file in that folder of 'src/<App>/', such as 'Features/Bookings/ConfirmBooking', or below it.
    public bool HasFilesIn(string folder) => FilesIn(folder, SearchOption.AllDirectories).Any();

    // Whether a test project declares a test method in that folder, or below it when 'recursive'.
    public bool HasTestsIn(string folder, bool recursive) =>
        FilesIn(folder, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
            .Any(file => TestMethodsIn(File.ReadAllText(file.Path), projectSymbols?[file.Project]).Any());

    // The module and slice folders of 'Features' in each test project, 'Bookings' and 'Bookings/ConfirmBooking', that hold C# files.
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
        from method in CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(preprocessorSymbols: symbols))
            .GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
        where method.AttributeLists.SelectMany(list => list.Attributes).Any(IsTestAttribute)
        select method.Identifier.Text;

    private IEnumerable<(string Project, string Path)> FilesIn(string folder, SearchOption search) =>
        from project in Projects
        let directory = Path.Combine([solutionRoot, "tests", project, .. folder.Split('/', StringSplitOptions.RemoveEmptyEntries)])
        where Directory.Exists(directory)
        from file in Directory.EnumerateFiles(directory, "*.cs", search)
        select (project, file);

    private static IReadOnlyDictionary<string, string[]> SymbolsFromBuild() =>
        typeof(TestSources).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Where(attribute => attribute.Key.StartsWith("TestSymbols:", StringComparison.Ordinal))
            .ToDictionary(attribute => attribute.Key["TestSymbols:".Length..],
                attribute => (attribute.Value ?? "").Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static IEnumerable<string> SubfoldersWithFiles(string directory) =>
        Directory.Exists(directory)
            ? Directory.EnumerateDirectories(directory).Where(sub => Directory.EnumerateFiles(sub, "*.cs", SearchOption.AllDirectories).Any())
            : [];

    private static bool IsTestAttribute(AttributeSyntax attribute)
    {
        var name = attribute.Name switch
        {
            QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
            AliasQualifiedNameSyntax aliased => aliased.Name.Identifier.Text,
            SimpleNameSyntax simple => simple.Identifier.Text,
            _ => ""
        };
        if (name.EndsWith("Attribute", StringComparison.Ordinal))
            name = name[..^"Attribute".Length];

        return name.EndsWith("Fact", StringComparison.Ordinal) || name.EndsWith("Theory", StringComparison.Ordinal);
    }
}
