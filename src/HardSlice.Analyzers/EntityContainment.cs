using System.Collections.Concurrent;
using System.Collections.Immutable;
using HardSlice.Analyzers.Configuration;
using Microsoft.CodeAnalysis;

namespace HardSlice.Analyzers;

// Whether a type holds an entity, root or child: the type itself, the type arguments of a constructed generic type
// (Nullable<T>, List<T>, Dictionary<TKey, TValue>, Result<T>, tuples), the elements of an array, and the public instance
// properties and fields of a DTO. A DTO can hold an entity only when it is declared in the domain, in the analyzed
// compilation or in an assembly that references the domain: the types of the framework and of the libraries are not
// followed. The contracts themselves count as entities: a 'TEntity : IEntity' or an 'IAggregateRoot' holds one.
// Shared by the rules on the responses of handlers (HS005) and on what queries read (HS006).
internal sealed class EntityContainment
{
    private readonly DomainModel _model;
    private readonly INamedTypeSymbol _entity;
    private readonly INamedTypeSymbol? _aggregateRoot;
    private readonly IAssemblySymbol _domain;
    private readonly IAssemblySymbol _analyzed;
    private readonly ConcurrentDictionary<IAssemblySymbol, bool> _declaresDtos = new(SymbolEqualityComparer.Default);

    private EntityContainment(DomainModel model, INamedTypeSymbol entity, Compilation compilation)
    {
        _model = model;
        _entity = entity;
        _aggregateRoot = model.Contract(ContractKeys.AggregateRoot);
        _domain = entity.ContainingAssembly;
        _analyzed = compilation.Assembly;
    }

    // Null without a usable entity contract: HS900 reports the configuration, and no entity can be recognized.
    public static EntityContainment? Create(Compilation compilation, DomainModel model) =>
        model.Contract(ContractKeys.Entity) is { } entity ? new EntityContainment(model, entity, compilation) : null;

    // All constructed arguments are examined, but the members of a DTO definition are expanded only once. Every entity
    // introduced by a substitution is already reachable through those arguments: expanding Node<List<T>> again cannot
    // add one. This bounds even expanding cycles such as Node<T>.Next : Node<List<T>>, without a depth limit on DTO chains.
    // The explicit stack keeps a long, finite chain off the call stack. Push in reverse to retain the diagnostic's path.
    public EntityPath? Find(ITypeSymbol type, CancellationToken cancellationToken)
    {
        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var expandedDtos = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var pending = new Stack<(ITypeSymbol Type, ImmutableArray<ISymbol> Members)>();
        pending.Push((type, ImmutableArray<ISymbol>.Empty));
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (currentType, path) = pending.Pop();
            if (!visited.Add(currentType))
                continue;

            switch (currentType)
            {
                case ITypeParameterSymbol parameter:
                    if (IsEntity(parameter, cancellationToken))
                        return new EntityPath(parameter, path);
                    break;

                case IArrayTypeSymbol array:
                    pending.Push((array.ElementType, path));
                    break;

                case INamedTypeSymbol { TypeKind: not TypeKind.Error } named:
                    if (IsEntityOrContract(named))
                        return new EntityPath(named, path);

                    if (IsDto(named) && expandedDtos.Add(named.OriginalDefinition))
                    {
                        var members = named.GetMembers();
                        for (var i = members.Length - 1; i >= 0; i--)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            var member = members[i];
                            var memberType = member switch
                            {
                                IPropertySymbol { IsStatic: false, IsIndexer: false, DeclaredAccessibility: Accessibility.Public } property => property.Type,
                                IFieldSymbol { IsStatic: false, IsImplicitlyDeclared: false, DeclaredAccessibility: Accessibility.Public } field => field.Type,
                                _ => null
                            };
                            if (memberType is not null)
                                pending.Push((memberType, path.Add(member)));
                        }

                        for (var i = named.Interfaces.Length - 1; i >= 0; i--)
                            pending.Push((named.Interfaces[i], path));
                        if (named.BaseType is { } baseType)
                            pending.Push((baseType, path));
                    }

                    var arguments = new List<ITypeSymbol>();
                    for (var current = named; current is not null; current = current.ContainingType)
                        arguments.AddRange(current.TypeArguments);
                    for (var i = arguments.Count - 1; i >= 0; i--)
                        pending.Push((arguments[i], path));
                    break;
            }
        }

        return null;
    }

    // An entity, the entity or aggregate root contract, or a type parameter constrained to one of them: 'TEntity : IEntity'.
    public bool IsEntity(ITypeSymbol type, CancellationToken cancellationToken)
    {
        if (type is not ITypeParameterSymbol)
            return IsEntityOrContract(type);

        var pending = new Stack<ITypeSymbol>();
        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        pending.Push(type);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = pending.Pop();
            if (!visited.Add(current))
                continue;
            if (current is ITypeParameterSymbol parameter)
            {
                foreach (var constraint in parameter.ConstraintTypes)
                    pending.Push(constraint);
            }
            else if (IsEntityOrContract(current))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsEntityOrContract(ITypeSymbol type) =>
        _model.IsEntity(type)
        || SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, _entity)
        || (_aggregateRoot is not null && SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, _aggregateRoot));

    private bool IsDto(INamedTypeSymbol type) =>
        type.TypeKind is TypeKind.Class or TypeKind.Struct or TypeKind.Interface
        && type.SpecialType == SpecialType.None
        && type.ContainingAssembly is { } assembly
        && _declaresDtos.GetOrAdd(assembly, DeclaresDtos);

    private bool DeclaresDtos(IAssemblySymbol assembly) =>
        SymbolEqualityComparer.Default.Equals(assembly, _domain)
        || SymbolEqualityComparer.Default.Equals(assembly, _analyzed)
        || assembly.Modules.Any(module => module.ReferencedAssemblySymbols.Any(referenced => SymbolEqualityComparer.Default.Equals(referenced, _domain)));
}

// The entity reached, and the public members of DTOs followed to reach it, outermost first: empty when the type, an
// element or a type argument is the entity.
internal sealed class EntityPath(ITypeSymbol entity, ImmutableArray<ISymbol> members)
{
    public ITypeSymbol Entity { get; } = entity;

    public ImmutableArray<ISymbol> Members { get; } = members;
}
