using ArchUnitNET.Domain;

namespace MyApp.ArchitectureTests;

// Where a type of the web project sits, read from its namespace, which follows its folder. Same convention as HardSlice.Analyzers:
// '<App>.Features.<Module>' is the module public contract, '<App>.Features.<Module>.<Slice>' and below is one slice,
// '<App>.Shared.<Folder>' and below is cross-cutting code.
internal sealed record Location(bool InFeatures, string? Module, string? Slice, bool InShared, string? SharedFolder)
{
    private static readonly Location Elsewhere = new(false, null, null, false, null);

    public static Location Of(IType type) => Of(type.Namespace.FullName);
    public static Location Of(ReferencedType type) => Of(type.Namespace);

    public static Location Of(string? ns)
    {
        if (ns is null)
            return Elsewhere;

        if (SegmentsUnder(ns, "Features") is { } features)
        {
            return features.Length switch
            {
                0 => new Location(true, null, null, false, null),
                1 => new Location(true, features[0], null, false, null),
                _ => new Location(true, features[0], $"{features[0]}/{features[1]}", false, null)
            };
        }

        return SegmentsUnder(ns, "Shared") is { } shared
            ? new Location(false, null, null, true, shared.FirstOrDefault())
            : Elsewhere;
    }

    // How a message names where a type is: "in slice 'Orders/CancelOrder'", "in module 'Orders'", "in 'Shared/Behaviors'".
    public string Describe() => this switch
    {
        { Slice: { } slice } => $"in slice '{slice}'",
        { Module: { } module } => $"in module '{module}'",
        { InFeatures: true } => "in 'Features'",
        { SharedFolder: { } folder } => $"in 'Shared/{folder}'",
        { InShared: true } => "in 'Shared'",
        _ => "outside Features and Shared"
    };

    private static string[]? SegmentsUnder(string ns, string area)
    {
        var root = $"{Application.WebName}.{area}";
        if (ns == root)
            return [];

        return ns.StartsWith(root + ".", StringComparison.Ordinal) ? ns[(root.Length + 1)..].Split('.') : null;
    }
}
