using System.Collections.Immutable;
using HardSlice.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace HardSlice.Analyzers;

// HS008: an aggregate references another aggregate by its identifier only. In the compilation that declares an entity, root or
// child, the rule reads the type of each instance property and field, of any accessibility since EF Core maps private fields
// too: the type contains a root when it is one, or when an element of an array or a type argument contains one. A root never
// contains another aggregate, not even another instance of its own type. A child entity may reference the possible owners
// that reach it, the roots whose members lead to its type directly or through other child entities; any other root is reported
// when its members are all visible, that is when it is declared in this compilation. A child that no root of the compilation
// reaches, or a root of another assembly, is ambiguous and stays silent. The members inherited from a base class are reported
// on the entity, unless that base is itself an entity of the compilation, which reports them. Generated code is analyzed.
// Not applied to test projects.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AggregateReferenceAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "HS008";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "An aggregate references another aggregate other than by its identifier",
        messageFormat: "{0} references {1} through {2}. An aggregate references another aggregate only by its identifier. Fix: {3} and load '{4}' in the handler that needs it. Doc: 'docs/rules/HS008.md'.",
        category: "HardSlice.Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly SymbolDisplayFormat DisplayFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        // An auto-generated header must not exempt an entity from the rule.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (RuleActivation.IsTestProject(start.Options))
                return;

            // Without usable entity and aggregate root contracts, HS900 reports the configuration and no aggregate can be recognized.
            var model = DomainModel.Create(start.Compilation, HardSliceConfiguration.Read(start.Options, start.CancellationToken));
            if (model.Contract(ContractKeys.Entity) is null || model.Contract(ContractKeys.AggregateRoot) is null)
                return;

            // Computed once, the first time a child entity of the compilation is analyzed: roots alone never need it.
            var owners = new Lazy<Ownership>(() => Ownership.Create(start.Compilation, model), LazyThreadSafetyMode.ExecutionAndPublication);
            start.RegisterSymbolAction(symbolContext => Analyze(symbolContext, model, owners), SymbolKind.NamedType);
        });
    }

    private static void Analyze(SymbolAnalysisContext context, DomainModel model, Lazy<Ownership> ownership)
    {
        var entity = (INamedTypeSymbol)context.Symbol;
        // IAggregateRoot implements IEntity, but an interface holds no reference.
        if (!IsStateful(entity) || !model.IsEntity(entity))
            return;

        var isRoot = model.IsAggregateRoot(entity);
        var owners = isRoot ? ImmutableArray<INamedTypeSymbol>.Empty : ownership.Value.Of(entity);
        // A child that no root of the compilation reaches belongs to no known aggregate: no violation can be asserted.
        if (!isRoot && owners.IsEmpty)
            return;

        var overridden = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        for (var type = entity; type is not null; type = type.BaseType)
        {
            // A base entity of this compilation reports its own members and those it inherits.
            if (!ReferenceEquals(type, entity)
                && model.IsEntity(type)
                && SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, context.Compilation.Assembly))
            {
                break;
            }

            foreach (var member in type.GetMembers())
            {
                if (member is IPropertySymbol property)
                {
                    // An override of the same type is reported where the property is declared; a covariant one replaces it.
                    if (overridden.Contains(property.OriginalDefinition)
                        || (property.OverriddenProperty is { } baseProperty && SymbolEqualityComparer.Default.Equals(baseProperty.Type, property.Type)))
                    {
                        continue;
                    }

                    for (var overriddenProperty = property.OverriddenProperty; overriddenProperty is not null; overriddenProperty = overriddenProperty.OverriddenProperty)
                        overridden.Add(overriddenProperty.OriginalDefinition);
                }

                if (StateType(member) is not { } memberType)
                    continue;

                var root = ContainedTypes(memberType).FirstOrDefault(candidate =>
                    model.IsAggregateRoot(candidate)
                    && (isRoot || IsForeignRoot(candidate, owners, context.Compilation)));
                if (root is not null)
                    Report(context, entity, owners, type, member, memberType, root);
            }
        }
    }

    // From a child entity, a root that is not one of its possible owners and whose members are all visible here.
    private static bool IsForeignRoot(INamedTypeSymbol root, ImmutableArray<INamedTypeSymbol> owners, Compilation compilation) =>
        IsStateful(root)
        && SymbolEqualityComparer.Default.Equals(root.ContainingAssembly, compilation.Assembly)
        && !owners.Contains(root.OriginalDefinition, SymbolEqualityComparer.Default);

    private static void Report(
        SymbolAnalysisContext context,
        INamedTypeSymbol entity,
        ImmutableArray<INamedTypeSymbol> owners,
        INamedTypeSymbol declaringType,
        ISymbol member,
        ITypeSymbol memberType,
        INamedTypeSymbol root)
    {
        var subject = owners.IsEmpty
            ? $"aggregate '{Name(entity)}'"
            : $"entity '{Name(entity)}' of {(owners.Length == 1 ? "aggregate" : "aggregates")} {List(owners.Select(Name))}";
        var target = SymbolEqualityComparer.Default.Equals(root.OriginalDefinition, entity)
            ? $"another aggregate '{Name(root)}'"
            : $"aggregate '{Name(root)}'";
        var kind = member is IFieldSymbol ? "field" : "property";
        var inherited = ReferenceEquals(declaringType, entity) ? null : declaringType.OriginalDefinition.ToDisplayString(DisplayFormat);
        var through = inherited is null ? $"{kind} '{member.Name}'" : $"{kind} '{member.Name}', inherited from '{inherited}'";
        var replaced = inherited is null ? $"the {kind}" : $"the {kind} '{inherited}.{member.Name}'";
        var fix = IsDirect(memberType, root)
            ? $"replace {replaced} with '{IdentifierName(member.Name, root)}'"
            : $"replace '{Name(root)}' with its identifier in the type of {replaced}";
        var location = inherited is null ? member.Locations.FirstOrDefault(candidate => candidate.IsInSource) : null;

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            location ?? entity.Locations.FirstOrDefault(candidate => candidate.IsInSource) ?? Location.None,
            subject,
            target,
            through,
            fix,
            Name(root)));
    }

    // 'RoomId' for 'Room', 'PreviousBookingId' for 'PreviousBooking', '_roomId' for '_room': the member keeps its name when it
    // ends with the name of the root. Otherwise the identifier is named after the root, in the case of the member: 'RoomKeyId' for 'Key'.
    private static string IdentifierName(string memberName, INamedTypeSymbol root)
    {
        if (memberName.EndsWith(root.Name, StringComparison.OrdinalIgnoreCase))
            return memberName + "Id";

        var prefix = memberName.StartsWith("_", StringComparison.Ordinal) ? "_" : "";
        var name = memberName.TrimStart('_');
        var rootName = name.Length > 0 && char.IsLower(name[0]) ? char.ToLowerInvariant(root.Name[0]) + root.Name.Substring(1) : root.Name;
        return prefix + rootName + "Id";
    }

    // 'Room Room' or 'Room? Room': the member holds the root itself, not a collection or an envelope of it.
    private static bool IsDirect(ITypeSymbol memberType, INamedTypeSymbol root)
    {
        if (memberType is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            memberType = nullable.TypeArguments[0];

        return SymbolEqualityComparer.Default.Equals(memberType, root);
    }

    // The type an instance property or field holds; null for what is not state: static members, indexers, constants,
    // and the backing fields of auto-properties, read through their property.
    private static ITypeSymbol? StateType(ISymbol member) =>
        member switch
        {
            IPropertySymbol { IsStatic: false, IsIndexer: false } property => property.Type,
            IFieldSymbol { IsStatic: false, IsConst: false, IsImplicitlyDeclared: false } field => field.Type,
            _ => null
        };

    // The named types a type holds: itself, the elements of an array and the type arguments of a constructed generic type,
    // those of its containing types included. Type parameters, pointers and dynamic hold none.
    private static IEnumerable<INamedTypeSymbol> ContainedTypes(ITypeSymbol type)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                foreach (var contained in ContainedTypes(array.ElementType))
                    yield return contained;

                break;

            case INamedTypeSymbol { TypeKind: not TypeKind.Error } named:
                yield return named;
                for (var current = named; current is not null; current = current.ContainingType)
                {
                    foreach (var argument in current.TypeArguments)
                    {
                        foreach (var contained in ContainedTypes(argument))
                            yield return contained;
                    }
                }

                break;
        }
    }

    private static bool IsStateful(INamedTypeSymbol type) => type.TypeKind is TypeKind.Class or TypeKind.Struct;

    // "'Booking'", "'Booking' and 'Invoice'", "'Booking', 'Invoice' and 'Quote'".
    private static string List(IEnumerable<string> names)
    {
        var quoted = names.Select(name => $"'{name}'").ToArray();
        return quoted.Length == 1 ? quoted[0] : $"{string.Join(", ", quoted.Take(quoted.Length - 1))} and {quoted[quoted.Length - 1]}";
    }

    private static string Name(INamedTypeSymbol type) => type.ToDisplayString(DisplayFormat);

    // The possible owners of each child entity of the compilation: the roots declared here that reach its type through
    // their members, directly or through other child entities. A root reached stops the walk: its children are its own.
    // The walk reads types, not instances: two roots can own distinct instances of the same type of child.
    private sealed class Ownership
    {
        private readonly ImmutableDictionary<INamedTypeSymbol, ImmutableArray<INamedTypeSymbol>> _owners;

        private Ownership(ImmutableDictionary<INamedTypeSymbol, ImmutableArray<INamedTypeSymbol>> owners) => _owners = owners;

        // Ordered by name, for a stable message.
        public ImmutableArray<INamedTypeSymbol> Of(INamedTypeSymbol child) =>
            _owners.TryGetValue(child.OriginalDefinition, out var owners) ? owners : ImmutableArray<INamedTypeSymbol>.Empty;

        public static Ownership Create(Compilation compilation, DomainModel model)
        {
            var owners = new Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>(SymbolEqualityComparer.Default);
            foreach (var root in Types(compilation.Assembly.GlobalNamespace).Where(type => IsStateful(type) && model.IsAggregateRoot(type)))
            {
                var reached = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                var pending = new Stack<INamedTypeSymbol>();
                pending.Push(root);
                while (pending.Count > 0)
                {
                    foreach (var child in ReachedChildren(pending.Pop(), model))
                    {
                        if (!reached.Add(child))
                            continue;

                        if (!owners.TryGetValue(child, out var childOwners))
                            owners.Add(child, childOwners = []);

                        childOwners.Add(root);
                        pending.Push(child);
                    }
                }
            }

            var ordered = ImmutableDictionary.CreateBuilder<INamedTypeSymbol, ImmutableArray<INamedTypeSymbol>>(SymbolEqualityComparer.Default);
            foreach (var pair in owners)
                ordered.Add(pair.Key, pair.Value.OrderBy(Name, StringComparer.Ordinal).ToImmutableArray());

            return new Ownership(ordered.ToImmutable());
        }

        // The child entities the instance members of a type hold, those inherited from its base classes included.
        private static IEnumerable<INamedTypeSymbol> ReachedChildren(INamedTypeSymbol type, DomainModel model)
        {
            for (var current = type; current is not null; current = current.BaseType)
            {
                foreach (var member in current.GetMembers())
                {
                    if (StateType(member) is not { } memberType)
                        continue;

                    foreach (var contained in ContainedTypes(memberType))
                    {
                        if (IsStateful(contained) && model.IsEntity(contained) && !model.IsAggregateRoot(contained))
                            yield return contained.OriginalDefinition;
                    }
                }
            }
        }

        private static IEnumerable<INamedTypeSymbol> Types(INamespaceSymbol @namespace)
        {
            foreach (var member in @namespace.GetMembers())
            {
                var types = member is INamespaceSymbol nested ? Types(nested) : Nested((INamedTypeSymbol)member);
                foreach (var type in types)
                    yield return type;
            }
        }

        private static IEnumerable<INamedTypeSymbol> Nested(INamedTypeSymbol type)
        {
            yield return type;
            foreach (var nested in type.GetTypeMembers().SelectMany(Nested))
                yield return nested;
        }
    }
}
