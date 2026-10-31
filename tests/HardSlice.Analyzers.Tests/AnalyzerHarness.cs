using System.Collections.Immutable;
using Microsoft.AspNetCore.Builder;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace HardSlice.Analyzers.Tests;

internal static class AnalyzerHarness
{
    public const string ImplicitUsings =
        """
        global using System;
        global using System.Collections.Generic;
        global using System.IO;
        global using System.Linq;
        global using System.Net.Http;
        global using System.Threading;
        global using System.Threading.Tasks;
        """;

    private static readonly ImmutableArray<MetadataReference> FrameworkReferences = LoadFrameworkReferences();

    public static CSharpCompilation Compile(
        string assemblyName,
        IEnumerable<(string Path, string Source)> files,
        MetadataReference[]? references = null,
        OutputKind outputKind = OutputKind.DynamicallyLinkedLibrary)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName,
            files.Select(file => CSharpSyntaxTree.ParseText(file.Source, new CSharpParseOptions(LanguageVersion.Latest), file.Path)),
            FrameworkReferences.AddRange(references ?? []),
            new CSharpCompilationOptions(outputKind, nullableContextOptions: NullableContextOptions.Enable));

        compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ShouldBeEmpty();

        return compilation;
    }

    public static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(Compilation compilation) =>
        compilation
            .WithAnalyzers([new SliceReferenceAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

    // Every HardSlice analyzer, with hardslice.json and the MSBuild properties that build/HardSlice.Analyzers.props
    // and .targets give the compiler. A null configuration means no hardslice.json was found.
    public static Task<ImmutableArray<Diagnostic>> AnalyzeAllAsync(
        Compilation compilation,
        string? configuration,
        bool isTestProject = false) =>
        compilation
            .WithAnalyzers(Analyzers(), Options(configuration, isTestProject))
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

    // The same, with the .editorconfig and global config files the compiler receives, as (path, text):
    // Roslyn merges them, an .editorconfig section taking precedence over a global config.
    public static Task<ImmutableArray<Diagnostic>> AnalyzeAllAsync(
        Compilation compilation,
        string? configuration,
        IEnumerable<(string Path, string Text)> analyzerConfigs) =>
        compilation
            .WithOptions(compilation.Options.WithSyntaxTreeOptionsProvider(new AnalyzerConfigSeverities(
                AnalyzerConfigSet.Create(analyzerConfigs.Select(config => AnalyzerConfig.Parse(config.Text, config.Path)).ToImmutableArray()))))
            .WithAnalyzers(Analyzers(), Options(configuration))
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

    public static ImmutableArray<DiagnosticAnalyzer> Analyzers() =>
        [new SuppressionJustificationAnalyzer(), new SliceReferenceAnalyzer(), new ClassificationAnalyzer(), new EndpointDatabaseAccessAnalyzer(), new SliceEndpointAnalyzer(), new AsyncUsageAnalyzer(), new SystemClockAnalyzer(), new SliceStructureAnalyzer(), new CommandResponseAnalyzer(), new QueryHandlerTrackingAnalyzer(), new EntityStateAnalyzer(), new AggregateReferenceAnalyzer(), new DomainEventRecordAnalyzer(), new DomainEventNameAnalyzer()];

    public const string ConfigurationPath = "/solution/hardslice.json";

    public static AnalyzerOptions Options(string? configuration, bool isTestProject = false)
    {
        var properties = ImmutableDictionary.CreateBuilder<string, string>();
        properties.Add("build_property.MSBuildProjectDirectory", "/solution/src/MyApp");
        properties.Add("build_property.IsTestProject", isTestProject ? "true" : "");

        return new AnalyzerOptions(
            configuration is null ? [] : [new InMemoryAdditionalText(ConfigurationPath, configuration)],
            new InMemoryOptionsProvider(properties.ToImmutable()));
    }

    // Assemblies of the packages restored for this test project, such as EF Core, at the template's versions.
    public static IEnumerable<MetadataReference> PackageReferences(params string[] assemblyPrefixes) =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => Path.GetFileName(path) is var name
                && assemblyPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .Select(path => MetadataReference.CreateFromFile(path));

    // A project reference passes the compilation itself; a package or a prebuilt assembly passes metadata only.
    public static MetadataReference Reference(Compilation compilation, bool asMetadata)
    {
        if (!asMetadata)
            return compilation.ToMetadataReference();

        using var image = new MemoryStream();
        var emit = compilation.Emit(image, cancellationToken: TestContext.Current.CancellationToken);
        emit.Success.ShouldBeTrue(string.Join(Environment.NewLine, emit.Diagnostics));
        return MetadataReference.CreateFromImage(image.ToArray());
    }

    // Sources mark each expected HS016 location with [|...|]; every other location must stay silent.
    public static Task VerifyAsync(MetadataReference[] references, params string[] markedSources) =>
        VerifyAsync(new SliceReferenceAnalyzer(), SliceReferenceAnalyzer.DiagnosticId, references, OutputKind.DynamicallyLinkedLibrary, markedSources);

    public static Task VerifyAsync(params string[] markedSources) => VerifyAsync([], markedSources);

    // Sources mark each expected location of the analyzer's diagnostic with [|...|]; every other location must stay silent.
    // The analyzer runs without hardslice.json, outside a test project. Returns the diagnostics, in source order.
    public static Task<ImmutableArray<Diagnostic>> VerifyAsync(
        DiagnosticAnalyzer analyzer,
        string diagnosticId,
        MetadataReference[] references,
        OutputKind outputKind,
        params string[] markedSources) =>
        VerifyAsync(analyzer, diagnosticId, "MyApp", configuration: null, references, outputKind, markedSources);

    // The same, in the assembly named assemblyName, with the given hardslice.json.
    public static Task<ImmutableArray<Diagnostic>> VerifyAsync(
        DiagnosticAnalyzer analyzer,
        string diagnosticId,
        string assemblyName,
        string? configuration,
        MetadataReference[] references,
        OutputKind outputKind,
        params string[] markedSources) =>
        VerifyAsync(
            analyzer,
            diagnosticId,
            assemblyName,
            configuration,
            references,
            outputKind,
            markedSources.Select((source, index) => ($"File{index}.cs", source)).ToArray());

    // The same, with the path of each file relative to the project directory.
    public static async Task<ImmutableArray<Diagnostic>> VerifyAsync(
        DiagnosticAnalyzer analyzer,
        string diagnosticId,
        string assemblyName,
        string? configuration,
        MetadataReference[] references,
        OutputKind outputKind,
        params (string Path, string MarkedSource)[] markedFiles)
    {
        var files = markedFiles.Select(file => (file.Path, Marked: Markup.Parse(file.MarkedSource))).ToList();
        var compilation = Compile(
            assemblyName,
            files.Select(file => (file.Path, file.Marked.Source)).Append(("Usings.cs", ImplicitUsings)),
            references,
            outputKind);

        var diagnostics = (await compilation
                .WithAnalyzers([analyzer], Options(configuration))
                .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken))
            .OrderBy(diagnostic => diagnostic.Location.SourceTree!.FilePath, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Location.SourceSpan.Start)
            .ToImmutableArray();
        var actual = diagnostics.Select(diagnostic => $"{diagnostic.Id} {diagnostic.Location.SourceTree!.FilePath}{diagnostic.Location.SourceSpan}");
        var expected = files.SelectMany(file => file.Marked.Spans.Select(span => $"{diagnosticId} {file.Path}{span}"));

        actual.Order().ShouldBe(expected.Order());
        return diagnostics;
    }

    private static ImmutableArray<MetadataReference> LoadFrameworkReferences()
    {
        string[] frameworkDirectories =
        [
            Path.GetDirectoryName(typeof(object).Assembly.Location)!,
            Path.GetDirectoryName(typeof(WebApplication).Assembly.Location)!
        ];

        return [
            ..((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator)
                .Where(path => frameworkDirectories.Contains(Path.GetDirectoryName(path)))
                .Select(path => MetadataReference.CreateFromFile(path))
        ];
    }

    // Severities as the compiler reads them: the sections of the file's path first, then the global configs.
    private sealed class AnalyzerConfigSeverities(AnalyzerConfigSet configs) : SyntaxTreeOptionsProvider
    {
        public override GeneratedKind IsGenerated(SyntaxTree tree, CancellationToken cancellationToken) => GeneratedKind.Unknown;

        public override bool TryGetDiagnosticValue(SyntaxTree tree, string diagnosticId, CancellationToken cancellationToken, out ReportDiagnostic severity) =>
            configs.GetOptionsForSourcePath(tree.FilePath).TreeOptions.TryGetValue(diagnosticId, out severity);

        public override bool TryGetGlobalDiagnosticValue(string diagnosticId, CancellationToken cancellationToken, out ReportDiagnostic severity) =>
            configs.GlobalConfigOptions.TreeOptions.TryGetValue(diagnosticId, out severity);
    }

    private sealed class InMemoryAdditionalText(string path, string text) : AdditionalText
    {
        public override string Path { get; } = path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text);
    }

    private sealed class InMemoryOptionsProvider(ImmutableDictionary<string, string> globalOptions) : AnalyzerConfigOptionsProvider
    {
        private static readonly InMemoryOptions Empty = new(ImmutableDictionary<string, string>.Empty);

        public override AnalyzerConfigOptions GlobalOptions { get; } = new InMemoryOptions(globalOptions);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Empty;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Empty;
    }

    private sealed class InMemoryOptions(ImmutableDictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value) =>
            values.TryGetValue(key, out value);
    }

    private static class Markup
    {
        public static (string Source, ImmutableArray<TextSpan> Spans) Parse(string marked)
        {
            var source = new System.Text.StringBuilder();
            var spans = ImmutableArray.CreateBuilder<TextSpan>();
            // Markers nest: [|[|db|]|] expects two diagnostics on 'db'.
            var starts = new Stack<int>();
            for (var i = 0; i < marked.Length; i++)
            {
                if (marked.AsSpan(i).StartsWith("[|"))
                {
                    starts.Push(source.Length);
                    i++;
                }
                else if (marked.AsSpan(i).StartsWith("|]"))
                {
                    spans.Add(TextSpan.FromBounds(starts.Pop(), source.Length));
                    i++;
                }
                else
                {
                    source.Append(marked[i]);
                }
            }

            return (source.ToString(), spans.ToImmutable());
        }
    }
}
