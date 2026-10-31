using System.Collections.Immutable;
using HardSlice.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace HardSlice.Analyzers;

// HS009: a domain event is a record. A domain event is a class or a struct that implements the domainEvent contract of
// hardslice.json, directly, through a base class or through a derived interface. In the compilation that declares it, in the
// domain or anywhere else, the rule reports a class or a struct that is not a record. It checks the shape, not immutability:
// a record with a public setter is accepted. A type that is also an entity is left to HS901, which asks for one role.
// Generated code is analyzed. Not applied to test projects.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DomainEventRecordAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "HS009";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "A domain event is not a record",
        messageFormat: "domain event '{0}' is a {1}. Domain events are records. Fix: declare it as '{2}'. Skill: add-domain-event. Doc: 'docs/rules/HS009.md'.",
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
        // An auto-generated header must not exempt an event from the rule.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (RuleActivation.IsTestProject(start.Options))
                return;

            // Without a usable domainEvent contract, HS900 reports the configuration and no event can be recognized.
            var model = DomainModel.Create(start.Compilation, HardSliceConfiguration.Read(start.Options, start.CancellationToken));
            if (model.Contract(ContractKeys.DomainEvent) is null)
                return;

            start.RegisterSymbolAction(symbolContext => Analyze(symbolContext, model), SymbolKind.NamedType);
        });
    }

    private static void Analyze(SymbolAnalysisContext context, DomainModel model)
    {
        var domainEvent = (INamedTypeSymbol)context.Symbol;
        // A derived interface such as IBookingEvent only groups events: it is not one.
        if (domainEvent.TypeKind is not (TypeKind.Class or TypeKind.Struct)
            || domainEvent.IsRecord
            || !model.IsDomainEvent(domainEvent)
            || model.IsEntity(domainEvent)
            || model.IsAggregateRoot(domainEvent))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            domainEvent.Locations.FirstOrDefault(location => location.IsInSource) ?? Location.None,
            domainEvent.ToDisplayString(DisplayFormat),
            domainEvent.TypeKind == TypeKind.Struct ? "struct" : "class",
            RecordDeclaration(domainEvent)));
    }

    // The declaration of the type as a record, with its accessibility, its modifiers and its base list:
    // 'public sealed record BookingConfirmed(...) : IDomainEvent'.
    private static string RecordDeclaration(INamedTypeSymbol domainEvent)
    {
        var modifiers = new List<string>();
        if (AccessibilityKeyword(domainEvent.DeclaredAccessibility) is { } accessibility)
            modifiers.Add(accessibility);

        if (domainEvent.TypeKind == TypeKind.Struct)
        {
            if (domainEvent.IsReadOnly)
                modifiers.Add("readonly");
        }
        else if (domainEvent.IsAbstract)
        {
            modifiers.Add("abstract");
        }
        else if (domainEvent.IsSealed)
        {
            modifiers.Add("sealed");
        }

        if (IsPartial(domainEvent))
            modifiers.Add("partial");

        modifiers.Add(domainEvent.TypeKind == TypeKind.Struct ? "record struct" : "record");

        var name = domainEvent.TypeParameters.IsEmpty
            ? domainEvent.Name
            : $"{domainEvent.Name}<{string.Join(", ", domainEvent.TypeParameters.Select(parameter => parameter.Name))}>";

        var bases = new List<string>();
        if (domainEvent.TypeKind == TypeKind.Class && domainEvent.BaseType is { SpecialType: not SpecialType.System_Object } baseType)
            bases.Add(baseType.ToDisplayString(DisplayFormat));

        bases.AddRange(domainEvent.Interfaces.Select(@interface => @interface.ToDisplayString(DisplayFormat)));

        return $"{string.Join(" ", modifiers)} {name}(...){(bases.Count == 0 ? "" : " : " + string.Join(", ", bases))}";
    }

    private static string? AccessibilityKeyword(Accessibility accessibility) =>
        accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            Accessibility.Private => "private",
            _ => null
        };

    // Every declaration of a partial record says 'record': the fix keeps 'partial'.
    private static bool IsPartial(INamedTypeSymbol type) =>
        type.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax())
            .OfType<TypeDeclarationSyntax>()
            .Any(declaration => declaration.Modifiers.Any(SyntaxKind.PartialKeyword));
}
