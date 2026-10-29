using System.Reflection;
using System.Runtime.CompilerServices;
using Assembly = System.Reflection.Assembly;
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using MyApp.Domain;
using MyApp.Shared.Messaging;

namespace MyApp.ArchitectureTests;

// The compiled application as the architecture tests see it. ArchUnitNET reads each type's dependencies, method bodies
// included, and folds compiler-generated types (lambdas, async state machines) into the type that declares them.
internal static class Application
{
    public static readonly Assembly DomainAssembly = typeof(DomainException).Assembly;
    public static readonly Assembly WebAssembly = typeof(IMediator).Assembly;

    public static readonly string DomainName = DomainAssembly.GetName().Name!;
    public static readonly string WebName = WebAssembly.GetName().Name!;

    public static readonly string SolutionRoot = typeof(Application).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(attribute => attribute.Key == "SolutionRoot")
        .Value!;

    private static readonly Lazy<Architecture> Architecture =
        new(() => new ArchLoader().LoadAssemblies(DomainAssembly, WebAssembly).Build());

    private static readonly Lazy<ILookup<ReferencedType, ReferencedType>> Catches = new(() =>
        (from assembly in new[] { DomainAssembly, WebAssembly }
         from type in assembly.GetTypes()
         where !type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
         from caught in CatchDependencies.Of(type)
         select (Source: ReferencedType.From(type), Target: ReferencedType.From(caught)))
        .ToLookup(dependency => dependency.Source, dependency => dependency.Target));

    public static IEnumerable<IType> TypesOf(Assembly assembly) =>
        Architecture.Value.Types.Where(type => type.Assembly.FullName == assembly.FullName);

    // Each type the given type uses, once, other than itself.
    public static IEnumerable<ReferencedType> TypesUsedBy(IType type) =>
        type.Dependencies
            .Select(dependency => ReferencedType.From(dependency.Target))
            .Concat(Catches.Value[ReferencedType.From(type)])
            .Where(target => target != ReferencedType.From(type))
            .Distinct();

    public static bool IsDeclaredIn(ReferencedType type, Assembly assembly) => type.AssemblyFullName == assembly.FullName;

    // 'Mediator.RequestDispatcher' for 'MyApp.Shared.Messaging.Mediator+RequestDispatcher`1'.
    public static string DisplayName(IType type) => DisplayName(ReferencedType.From(type));

    public static string DisplayName(ReferencedType type)
    {
        var name = type.FullName;
        if (type.Namespace.Length > 0)
            name = name[(type.Namespace.Length + 1)..];

        return string.Join('.', name.Split('+').Select(part => part.Split('`')[0]));
    }
}
