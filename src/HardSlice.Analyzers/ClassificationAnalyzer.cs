using System.Collections.Immutable;
using HardSlice.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace HardSlice.Analyzers;

// HS900 to HS902: the classification of types by their contracts is usable, unambiguous and covers every mapped entity.
// Not applied to test projects.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ClassificationAnalyzer : DiagnosticAnalyzer
{
    public const string ConfigurationId = "HS900";
    public const string AmbiguityId = "HS901";
    public const string UnclassifiedEntityId = "HS902";

    private const string Category = "HardSlice.Configuration";

    // The contracts each rule classifies with. HS900 reports a problem only when an active rule needs one of its contracts.
    // A rule of another analyzer that reads the DomainModel adds its line here.
    private static readonly ImmutableArray<(string RuleId, ImmutableArray<string> Contracts)> ContractsByRule =
    [
        (SliceStructureAnalyzer.DiagnosticId, [ContractKeys.Command, ContractKeys.Query, ContractKeys.RequestHandler]),
        (CommandResponseAnalyzer.DiagnosticId, [ContractKeys.Entity, ContractKeys.Command, ContractKeys.RequestHandler]),
        (QueryHandlerTrackingAnalyzer.DiagnosticId, [ContractKeys.Entity, ContractKeys.Query, ContractKeys.RequestHandler]),
        (SystemClockAnalyzer.DiagnosticId, [ContractKeys.Entity]),
        (AmbiguityId, [ContractKeys.Entity, ContractKeys.AggregateRoot, ContractKeys.DomainEvent, ContractKeys.Command, ContractKeys.Query]),
        (UnclassifiedEntityId, [ContractKeys.Entity, ContractKeys.AggregateRoot])
    ];

    private static readonly DiagnosticDescriptor ConfigurationRule = new(
        ConfigurationId,
        title: "The HardSlice configuration is unusable",
        messageFormat: "{0}. The contracts of hardslice.json classify the types checked by {1}. Fix: {2}. Doc: 'docs/rules/HS900.md'.",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    private static readonly DiagnosticDescriptor AmbiguityRule = new(
        AmbiguityId,
        title: "A type is classified in two incompatible categories",
        messageFormat: "'{0}' is both {1} and {2}: it implements '{3}' and '{4}'. {5}. Fix: {6}. Doc: 'docs/rules/HS901.md'.",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnclassifiedEntityRule = new(
        UnclassifiedEntityId,
        title: "A domain type mapped as an EF Core entity implements no entity contract",
        messageFormat: "'{0}' is mapped as an EF Core entity by '{1}' but implements neither '{2}' nor '{3}'. "
            + "HardSlice classifies domain types only through these contracts. "
            + "Fix: implement {3} if '{0}' is the root of its aggregate, {2} otherwise. Doc: 'docs/rules/HS902.md'.",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly SymbolDisplayFormat DisplayFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeContainingType);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(ConfigurationRule, AmbiguityRule, UnclassifiedEntityRule);

    public override void Initialize(AnalysisContext context)
    {
        // Generated code holds mappings too, such as a scaffolded DbContext.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (RuleActivation.IsTestProject(start.Options))
                return;

            var compilation = start.Compilation;
            var activeRules = ContractsByRule
                .Where(rule => RuleActivation.IsActive(compilation, rule.RuleId, start.CancellationToken))
                .ToImmutableArray();
            if (activeRules.IsEmpty)
                return;

            var model = DomainModel.Create(compilation, HardSliceConfiguration.Read(start.Options, start.CancellationToken));
            // HS900 has no source location: the compiler filters it by the global options only, never by .editorconfig.
            if (RuleActivation.IsActive(compilation, ConfigurationId, start.CancellationToken))
                start.RegisterCompilationEndAction(end => ReportConfigurationProblems(end, model, activeRules));

            if (activeRules.Any(rule => rule.RuleId == AmbiguityId))
                start.RegisterSymbolAction(symbolContext => AnalyzeAmbiguity(symbolContext, model), SymbolKind.NamedType);

            if (activeRules.Any(rule => rule.RuleId == UnclassifiedEntityId)
                && EntityFramework.Create(compilation) is { } entityFramework
                && model.Contract(ContractKeys.Entity) is { } entity
                && model.Contract(ContractKeys.AggregateRoot) is { } aggregateRoot)
            {
                var mapping = new EntityMapping(model, entityFramework, entity, aggregateRoot);
                start.RegisterSymbolAction(mapping.AnalyzeConfiguration, SymbolKind.NamedType);
                start.RegisterSymbolAction(mapping.AnalyzeDbSet, SymbolKind.Property);
                start.RegisterOperationAction(mapping.AnalyzeEntityCall, OperationKind.Invocation);
            }
        });
    }

    private static void ReportConfigurationProblems(
        CompilationAnalysisContext context,
        DomainModel model,
        ImmutableArray<(string RuleId, ImmutableArray<string> Contracts)> activeRules)
    {
        foreach (var problem in model.Problems)
        {
            var rules = activeRules
                .Where(rule => rule.Contracts.Any(problem.Keys.Contains))
                .Select(rule => rule.RuleId)
                .ToArray();
            if (rules.Length == 0)
                continue;

            context.ReportDiagnostic(Diagnostic.Create(
                ConfigurationRule,
                problem.Problem.Location,
                problem.Problem.Description,
                rules.Length == 1 ? rules[0] : $"{string.Join(", ", rules.Take(rules.Length - 1))} and {rules[rules.Length - 1]}",
                problem.Problem.Fix));
        }
    }

    private static void AnalyzeAmbiguity(SymbolAnalysisContext context, DomainModel model)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct))
            return;

        var entityKey = model.IsAggregateRoot(type) ? ContractKeys.AggregateRoot : ContractKeys.Entity;
        var isEntity = model.IsEntity(type) || model.IsAggregateRoot(type);
        var isCommand = model.IsCommand(type);
        var isQuery = model.IsQuery(type);

        if (isEntity && model.IsDomainEvent(type))
        {
            Report(
                entityKey, "an entity", ContractKeys.DomainEvent, "a domain event",
                "A type is either an entity, which has an identity and changes through its methods, or a domain event, an immutable fact an entity records",
                $"keep '{type.Name}' as the entity and declare the event as a separate record that it records");
        }

        if (isCommand && isQuery)
        {
            Report(
                ContractKeys.Command, "a command", ContractKeys.Query, "a query",
                "A request either changes state, as a command, or only reads, as a query",
                "split it into a command that changes state and a query that reads");
        }

        if (isEntity && (isCommand || isQuery))
        {
            Report(
                entityKey, "an entity", isCommand ? ContractKeys.Command : ContractKeys.Query, isCommand ? "a command" : "a query",
                "A request is a message sent through the mediator, never a domain entity",
                $"declare the request as its own record in its slice, carrying the values '{type.Name}' needs");
        }

        void Report(string firstKey, string first, string secondKey, string second, string rule, string fix) =>
            context.ReportDiagnostic(Diagnostic.Create(
                AmbiguityRule,
                type.Locations.FirstOrDefault(location => location.IsInSource) ?? Location.None,
                type.ToDisplayString(DisplayFormat),
                first,
                second,
                model.Contract(firstKey)!.ToDisplayString(DisplayFormat),
                model.Contract(secondKey)!.ToDisplayString(DisplayFormat),
                rule,
                fix));
    }

    // The EF Core types of the three mapping forms HS902 recognizes, when the compilation references EF Core.
    private sealed class EntityFramework(
        INamedTypeSymbol entityTypeConfiguration,
        INamedTypeSymbol dbContext,
        INamedTypeSymbol dbSet,
        INamedTypeSymbol modelBuilder)
    {
        public INamedTypeSymbol EntityTypeConfiguration { get; } = entityTypeConfiguration;

        public INamedTypeSymbol DbContext { get; } = dbContext;

        public INamedTypeSymbol DbSet { get; } = dbSet;

        public INamedTypeSymbol ModelBuilder { get; } = modelBuilder;

        public static EntityFramework? Create(Compilation compilation) =>
            compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.IEntityTypeConfiguration`1") is { } configuration
            && compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbContext") is { } dbContext
            && compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbSet`1") is { } dbSet
            && compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.ModelBuilder") is { } modelBuilder
                ? new EntityFramework(configuration, dbContext, dbSet, modelBuilder)
                : null;
    }

    // HS902: a type of the assembly that declares the entity contract, mapped as an entity, must implement it.
    // Owned and complex types are not entity mappings: their contract alone says whether they are entities.
    private sealed class EntityMapping(DomainModel model, EntityFramework entityFramework, INamedTypeSymbol entity, INamedTypeSymbol aggregateRoot)
    {
        // 'RoomConfiguration : IEntityTypeConfiguration<Room>', reported on the type that sets Room as the argument,
        // itself or through a generic base class, never again on the types derived from it.
        public void AnalyzeConfiguration(SymbolAnalysisContext context)
        {
            var type = (INamedTypeSymbol)context.Symbol;
            foreach (var mapped in MappedTypes(type).Except(MappedTypes(type.BaseType?.OriginalDefinition), SymbolEqualityComparer.Default))
                Check(context.ReportDiagnostic, (INamedTypeSymbol)mapped!, type, type.Locations.FirstOrDefault(location => location.IsInSource));
        }

        // 'public DbSet<Room> Rooms { get; }' in a type derived from DbContext.
        public void AnalyzeDbSet(SymbolAnalysisContext context)
        {
            var property = (IPropertySymbol)context.Symbol;
            if (property.Type is INamedTypeSymbol set
                && SymbolEqualityComparer.Default.Equals(set.OriginalDefinition, entityFramework.DbSet)
                && set.TypeArguments[0] is INamedTypeSymbol mapped
                && DerivesFrom(property.ContainingType, entityFramework.DbContext))
            {
                Check(context.ReportDiagnostic, mapped, property, property.Locations.FirstOrDefault(location => location.IsInSource));
            }
        }

        // 'modelBuilder.Entity<Room>()', with or without its build action.
        public void AnalyzeEntityCall(OperationAnalysisContext context)
        {
            var invocation = (IInvocationOperation)context.Operation;
            var method = invocation.TargetMethod;
            if (method is { Name: "Entity", TypeArguments.Length: 1 }
                && method.TypeArguments[0] is INamedTypeSymbol mapped
                && SymbolEqualityComparer.Default.Equals(method.ContainingType.OriginalDefinition, entityFramework.ModelBuilder))
            {
                var location = invocation.Syntax is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess }
                    ? memberAccess.Name.GetLocation()
                    : invocation.Syntax.GetLocation();
                Check(context.ReportDiagnostic, mapped, context.ContainingSymbol, location);
            }
        }

        private IEnumerable<ITypeSymbol> MappedTypes(INamedTypeSymbol? type) =>
            type is null
                ? []
                : type.AllInterfaces
                    .Where(@interface => SymbolEqualityComparer.Default.Equals(@interface.OriginalDefinition, entityFramework.EntityTypeConfiguration))
                    .Select(@interface => @interface.TypeArguments[0])
                    .Where(argument => argument is INamedTypeSymbol);

        private void Check(Action<Diagnostic> report, INamedTypeSymbol mapped, ISymbol mappedBy, Location? location)
        {
            var definition = mapped.OriginalDefinition;
            if (!SymbolEqualityComparer.Default.Equals(definition.ContainingAssembly, entity.ContainingAssembly)
                || definition.TypeKind == TypeKind.Error
                || model.IsEntity(definition))
            {
                return;
            }

            report(Diagnostic.Create(
                UnclassifiedEntityRule,
                location ?? Location.None,
                definition.ToDisplayString(DisplayFormat),
                mappedBy.ToDisplayString(DisplayFormat),
                entity.ToDisplayString(DisplayFormat),
                aggregateRoot.ToDisplayString(DisplayFormat)));
        }

        private static bool DerivesFrom(INamedTypeSymbol? type, INamedTypeSymbol baseType)
        {
            for (var current = type?.BaseType; current is not null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, baseType))
                    return true;
            }

            return false;
        }
    }
}
