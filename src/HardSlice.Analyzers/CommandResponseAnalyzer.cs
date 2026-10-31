using System.Collections.Immutable;
using HardSlice.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace HardSlice.Analyzers;

// HS005: a command handler never returns an entity, root or child, directly, in an envelope, a collection, an array, a
// tuple or a DTO. A command handler is a concrete type that implements the requestHandler contract of hardslice.json,
// directly or through a base class, for a request that implements the command contract; its TResponse is examined by
// EntityContainment. Generated code is analyzed. Not applied to test projects.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CommandResponseAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "HS005";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "A command handler returns an entity",
        messageFormat: "handler '{0}' of command '{1}' returns {2}. Command handlers return an identifier, a DTO of values or nothing. Fix: {3}. Skill: fix-architecture-violation. Doc: 'docs/rules/HS005.md'.",
        category: "HardSlice.Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly SymbolDisplayFormat DisplayFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        // An auto-generated header must not exempt a handler from the rule.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (RuleActivation.IsTestProject(start.Options))
                return;

            // Without usable contracts, HS900 reports the configuration. The domain sees neither the command nor the handler contract.
            var model = DomainModel.Create(start.Compilation, HardSliceConfiguration.Read(start.Options, start.CancellationToken));
            if (model.Contract(ContractKeys.Command) is not { } command
                || model.Contract(ContractKeys.RequestHandler) is not { } handler
                || EntityContainment.Create(start.Compilation, model) is not { } containment)
            {
                return;
            }

            start.RegisterSymbolAction(symbolContext => Analyze(symbolContext, model, command, handler, containment), SymbolKind.NamedType);
        });
    }

    private static void Analyze(SymbolAnalysisContext context, DomainModel model, INamedTypeSymbol command, INamedTypeSymbol handlerContract, EntityContainment containment)
    {
        var handler = (INamedTypeSymbol)context.Symbol;
        // An abstract base handler is reported through the concrete handlers derived from it, with their own type arguments.
        if (handler.TypeKind is not (TypeKind.Class or TypeKind.Struct) || handler.IsAbstract || handler.IsStatic)
            return;

        foreach (var implementation in DomainModel.Implementations(handler, handlerContract))
        {
            if (implementation.TypeArguments.Length != 2
                || !IsCommand(implementation.TypeArguments[0], model, command)
                || containment.Find(implementation.TypeArguments[1]) is not { } path)
            {
                continue;
            }

            var request = implementation.TypeArguments[0];
            var response = implementation.TypeArguments[1];
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                handler.Locations.FirstOrDefault(location => location.IsInSource) ?? Location.None,
                handler.ToDisplayString(DisplayFormat),
                request.ToDisplayString(DisplayFormat),
                Describe(response, path),
                Fix(path.Entity)));
        }
    }

    // A command, or a type parameter constrained to one: 'Handler<TCommand> : IRequestHandler<TCommand, Booking> where TCommand : ICommand<Booking>'.
    private static bool IsCommand(ITypeSymbol request, DomainModel model, INamedTypeSymbol command) =>
        request is ITypeParameterSymbol parameter
            ? parameter.ConstraintTypes.Any(constraint => model.IsCommand(constraint) || SymbolEqualityComparer.Default.Equals(constraint.OriginalDefinition, command))
            : model.IsCommand(request);

    // "the entity 'Booking'", "'List<Booking>', which holds the entity 'Booking'" or
    // "'ConfirmationDto', whose property 'Booking' holds the entity 'Booking'".
    private static string Describe(ITypeSymbol response, EntityPath path)
    {
        var entity = $"the entity {Name(path.Entity)}";
        if (path.Members.IsEmpty)
        {
            return SymbolEqualityComparer.Default.Equals(response, path.Entity)
                ? entity
                : $"'{response.ToDisplayString(DisplayFormat)}', which holds {entity}";
        }

        // A DTO reached through an envelope is named: 'List<ConfirmationDto>', whose property 'ConfirmationDto.Booking'.
        var first = path.Members[0].ContainingType;
        var names = path.Members.Select(member => member.Name);
        var memberPath = string.Join(
            ".",
            SymbolEqualityComparer.Default.Equals(first, response) ? names : names.Prepend(first.ToDisplayString(DisplayFormat)));
        var kind = path.Members[path.Members.Length - 1] is IFieldSymbol ? "field" : "property";
        return $"'{response.ToDisplayString(DisplayFormat)}', whose {kind} '{memberPath}' holds {entity}";
    }

    // "return 'BookingId', or a DTO built from the entity's values": the type of the public Id of the entity, when it has one.
    private static string Fix(ITypeSymbol entity)
    {
        var identifier = Identifier(entity) is { } type ? $"'{type.ToDisplayString(DisplayFormat)}'" : $"the identifier of {Name(entity)}";
        return $"return {identifier}, or a DTO built from the entity's values";
    }

    // Declared by the entity, a base class or an interface; for a type parameter, by its constraints.
    private static ITypeSymbol? Identifier(ITypeSymbol entity)
    {
        var types = entity is ITypeParameterSymbol parameter ? parameter.ConstraintTypes.OfType<INamedTypeSymbol>() : new[] { (INamedTypeSymbol)entity };
        foreach (var type in types)
        {
            var declaring = new List<INamedTypeSymbol>();
            for (var current = type; current is not null; current = current.BaseType)
                declaring.Add(current);

            foreach (var candidate in declaring.Concat(type.AllInterfaces))
            {
                if (candidate.GetMembers("Id").OfType<IPropertySymbol>().FirstOrDefault(property => !property.IsStatic) is { } id)
                    return id.Type;
            }
        }

        return null;
    }

    // 'Booking', even when the response declares 'Booking?'.
    private static string Name(ITypeSymbol entity) => $"'{entity.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString(DisplayFormat)}'";
}
