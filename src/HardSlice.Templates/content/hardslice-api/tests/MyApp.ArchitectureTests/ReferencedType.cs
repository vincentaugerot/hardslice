using ArchUnitNET.Domain;

namespace MyApp.ArchitectureTests;

// A dependency target from either ArchUnitNET or reflection, identified within its assembly.
internal sealed record ReferencedType(string FullName, string Namespace, string AssemblyFullName)
{
    public static ReferencedType From(IType type) => new(type.FullName, type.Namespace.FullName, type.Assembly.FullName);

    public static ReferencedType From(Type type)
    {
        if (type.IsGenericType)
            type = type.GetGenericTypeDefinition();

        return new(type.FullName!, type.Namespace ?? "", type.Assembly.FullName!);
    }
}
