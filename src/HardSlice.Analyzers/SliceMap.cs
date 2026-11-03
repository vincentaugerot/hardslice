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
    private readonly ConcurrentDictionary<INamespaceSymbol, string[]?> _featuresPaths = new(SymbolEqualityComparer.Default);

    // Only symbols of the analyzed assembly are classified: cross-assembly references are out of scope.
    public string? SliceOf(ISymbol symbol) =>
        SymbolEqualityComparer.Default.Equals(symbol.ContainingAssembly, _assembly) && symbol.ContainingNamespace is { } ns
            ? _slices.GetOrAdd(ns, ComputeSlice)
            : null;

    // The namespaces of a symbol below '<AssemblyName>.Features': empty in 'Features' itself, [module] in the namespace of
    // a module, [module, slice, ...] in a slice, null outside 'Features' or outside the analyzed assembly.
    public string[]? FeaturesPathOf(ISymbol symbol) =>
        SymbolEqualityComparer.Default.Equals(symbol.ContainingAssembly, _assembly) && symbol.ContainingNamespace is { } ns
            ? _featuresPaths.GetOrAdd(ns, ComputeFeaturesPath)
            : null;

    // The same, for a namespace itself: [module, slice] for '<AssemblyName>.Features.<Module>.<Slice>'.
    public string[]? FeaturesPathOfNamespace(INamespaceSymbol ns) =>
        SymbolEqualityComparer.Default.Equals(ns.ContainingAssembly, _assembly) ? _featuresPaths.GetOrAdd(ns, ComputeFeaturesPath) : null;

    public static string ModuleOf(string slice) => slice.Substring(0, slice.IndexOf('/'));

    // Whether the compilation declares the namespace '<AssemblyName>.Features', which holds its slices.
    public static bool HoldsFeatures(Compilation compilation)
    {
        if (string.IsNullOrEmpty(compilation.AssemblyName))
            return false;

        var ns = compilation.Assembly.GlobalNamespace;
        foreach (var segment in compilation.AssemblyName!.Split('.').Append(FeaturesSegment))
        {
            if (ns.GetNamespaceMembers().FirstOrDefault(member => member.Name == segment) is not { } child)
                return false;

            ns = child;
        }

        return true;
    }

    private string? ComputeSlice(INamespaceSymbol ns) =>
        ComputeFeaturesPath(ns) is { Length: >= 2 } path ? $"{path[0]}/{path[1]}" : null;

    private string[]? ComputeFeaturesPath(INamespaceSymbol ns)
    {
        var segments = new List<string>();
        for (var current = ns; current is { IsGlobalNamespace: false }; current = current.ContainingNamespace)
            segments.Add(current.Name);
        segments.Reverse();

        var features = _rootSegments.Length;
        if (_rootSegments.Length == 0 || segments.Count < features + 1 || segments[features] != FeaturesSegment)
            return null;

        for (var i = 0; i < features; i++)
        {
            if (segments[i] != _rootSegments[i])
                return null;
        }

        return segments.Skip(features + 1).ToArray();
    }
}
