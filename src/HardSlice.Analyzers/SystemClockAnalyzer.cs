using System.Collections.Immutable;
using HardSlice.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace HardSlice.Analyzers;

// HS010: the domain and the slices never read the system clock. The clock properties of DateTime and DateTimeOffset are
// recognized by their symbols. The domain is the assembly that declares the entity contract of hardslice.json: all its code
// is checked, in its own compilation. In any other compilation, only the code of its slices is (SliceMap). A slice also never
// waits on the system clock: 'Task.Delay' and 'new PeriodicTimer' without a TimeProvider argument are reported there, recognized
// by their symbols too. Not applied to test projects.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SystemClockAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "HS010";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "The domain or a slice reads the system clock",
        messageFormat: "{0} reads '{1}'. The domain and the slices never read the system clock. Fix: {2}. Skill: fix-architecture-violation. Doc: 'docs/rules/HS010.md'.",
        category: "HardSlice.Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: RuleSheet.Uri(DiagnosticId));

    private static readonly DiagnosticDescriptor TimerRule = new(
        DiagnosticId,
        title: "The domain or a slice reads the system clock",
        messageFormat: "{0} waits on the system clock with '{1}'. A slice waits on an injected TimeProvider, which tests advance. Fix: {2}. Skill: fix-architecture-violation. Doc: 'docs/rules/HS010.md'.",
        category: "HardSlice.Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: RuleSheet.Uri(DiagnosticId));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule, TimerRule);

    public override void Initialize(AnalysisContext context)
    {
        // An auto-generated header must not exempt code from the rule.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (RuleActivation.IsTestProject(start.Options))
                return;

            var compilation = start.Compilation;
            var clock = ClockProperties.Create(compilation);
            // Without a usable entity contract, HS900 reports the configuration and the domain cannot be recognized.
            var isDomain = DomainModel.For(compilation, start.Options, start.CancellationToken)
                .Contract(ContractKeys.Entity) is { } entity
                && SymbolEqualityComparer.Default.Equals(entity.ContainingAssembly, compilation.Assembly);
            var slices = new SliceMap(compilation);
            var expression = compilation.GetTypeByMetadataName("System.Linq.Expressions.Expression`1");

            start.RegisterOperationAction(
                operationContext => Analyze(operationContext, clock, isDomain, slices, expression),
                OperationKind.PropertyReference);

            if (SystemTimers.Create(compilation) is { } timers)
            {
                start.RegisterOperationAction(
                    operationContext => AnalyzeTimer(operationContext, timers, slices),
                    OperationKind.Invocation,
                    OperationKind.ObjectCreation);
            }
        });
    }

    private static void Analyze(OperationAnalysisContext context, ClockProperties clock, bool isDomain, SliceMap slices, INamedTypeSymbol? expression)
    {
        var property = ((IPropertyReferenceOperation)context.Operation).Property;
        if (clock.FixOf(property) is not { } call || ContainingMember.IsWithinNameOf(context.Operation))
            return;

        string where;
        string fix;
        if (isDomain)
        {
            where = $"in domain project '{context.Compilation.AssemblyName}'";
            fix = $"take the instant as a parameter; the handler reads it from an injected TimeProvider with {call}";
        }
        else if (slices.SliceOf(context.ContainingSymbol) is { } slice)
        {
            where = $"in slice '{slice}'";
            fix = IsInExpressionTree(context.Operation, expression)
                ? $"read the instant once before the query, from an injected TimeProvider with {call}, and use that variable in the expression"
                : $"inject TimeProvider and call {call}, or pass the instant to the domain as a parameter";
        }
        else
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            context.Operation.Syntax is MemberAccessExpressionSyntax memberAccess ? memberAccess.Name.GetLocation() : context.Operation.Syntax.GetLocation(),
            $"{ContainingMember.Describe(context.ContainingSymbol)} {where}",
            property.ToDisplayString(ContainingMember.DisplayFormat),
            fix));
    }

    // 'Task.Delay(TimeSpan)' or 'new PeriodicTimer(TimeSpan)' in a slice: the overload that takes a TimeProvider replaces it.
    private static void AnalyzeTimer(OperationAnalysisContext context, SystemTimers timers, SliceMap slices)
    {
        var method = context.Operation switch
        {
            IInvocationOperation invocation => invocation.TargetMethod,
            IObjectCreationOperation creation => creation.Constructor,
            _ => null
        };
        if (method is null || timers.FixOf(method) is not { } fix || slices.SliceOf(context.ContainingSymbol) is not { } slice)
            return;

        var location = context.Operation.Syntax switch
        {
            InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess } => memberAccess.Name.GetLocation(),
            BaseObjectCreationExpressionSyntax creation => creation.NewKeyword.GetLocation(),
            var syntax => syntax.GetLocation()
        };
        context.ReportDiagnostic(Diagnostic.Create(
            TimerRule,
            location,
            $"{ContainingMember.Describe(context.ContainingSymbol)} in slice '{slice}'",
            method.MethodKind == MethodKind.Constructor
                ? $"new {method.ContainingType.Name}({string.Join(", ", method.Parameters.Select(parameter => parameter.Type.ToDisplayString(TimerFormat)))})"
                : method.ToDisplayString(TimerFormat),
            fix));
    }

    // 'Task.Delay(TimeSpan, CancellationToken)'.
    private static readonly SymbolDisplayFormat TimerFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        memberOptions: SymbolDisplayMemberOptions.IncludeContainingType | SymbolDisplayMemberOptions.IncludeParameters,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    // 'db.Set<Booking>().Where(booking => booking.End < DateTimeOffset.UtcNow)': the lambda is converted to Expression<TDelegate>.
    private static bool IsInExpressionTree(IOperation operation, INamedTypeSymbol? expression)
    {
        if (expression is null)
            return false;

        for (var current = operation.Parent; current is not null; current = current.Parent)
        {
            if (current is IAnonymousFunctionOperation function
                && operation.SemanticModel?.GetTypeInfo(function.Syntax).ConvertedType is INamedTypeSymbol converted
                && SymbolEqualityComparer.Default.Equals(converted.OriginalDefinition, expression))
            {
                return true;
            }
        }

        return false;
    }

    // The methods that wait on the system clock, with the call that replaces each: every overload of Task.Delay and of the
    // constructor of PeriodicTimer that takes no TimeProvider. Null when the compilation knows no TimeProvider, before .NET 8.
    private sealed class SystemTimers(ImmutableDictionary<ISymbol, string> fixes)
    {
        public static SystemTimers? Create(Compilation compilation)
        {
            var timeProvider = compilation.GetTypeByMetadataName("System.TimeProvider");
            if (timeProvider is null || timeProvider.TypeKind == TypeKind.Error)
                return null;

            var fixes = ImmutableDictionary.CreateBuilder<ISymbol, string>(SymbolEqualityComparer.Default);
            Add(compilation.GetTypeByMetadataName("System.Threading.Tasks.Task")?.GetMembers("Delay").OfType<IMethodSymbol>().Where(method => method.IsStatic),
                "inject TimeProvider and pass it: Task.Delay(delay, timeProvider, cancellationToken)");
            Add(compilation.GetTypeByMetadataName("System.Threading.PeriodicTimer")?.InstanceConstructors,
                "inject TimeProvider and pass it: new PeriodicTimer(period, timeProvider); for a command sent at regular intervals, register it with AddRecurringCommand in its module instead");
            return fixes.Count == 0 ? null : new SystemTimers(fixes.ToImmutable());

            void Add(IEnumerable<IMethodSymbol>? methods, string fix)
            {
                foreach (var method in methods ?? [])
                {
                    if (!method.Parameters.Any(parameter => SymbolEqualityComparer.Default.Equals(parameter.Type, timeProvider)))
                        fixes[method] = fix;
                }
            }
        }

        public string? FixOf(IMethodSymbol method) => fixes.TryGetValue(method.OriginalDefinition, out var fix) ? fix : null;
    }

    // The properties that read the system clock, with the TimeProvider call that replaces each.
    private sealed class ClockProperties(ImmutableDictionary<ISymbol, string> fixes)
    {
        public static ClockProperties Create(Compilation compilation)
        {
            var fixes = ImmutableDictionary.CreateBuilder<ISymbol, string>(SymbolEqualityComparer.Default);
            Add(compilation.GetSpecialType(SpecialType.System_DateTime), ("Now", "GetLocalNow().DateTime"), ("UtcNow", "GetUtcNow().UtcDateTime"), ("Today", "GetLocalNow().Date"));
            Add(compilation.GetTypeByMetadataName("System.DateTimeOffset"), ("Now", "GetLocalNow()"), ("UtcNow", "GetUtcNow()"));
            return new ClockProperties(fixes.ToImmutable());

            void Add(INamedTypeSymbol? type, params (string Name, string Fix)[] properties)
            {
                if (type is null || type.TypeKind == TypeKind.Error)
                    return;

                foreach (var (name, fix) in properties)
                {
                    foreach (var property in type.GetMembers(name).OfType<IPropertySymbol>().Where(property => property.IsStatic))
                        fixes[property] = fix;
                }
            }
        }

        // 'GetUtcNow()' for 'DateTimeOffset.UtcNow', null for any other property.
        public string? FixOf(IPropertySymbol property) => fixes.TryGetValue(property, out var fix) ? fix : null;
    }
}
