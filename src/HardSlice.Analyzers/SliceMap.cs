using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;

namespace HardSlice.Analyzers;

// Template convention: a slice is the namespace '<AssemblyName>.Features.<Module>.<Slice>' and everything below it.
// Types directly in '<AssemblyName>.Features.<Module>' form the module public contract and belong to no slice.
internal sealed class SliceMap(Compilation compilation)
{
    private const string FeaturesSegment = "Features";

    private readonly IAssemblySymbol _assembly = compilation.Assembly;
    private readonly string[] _rootSegments = compilation.AssemblyName?.Split('.') ?? [];
    private readonly ConcurrentDictionary<INamespaceSymbol, string?> _slices = new(SymbolEqualityComparer.Default);

    // Only symbols of the analyzed assembly are classified: cross-assembly references are out of scope.
    public string? SliceOf(ISymbol symbol) =>
        SymbolEqualityComparer.Default.Equals(symbol.ContainingAssembly, _assembly) && symbol.ContainingNamespace is { } ns
            ? _slices.GetOrAdd(ns, ComputeSlice)
            : null;

    private string? ComputeSlice(INamespaceSymbol ns)
    {
        var segments = new List<string>();
        for (var current = ns; current is { IsGlobalNamespace: false }; current = current.ContainingNamespace)
            segments.Add(current.Name);
        segments.Reverse();

        var features = _rootSegments.Length;
        if (_rootSegments.Length == 0 || segments.Count < features + 3 || segments[features] != FeaturesSegment)
            return null;

        for (var i = 0; i < features; i++)
        {
            if (segments[i] != _rootSegments[i])
                return null;
        }

        return $"{segments[features + 1]}/{segments[features + 2]}";
    }
}
