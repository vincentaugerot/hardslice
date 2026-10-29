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

    // Sources mark each expected HS016 location with [|...|]; every other location must stay silent.
    public static async Task VerifyAsync(MetadataReference[] references, params string[] markedSources)
    {
        var files = markedSources.Select((source, index) => (Path: $"File{index}.cs", Marked: Markup.Parse(source))).ToList();
        var compilation = Compile("MyApp", files.Select(file => (file.Path, file.Marked.Source)).Append(("Usings.cs", ImplicitUsings)), references);

        var actual = (await AnalyzeAsync(compilation))
            .Select(diagnostic => $"{diagnostic.Id} {diagnostic.Location.SourceTree!.FilePath}{diagnostic.Location.SourceSpan}");
        var expected = files.SelectMany(file => file.Marked.Spans.Select(span => $"{SliceReferenceAnalyzer.DiagnosticId} {file.Path}{span}"));

        actual.Order().ShouldBe(expected.Order());
    }

    public static Task VerifyAsync(params string[] markedSources) => VerifyAsync([], markedSources);

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

    private static class Markup
    {
        public static (string Source, ImmutableArray<TextSpan> Spans) Parse(string marked)
        {
            var source = new System.Text.StringBuilder();
            var spans = ImmutableArray.CreateBuilder<TextSpan>();
            var start = -1;
            for (var i = 0; i < marked.Length; i++)
            {
                if (marked.AsSpan(i).StartsWith("[|"))
                {
                    start = source.Length;
                    i++;
                }
                else if (marked.AsSpan(i).StartsWith("|]"))
                {
                    spans.Add(TextSpan.FromBounds(start, source.Length));
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
