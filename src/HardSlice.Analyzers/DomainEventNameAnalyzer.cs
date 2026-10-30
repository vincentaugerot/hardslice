using System.Collections.Immutable;
using HardSlice.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace HardSlice.Analyzers;

// HS019: a domain event is named as a past fact. A naming convention, never blocking: a suggestion in the All analysis mode
// only, off by default. The last word of the name in PascalCase ends with 'ed' or is one of a fixed list of irregular past
// participles, published in the sheet; a version suffix such as 'V2' is ignored. It proves nothing about the meaning of the
// name: 'RoomSeed' passes. Domain events are recognized as HS009 recognizes them, in the compilation that declares them; an
// abstract event or a derived interface names a family of events, not a fact, and is not checked. Generated code is analyzed.
// Not applied to test projects.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DomainEventNameAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "HS019";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "A domain event is not named as a past fact",
        messageFormat: "domain event '{0}' is not named as a past fact. Domain events are named in the past tense. Fix: rename it, for example to 'BookingConfirmed'. Doc: 'docs/rules/HS019.md'.",
        category: "HardSlice.Naming",
        defaultSeverity: DiagnosticSeverity.Info,
        // Off by default, as in the Recommended mode: the All mode turns it on as a suggestion. HS900 knows it from OffByDefault.
        isEnabledByDefault: false);

    // Irregular past participles that do not end with 'ed', as the sheet lists them. Compared without case.
    private static readonly ImmutableHashSet<string> IrregularPastParticiples = new[]
    {
        "Arisen", "Awoken", "Begun", "Bent", "Bitten", "Blown", "Bought", "Bound", "Broadcast", "Broken", "Brought", "Built",
        "Burnt", "Caught", "Chosen", "Dealt", "Done", "Drawn", "Driven", "Dug", "Eaten", "Fallen", "Felt", "Flown", "Forbidden",
        "Forgiven", "Forgotten", "Fought", "Found", "Frozen", "Given", "Gone", "Got", "Gotten", "Grown", "Held", "Hidden", "Hung",
        "Kept", "Known", "Laid", "Left", "Lent", "Lit", "Lost", "Made", "Meant", "Met", "Mistaken", "Overdrawn", "Overpaid",
        "Overridden", "Overtaken", "Paid", "Prepaid", "Quit", "Read", "Rebuilt", "Redone", "Remade", "Repaid", "Reset", "Resent",
        "Resold", "Retaken", "Rewritten", "Ridden", "Risen", "Rung", "Said", "Seen", "Sent", "Set", "Shaken", "Shot", "Shown",
        "Shut", "Slept", "Sold", "Sought", "Spent", "Split", "Spoken", "Spread", "Spun", "Stolen", "Stood", "Struck", "Stuck",
        "Sung", "Sunk", "Swept", "Sworn", "Taken", "Taught", "Thrown", "Told", "Torn", "Undergone", "Underpaid", "Undertaken",
        "Understood", "Undone", "Upheld", "Withdrawn", "Withheld", "Woken", "Won", "Worn", "Wound", "Written"
    }.ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);

    private static readonly SymbolDisplayFormat DisplayFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
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
        // A type that is also an entity is left to HS901, which asks for one role.
        if (domainEvent.TypeKind is not (TypeKind.Class or TypeKind.Struct)
            || domainEvent.IsAbstract
            || !model.IsDomainEvent(domainEvent)
            || model.IsEntity(domainEvent)
            || model.IsAggregateRoot(domainEvent)
            || IsNamedAsAPastFact(domainEvent.Name))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            domainEvent.Locations.FirstOrDefault(location => location.IsInSource) ?? Location.None,
            domainEvent.ToDisplayString(DisplayFormat)));
    }

    internal static bool IsNamedAsAPastFact(string name)
    {
        var word = LastWord(name);
        return word.EndsWith("ed", StringComparison.OrdinalIgnoreCase) || IrregularPastParticiples.Contains(word);
    }

    // 'BookingConfirmed' -> 'Confirmed', 'RefundPAID' -> 'PAID', 'BookingConfirmedV2' -> 'Confirmed', 'Booking_Paid' -> 'Paid'.
    private static string LastWord(string name)
    {
        var end = WithoutVersion(name);
        var start = end - 1;
        while (start > 0 && !StartsAWord(name, start))
            start--;

        return start < 0 ? "" : name.Substring(start, end - start);
    }

    // The length of the name without its trailing underscores and its version, such as 'V2' or '2': the version of an
    // event is not a word of its name. A name made of a version only keeps it.
    private static int WithoutVersion(string name)
    {
        var end = TrimUnderscores(name, name.Length);
        var version = end;
        while (version > 0 && char.IsDigit(name[version - 1]))
            version--;

        if (version == end || version == 0)
            return end;

        if (version > 1 && name[version - 1] is 'V' or 'v')
            version--;

        return TrimUnderscores(name, version) is > 0 and var trimmed ? trimmed : end;
    }

    private static int TrimUnderscores(string name, int end)
    {
        while (end > 0 && name[end - 1] == '_')
            end--;

        return end;
    }

    // An upper-case letter after a lower-case letter or a digit, the last capital of an acronym before a lower-case
    // letter ('HTTPRequested' -> 'HTTP', 'Requested'), or any character after an underscore.
    private static bool StartsAWord(string name, int index)
    {
        var previous = name[index - 1];
        var current = name[index];
        if (previous == '_')
            return true;

        if (!char.IsUpper(current))
            return false;

        return char.IsLower(previous)
            || char.IsDigit(previous)
            || (char.IsUpper(previous) && index + 1 < name.Length && char.IsLower(name[index + 1]));
    }
}
