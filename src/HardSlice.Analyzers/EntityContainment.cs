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

    // The first entity reached from the type, or null when none is. The walk visits each type once, which also stops cycles.
    public EntityPath? Find(ITypeSymbol type) => Find(type, ImmutableArray<ISymbol>.Empty, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));

    private EntityPath? Find(ITypeSymbol type, ImmutableArray<ISymbol> members, HashSet<ITypeSymbol> visited)
    {
        switch (type)
        {
            case ITypeParameterSymbol parameter:
                // An unconstrained type parameter cannot be analyzed; one constrained to an entity is one.
                return parameter.ConstraintTypes.Any(IsEntity) ? new EntityPath(parameter, members) : null;

            case IArrayTypeSymbol array:
                return Find(array.ElementType, members, visited);

            case INamedTypeSymbol { TypeKind: not TypeKind.Error } named:
                if (IsEntity(named))
                    return new EntityPath(named, members);

                if (!visited.Add(named))
                    return null;

                for (var current = named; current is not null; current = current.ContainingType)
                {
                    foreach (var argument in current.TypeArguments)
                    {
                        if (Find(argument, members, visited) is { } found)
                            return found;
                    }
                }

                return IsDto(named) ? FindInDto(named, members, visited) : null;

            default:
                return null;
        }
    }

    // 'BookingList : List<BookingLine>' holds its elements; 'ConfirmationDto' holds its property 'Booking'.
    private EntityPath? FindInDto(INamedTypeSymbol dto, ImmutableArray<ISymbol> members, HashSet<ITypeSymbol> visited)
    {
        if (dto.BaseType is { } baseType && Find(baseType, members, visited) is { } inBase)
            return inBase;

        foreach (var @interface in dto.Interfaces)
        {
            if (Find(@interface, members, visited) is { } inInterface)
                return inInterface;
        }

        foreach (var member in dto.GetMembers())
        {
            var memberType = member switch
            {
                IPropertySymbol { IsStatic: false, IsIndexer: false, DeclaredAccessibility: Accessibility.Public } property => property.Type,
                IFieldSymbol { IsStatic: false, IsImplicitlyDeclared: false, DeclaredAccessibility: Accessibility.Public } field => field.Type,
                _ => null
            };
            if (memberType is not null && Find(memberType, members.Add(member), visited) is { } inMember)
                return inMember;
        }

        return null;
    }

    private bool IsEntity(ITypeSymbol type) =>
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
