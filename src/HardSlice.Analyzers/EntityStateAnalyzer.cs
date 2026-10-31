using System.Collections.Immutable;
using HardSlice.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace HardSlice.Analyzers;

// HS007: the state of an entity is never written from outside its assembly. An entity is a class, a struct or a record that
// implements the entity contract of hardslice.json, directly or through a base class, aggregate roots included. In the
// compilation that declares it, the rule reports an instance property whose set or init accessor is public, a public instance
// field that is not readonly, and a setter that implements a public interface, explicitly or implicitly. Accessibility is
// effective: a public accessor of an internal type is not reachable from another assembly. The members inherited from a base
// class are reported on the entity, unless that base is itself an entity of the compilation, which reports them. Generated code
// is analyzed. Not applied to test projects.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EntityStateAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "HS007";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "The state of an entity can be written from outside",
        messageFormat: "entity '{0}' {1}. The state of an entity changes only through its methods, which protect its invariants. Fix: {2}. Skill: extract-to-domain. Doc: 'docs/rules/HS007.md'.",
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

            // Without a usable entity contract, HS900 reports the configuration and no entity can be recognized.
            var model = DomainModel.Create(start.Compilation, HardSliceConfiguration.Read(start.Options, start.CancellationToken));
            if (model.Contract(ContractKeys.Entity) is null)
                return;

            start.RegisterSymbolAction(symbolContext => Analyze(symbolContext, model), SymbolKind.NamedType);
        });
    }

    private static void Analyze(SymbolAnalysisContext context, DomainModel model)
    {
        var entity = (INamedTypeSymbol)context.Symbol;
        // IAggregateRoot implements IEntity, but an interface holds no state.
        if (entity.TypeKind is not (TypeKind.Class or TypeKind.Struct) || !model.IsEntity(entity))
            return;

        var overridden = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        INamedTypeSymbol? entityBase = null;
        for (var type = entity; type is not null; type = type.BaseType)
        {
            // A base entity of this compilation reports its own members and those it inherits.
            if (!ReferenceEquals(type, entity)
                && model.IsEntity(type)
                && SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, context.Compilation.Assembly))
            {
                entityBase = type;
                break;
            }

            foreach (var member in type.GetMembers())
            {
                if (member.IsStatic || overridden.Contains(member.OriginalDefinition))
                    continue;

                switch (member)
                {
                    case IPropertySymbol property:
                        for (var overriddenProperty = property.OverriddenProperty; overriddenProperty is not null; overriddenProperty = overriddenProperty.OverriddenProperty)
                            overridden.Add(overriddenProperty.OriginalDefinition);

                        if (property.SetMethod is { } setter && IsPublic(setter))
                            Report(context, entity, type, property, setter);

                        break;

                    case IFieldSymbol { IsConst: false, IsReadOnly: false, IsImplicitlyDeclared: false } field when IsPublic(field):
                        Report(context, entity, type, field);
                        break;
                }
            }
        }

        ReportInterfaceSetters(context, entity, entityBase);
    }

    // '((IEditableBooking)booking).Status = ...' writes the state without a domain method. A public implementation is already
    // reported as a public setter, here or by a base entity; a default implementation in the interface writes no state.
    private static void ReportInterfaceSetters(SymbolAnalysisContext context, INamedTypeSymbol entity, INamedTypeSymbol? entityBase)
    {
        foreach (var @interface in entity.AllInterfaces)
        {
            if (!IsPublic(@interface) || entityBase?.AllInterfaces.Contains(@interface, SymbolEqualityComparer.Default) == true)
                continue;

            foreach (var property in @interface.GetMembers().OfType<IPropertySymbol>())
            {
                if (property.IsStatic
                    || property.SetMethod is not { DeclaredAccessibility: Accessibility.Public } interfaceSetter
                    || entity.FindImplementationForInterfaceMember(interfaceSetter) is not IMethodSymbol implementation
                    || implementation.ContainingType.TypeKind == TypeKind.Interface
                    || IsPublic(implementation))
                {
                    continue;
                }

                var location = SymbolEqualityComparer.Default.Equals(implementation.ContainingType, entity) ? AccessorLocation(implementation) : null;
                context.ReportDiagnostic(Diagnostic.Create(
                    Rule,
                    location ?? EntityLocation(entity),
                    Name(entity),
                    $"lets '{@interface.ToDisplayString(DisplayFormat)}.{property.Name}' be set from outside",
                    $"remove the setter from the public interface and change the state in a domain method{Example(entity)}"));
            }
        }
    }

    private static void Report(SymbolAnalysisContext context, INamedTypeSymbol entity, INamedTypeSymbol declaringType, IPropertySymbol property, IMethodSymbol setter)
    {
        var accessor = setter.IsInitOnly ? "init accessor" : "setter";
        var name = PropertyName(property);
        string exposure;
        string fix;
        Location? location = null;
        if (!ReferenceEquals(declaringType, entity))
        {
            var declaring = declaringType.OriginalDefinition.ToDisplayString(DisplayFormat);
            exposure = $"exposes '{name}', inherited from '{declaring}', with a public {accessor}";
            fix = $"make the {accessor} of '{declaring}.{name}' private or protected and change the state in a domain method{Example(entity)}";
        }
        else if (AccessorLocation(setter) is { } accessorLocation)
        {
            location = accessorLocation;
            exposure = $"exposes '{name}' with a public {accessor}";
            fix = setter.IsInitOnly
                ? $"make the init accessor private and set the value in a constructor or a domain method{Example(entity)}"
                : $"make the setter private and change the state in a domain method{Example(entity)}";
        }
        else
        {
            // A positional parameter of a record declares a property with a public init accessor, or a public setter in a record struct.
            location = property.Locations.FirstOrDefault(candidate => candidate.IsInSource);
            exposure = $"exposes '{name}', declared by a positional parameter, with a public {accessor}";
            fix = $"declare '{name}' in the body of the record with a private setter, initialized from the parameter, and change the state in a domain method{Example(entity)}";
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, location ?? EntityLocation(entity), Name(entity), exposure, fix));
    }

    private static void Report(SymbolAnalysisContext context, INamedTypeSymbol entity, INamedTypeSymbol declaringType, IFieldSymbol field)
    {
        string exposure;
        string fix;
        Location? location = null;
        if (!ReferenceEquals(declaringType, entity))
        {
            var declaring = declaringType.OriginalDefinition.ToDisplayString(DisplayFormat);
            exposure = $"exposes '{field.Name}', inherited from '{declaring}', as a public field that is not readonly";
            fix = $"make the field '{declaring}.{field.Name}' private or protected and change the state in a domain method{Example(entity)}";
        }
        else
        {
            location = field.Locations.FirstOrDefault(candidate => candidate.IsInSource);
            exposure = $"exposes '{field.Name}' as a public field that is not readonly";
            fix = $"make the field private and change the state in a domain method{Example(entity)}";
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, location ?? EntityLocation(entity), Name(entity), exposure, fix));
    }

    // Reachable from another assembly: the member and every type that contains it are public. Protected and internal are not.
    private static bool IsPublic(ISymbol symbol)
    {
        for (var current = symbol; current is not null and not INamespaceSymbol; current = current.ContainingSymbol)
        {
            if (current.DeclaredAccessibility != Accessibility.Public)
                return false;
        }

        return true;
    }

    // The 'set' or 'init' keyword of an accessor written in source; null for the accessor of a positional parameter.
    private static Location? AccessorLocation(IMethodSymbol accessor) =>
        accessor.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax()).OfType<AccessorDeclarationSyntax>().FirstOrDefault() is { } syntax
            ? syntax.Keyword.GetLocation()
            : null;

    private static Location EntityLocation(INamedTypeSymbol entity) => entity.Locations.FirstOrDefault(location => location.IsInSource) ?? Location.None;

    // ", such as 'Booking.Cancel()'": the first public instance method the entity declares, when it has one.
    private static string Example(INamedTypeSymbol entity)
    {
        var method = entity.GetMembers().OfType<IMethodSymbol>().FirstOrDefault(candidate =>
            candidate is { MethodKind: MethodKind.Ordinary, IsStatic: false, IsOverride: false, IsImplicitlyDeclared: false, DeclaredAccessibility: Accessibility.Public });
        return method is null ? "" : $", such as '{Name(entity)}.{method.Name}({(method.Parameters.IsEmpty ? "" : "...")})'";
    }

    private static string PropertyName(IPropertySymbol property) =>
        property.IsIndexer ? $"this[{string.Join(", ", property.Parameters.Select(parameter => parameter.Type.ToDisplayString(DisplayFormat)))}]" : property.Name;

    private static string Name(INamedTypeSymbol entity) => entity.ToDisplayString(DisplayFormat);
}
