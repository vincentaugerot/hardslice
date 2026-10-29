namespace MyApp.ArchitectureTests;

internal static class TypeVisibility
{
    // IsVisible covers public nesting only. A derived type in another assembly can also reach protected nested types.
    public static bool IsExternallyAccessible(Type type) => type.DeclaringType is { } declaring
        ? (type.IsNestedPublic || type.IsNestedFamily || type.IsNestedFamORAssem) && IsExternallyAccessible(declaring)
        : type.IsPublic;
}
