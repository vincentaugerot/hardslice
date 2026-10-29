using System.Reflection;
using System.Runtime.CompilerServices;

namespace MyApp.ArchitectureTests;

// ArchUnitNET 0.13.4 omits catch-clause types. Read those metadata entries as well, without executing the methods.
internal static class CatchDependencies
{
    private const BindingFlags DeclaredMembers = BindingFlags.Public | BindingFlags.NonPublic
        | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    public static IEnumerable<Type> Of(Type type)
    {
        var methods = type.GetMethods(DeclaredMembers).Cast<MethodBase>().Concat(type.GetConstructors(DeclaredMembers));
        foreach (var method in methods)
        {
            if (method.GetMethodBody() is not { } body)
                continue;

            foreach (var clause in body.ExceptionHandlingClauses)
            {
                // A filter's type check is in its IL, already read by ArchUnitNET. CatchType is invalid for filters/finally.
                if (clause.Flags == ExceptionHandlingClauseOptions.Clause && clause.CatchType is { } caught)
                {
                    foreach (var dependency in TypeAndArguments(caught))
                        yield return dependency;
                }
            }
        }

        // Fold async/iterator state machines and lambda closures back into the type that declares them.
        // Ordinary nested types have their own dependencies and must not be attributed to their enclosing type.
        foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
            .Where(nested => nested.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)))
        {
            foreach (var caught in Of(nested))
                yield return caught;
        }
    }

    private static IEnumerable<Type> TypeAndArguments(Type type)
    {
        if (type.IsGenericParameter)
            yield break;
        if (type.HasElementType)
        {
            foreach (var element in TypeAndArguments(type.GetElementType()!))
                yield return element;
            yield break;
        }

        yield return type;
        foreach (var argument in type.GetGenericArguments())
        {
            foreach (var dependency in TypeAndArguments(argument))
                yield return dependency;
        }
    }
}
