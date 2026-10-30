using System.Collections.Immutable;
using HardSlice.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using static HardSlice.Analyzers.EndpointMapping;

namespace HardSlice.Analyzers;

// HS006: a query handler never writes through EF Core, never turns tracking back on nor reconfigures it, and reads its
// entities with a local guarantee that they are not tracked. A query handler is a type that implements the requestHandler
// contract of hardslice.json, directly or through a base class, for a request that implements the query contract, or a type
// parameter constrained to one; all its code is examined: methods, lambdas, local functions, initializers and nested types. The global
// UseQueryTrackingBehavior is not read: the guarantee is AsNoTracking in the handler. Generated code is analyzed. Not
// applied to test projects.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class QueryHandlerTrackingAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "HS006";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "A query handler tracks or writes",
        messageFormat: "query handler '{0}' {1}. Query handlers read without tracking and never write. Fix: {2}. Doc: 'docs/rules/HS006.md'.",
        category: "HardSlice.Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private const string WriteFix = "move the write into a command slice";

    private const string ReadFix = "add AsNoTracking() where the query starts, and project to a DTO";

    private static readonly SymbolDisplayFormat DisplayFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeContainingType,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

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

            // Without usable contracts, HS900 reports the configuration. Without EF Core, a handler has nothing to track.
            var model = DomainModel.Create(start.Compilation, HardSliceConfiguration.Read(start.Options, start.CancellationToken));
            if (model.Contract(ContractKeys.Query) is not { } query
                || model.Contract(ContractKeys.RequestHandler) is not { } handler
                || EntityContainment.Create(start.Compilation, model) is not { } containment
                || EntityFramework.Create(start.Compilation) is not { } entityFramework)
            {
                return;
            }

            start.RegisterSymbolStartAction(
                symbolStart =>
                {
                    // A type nested in a query handler is part of its code.
                    var type = (INamedTypeSymbol)symbolStart.Symbol;
                    while (type is not null && !IsQueryHandler(type, model, query, handler))
                        type = type.ContainingType;
                    if (type is null)
                        return;

                    var name = type.ToDisplayString(DisplayFormat);
                    var analysis = new HandlerAnalysis(name, entityFramework, containment);
                    symbolStart.RegisterOperationAction(analysis.AnalyzeInvocation, OperationKind.Invocation);
                    symbolStart.RegisterOperationAction(analysis.AnalyzeLoop, OperationKind.Loop);
                    symbolStart.RegisterOperationAction(analysis.AnalyzeAssignment, OperationKind.SimpleAssignment);
                },
                SymbolKind.NamedType);
        });
    }

    // Abstract handlers included: their code runs in the query handlers derived from them.
    private static bool IsQueryHandler(INamedTypeSymbol type, DomainModel model, INamedTypeSymbol query, INamedTypeSymbol handler) =>
        type.TypeKind is TypeKind.Class or TypeKind.Struct
        && !type.IsStatic
        && DomainModel.Implementations(type, handler).Any(implementation =>
            implementation.TypeArguments.Length == 2 && IsQuery(implementation.TypeArguments[0], model, query));

    // A query, or a type parameter constrained to one: 'Handler<TQuery> : IRequestHandler<TQuery, BookingDto> where TQuery : IQuery<BookingDto>'.
    private static bool IsQuery(ITypeSymbol request, DomainModel model, INamedTypeSymbol query) =>
        request is ITypeParameterSymbol parameter
            ? parameter.ConstraintTypes.Any(constraint => model.IsQuery(constraint) || SymbolEqualityComparer.Default.Equals(constraint.OriginalDefinition, query))
            : model.IsQuery(request);

    // The checks of one query handler, on each operation of its code.
    private sealed class HandlerAnalysis(string handler, EntityFramework entityFramework, EntityContainment containment)
    {
        public void AnalyzeInvocation(OperationAnalysisContext context)
        {
            var invocation = (IInvocationOperation)context.Operation;
            var method = invocation.TargetMethod;
            if (entityFramework.IsWrite(method))
            {
                Report(context, invocation.Syntax, $"writes through '{method.Name}'", WriteFix);
            }
            else if (entityFramework.IsTrackingOperator(method))
            {
                Report(context, invocation.Syntax, $"turns tracking back on with '{method.Name}'", "remove AsTracking(): the query starts with AsNoTracking()");
            }
            else if (entityFramework.IsFind(method))
            {
                Report(
                    context,
                    invocation.Syntax,
                    $"reads with '{method.Name}', which always tracks the entity it returns",
                    "query the entity with AsNoTracking() and Where(), and project to a DTO");
            }
            else if (entityFramework.IsExecution(method) && Receiver(invocation) is { } source && !IsInExpressionTree(invocation))
            {
                AnalyzeRead(context, source, OperationLocation(invocation.Syntax));
            }
        }

        // 'foreach (var booking in db.Bookings)' and 'await foreach' execute the query they enumerate.
        public void AnalyzeLoop(OperationAnalysisContext context)
        {
            if (context.Operation is IForEachLoopOperation loop && !IsInExpressionTree(loop))
                AnalyzeRead(context, loop.Collection, loop.Collection.Syntax.GetLocation());
        }

        // 'db.ChangeTracker.QueryTrackingBehavior = ...', whatever the value: the context may serve other components of the scope.
        public void AnalyzeAssignment(OperationAnalysisContext context)
        {
            var assignment = (ISimpleAssignmentOperation)context.Operation;
            if (assignment.Target is IPropertyReferenceOperation { Property: { Name: "QueryTrackingBehavior" } property }
                && entityFramework.IsChangeTracker(property.ContainingType))
            {
                Report(
                    context,
                    assignment.Target.Syntax,
                    "reconfigures tracking through 'ChangeTracker.QueryTrackingBehavior'",
                    "remove the assignment, which changes the context for every component of the scope, and start each query with AsNoTracking()");
            }
        }

        private void AnalyzeRead(OperationAnalysisContext context, IOperation source, Location execution)
        {
            if (Query.Recognize(source, entityFramework, containment, context.CancellationToken) is not { } query
                || query.Guaranteed.Any(entityFramework.SetsTracking))
            {
                return;
            }

            // The elements EF Core materializes, then what the last projection passes into the result.
            var entity = containment.Find(query.ElementType)?.Entity
                ?? (query.LastProjection is { } projection ? ProjectedEntity(projection) : null);
            if (entity is null)
                return;

            Report(
                context,
                execution,
                $"reads '{entity.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString(DisplayFormat)}' from '{query.Root}' without a local guarantee that it is not tracked",
                ReadFix);
        }

        // A subquery of a projection, such as 'Select(b => new { Lines = db.Set<BookingLine>().ToList() })', is part of the
        // query that holds it: it never runs on its own and follows the tracking of that query.
        private bool IsInExpressionTree(IOperation operation)
        {
            for (var current = operation.Parent; current is not null; current = current.Parent)
            {
                if (current is IAnonymousFunctionOperation function && entityFramework.IsExpressionTree(function))
                    return true;
            }

            return false;
        }

        // 'Select(b => Describe(b))' and 'Select(b => new BookingDto(b))' materialize the entity to evaluate the method or
        // the constructor on the client, even when the result holds no entity.
        private ITypeSymbol? ProjectedEntity(IAnonymousFunctionOperation projection)
        {
            foreach (var operation in projection.Body.Descendants())
            {
                if (operation.Type is null || containment.Find(operation.Type) is not { } path || !PassesIntoResult(operation, projection))
                    continue;

                return path.Entity;
            }

            return null;
        }

        private bool PassesIntoResult(IOperation operation, IAnonymousFunctionOperation projection)
        {
            var value = operation;
            while (value.Parent is IConversionOperation conversion)
                value = conversion;

            return value.Parent switch
            {
                // An element of an array or a tuple, a member of an anonymous type or an object initializer, an argument of a
                // constructor, or of a method evaluated on the client, collection initializers included.
                IArrayInitializerOperation or ITupleOperation => true,
                ISimpleAssignmentOperation assignment => assignment.Value == value
                    && assignment.Parent is IObjectOrCollectionInitializerOperation or IAnonymousObjectCreationOperation,
                IArgumentOperation { Parent: IObjectCreationOperation } => true,
                IArgumentOperation { Parent: IInvocationOperation invocation } => !entityFramework.IsTranslated(invocation.TargetMethod),
                // A method of the entity, evaluated on the client.
                IInvocationOperation invocation => invocation.Instance == value && !entityFramework.IsTranslated(invocation.TargetMethod),
                // The value of the projection itself, converted: 'Select(b => (object)b)'.
                IReturnOperation { Parent: IBlockOperation { Parent: { } function } } => function == projection,
                _ => false
            };
        }

        private void Report(OperationAnalysisContext context, SyntaxNode syntax, string what, string fix) =>
            Report(context, OperationLocation(syntax), what, fix);

        private void Report(OperationAnalysisContext context, Location location, string what, string fix) =>
            context.ReportDiagnostic(Diagnostic.Create(Rule, location, handler, what, fix));
    }

    // A query EF Core runs: its root, the chain that guarantees its tracking behavior, its elements and its last projection.
    private sealed class Query(string root, ImmutableArray<IMethodSymbol> guaranteed, ITypeSymbol elementType, IAnonymousFunctionOperation? lastProjection)
    {
        public string Root { get; } = root;

        // The operators of the expression and, for a local, of its initializer: a reassignment may not run.
        public ImmutableArray<IMethodSymbol> Guaranteed { get; } = guaranteed;

        public ITypeSymbol ElementType { get; } = elementType;

        public IAnonymousFunctionOperation? LastProjection { get; } = lastProjection;

        // Null when the query cannot be analyzed: obtained from a field, a property, a parameter or a method other than an
        // operator, rooted in a set of types that are not entities, or held by a local reassigned another way.
        public static Query? Recognize(IOperation source, EntityFramework entityFramework, EntityContainment containment, CancellationToken cancellationToken)
        {
            var value = WithoutConversions(source);
            if (entityFramework.QueryElement(value.Type) is not { } elementType)
                return null;

            var operators = ImmutableArray.CreateBuilder<IInvocationOperation>();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                switch (value)
                {
                    case IInvocationOperation invocation when entityFramework.IsSetMethod(invocation.TargetMethod)
                        || entityFramework.IsSqlRoot(invocation.TargetMethod):
                        var set = invocation.TargetMethod;
                        var root = set.Name == "Set" ? $"Set<{set.TypeArguments[0].ToDisplayString(DisplayFormat)}>()" : $"{set.Name}(...)";
                        return entityFramework.QueryElement(invocation.Type) is { } setElement && containment.IsEntity(setElement)
                            ? Create(root, operators, elementType, guaranteed: [], projection: null)
                            : null;

                    case IInvocationOperation invocation when entityFramework.IsOperator(invocation.TargetMethod) && Receiver(invocation) is { } receiver:
                        operators.Add(invocation);
                        value = WithoutConversions(receiver);
                        continue;

                    case IPropertyReferenceOperation property when entityFramework.IsSet(property.Type):
                        return entityFramework.QueryElement(property.Type) is { } propertyElement && containment.IsEntity(propertyElement)
                            ? Create(property.Property.ToDisplayString(DisplayFormat), operators, elementType, guaranteed: [], projection: null)
                            : null;

                    case ILocalReferenceOperation local:
                        return Initializer(local, entityFramework) is { } initializer
                            && Recognize(initializer, entityFramework, containment, cancellationToken) is { } initialized
                            ? Create(initialized.Root, operators, elementType, initialized.Guaranteed, initialized.LastProjection)
                            : null;

                    default:
                        return null;
                }
            }
        }

        // Operators come from the execution to the root: the first projection met is the last one EF Core applies.
        private static Query Create(
            string root,
            ImmutableArray<IInvocationOperation>.Builder operators,
            ITypeSymbol elementType,
            ImmutableArray<IMethodSymbol> guaranteed,
            IAnonymousFunctionOperation? projection) =>
            new(
                root,
                operators.Select(@operator => @operator.TargetMethod).Concat(guaranteed).ToImmutableArray(),
                elementType,
                operators.Select(Projection).FirstOrDefault(found => found is not null) ?? projection);

        // The selector of 'Select' or the result selector of 'SelectMany'.
        private static IAnonymousFunctionOperation? Projection(IInvocationOperation invocation)
        {
            if (invocation.TargetMethod.Name is not ("Select" or "SelectMany") || invocation.Arguments.LastOrDefault()?.Value is not { } selector)
                return null;

            while (true)
            {
                switch (selector)
                {
                    case IConversionOperation conversion:
                        selector = conversion.Operand;
                        continue;
                    case IDelegateCreationOperation creation:
                        selector = creation.Target;
                        continue;
                    case IAnonymousFunctionOperation function:
                        return function;
                    default:
                        return null;
                }
            }
        }

        // The initializer of a local whose only other assignments read 'query = query.<operators>', conditional or not.
        private static IOperation? Initializer(ILocalReferenceOperation reference, EntityFramework entityFramework)
        {
            var root = (IOperation)reference;
            while (root.Parent is { } parent)
                root = parent;

            IOperation? initializer = null;
            foreach (var operation in root.Descendants())
            {
                switch (operation)
                {
                    case IVariableDeclaratorOperation declarator when SymbolEqualityComparer.Default.Equals(declarator.Symbol, reference.Local):
                        initializer = declarator.Initializer?.Value;
                        if (initializer is null)
                            return null;
                        break;

                    case ILocalReferenceOperation other when SymbolEqualityComparer.Default.Equals(other.Local, reference.Local)
                        && !IsReassignmentOnItself(other, entityFramework)
                        && IsWritten(other):
                        return null;
                }
            }

            return initializer;
        }

        // 'query = query.Where(...)': the value is a chain of operators on the local itself.
        private static bool IsReassignmentOnItself(ILocalReferenceOperation target, EntityFramework entityFramework)
        {
            if (target.Parent is not ISimpleAssignmentOperation assignment || assignment.Target != target)
                return false;

            var value = WithoutConversions(assignment.Value);
            while (value is IInvocationOperation invocation && entityFramework.IsOperator(invocation.TargetMethod) && Receiver(invocation) is { } receiver)
                value = WithoutConversions(receiver);

            return value is ILocalReferenceOperation source && SymbolEqualityComparer.Default.Equals(source.Local, target.Local);
        }

        // The target of an assignment of any kind, a deconstruction, or an argument passed by reference.
        private static bool IsWritten(ILocalReferenceOperation reference)
        {
            IOperation current = reference;
            while (current.Parent is ITupleOperation tuple)
                current = tuple;

            return current.Parent switch
            {
                IAssignmentOperation assignment => assignment.Target == current,
                IArgumentOperation argument => argument.Parameter?.RefKind is RefKind.Ref or RefKind.Out,
                _ => false
            };
        }
    }

    // The receiver of a call: the instance, or the first argument of an extension method.
    private static IOperation? Receiver(IInvocationOperation invocation) =>
        invocation.Instance ?? (invocation.TargetMethod.IsExtensionMethod ? Argument(invocation, 0) : null);

    // The members of EF Core and LINQ the rule recognizes, by their symbols.
    private sealed class EntityFramework
    {
        private static readonly ImmutableHashSet<string> SaveMethods = new[] { "SaveChanges", "SaveChangesAsync" }.ToImmutableHashSet();

        private static readonly ImmutableHashSet<string> TrackingMethods = new[]
        {
            "Add", "AddAsync", "AddRange", "AddRangeAsync", "Attach", "AttachRange", "Update", "UpdateRange", "Remove", "RemoveRange"
        }.ToImmutableHashSet();

        private static readonly ImmutableHashSet<string> BulkMethods = new[]
        {
            "ExecuteUpdate", "ExecuteUpdateAsync", "ExecuteDelete", "ExecuteDeleteAsync"
        }.ToImmutableHashSet();

        private static readonly ImmutableHashSet<string> SqlMethods = new[]
        {
            "ExecuteSql", "ExecuteSqlAsync", "ExecuteSqlRaw", "ExecuteSqlRawAsync", "ExecuteSqlInterpolated", "ExecuteSqlInterpolatedAsync"
        }.ToImmutableHashSet();

        private static readonly ImmutableHashSet<string> SqlRoots = new[] { "FromSql", "FromSqlRaw", "FromSqlInterpolated" }.ToImmutableHashSet();

        private static readonly ImmutableHashSet<string> NoTrackingMethods = new[] { "AsNoTracking", "AsNoTrackingWithIdentityResolution" }.ToImmutableHashSet();

        // The operators that run the query, synchronous or asynchronous.
        private static readonly ImmutableHashSet<string> ExecutionMethods = new[]
        {
            "ToList", "ToListAsync", "ToArray", "ToArrayAsync", "ToDictionary", "ToDictionaryAsync", "ToHashSet", "ToHashSetAsync", "ToLookup",
            "First", "FirstOrDefault", "FirstAsync", "FirstOrDefaultAsync", "Single", "SingleOrDefault", "SingleAsync", "SingleOrDefaultAsync",
            "Last", "LastOrDefault", "LastAsync", "LastOrDefaultAsync", "ElementAt", "ElementAtOrDefault", "ElementAtAsync", "ElementAtOrDefaultAsync",
            "Load", "LoadAsync", "ForEachAsync", "AsEnumerable", "AsAsyncEnumerable"
        }.ToImmutableHashSet();

        private readonly INamedTypeSymbol _dbContext;
        private readonly INamedTypeSymbol _dbSet;
        private readonly INamedTypeSymbol _queryable;
        private readonly INamedTypeSymbol? _changeTracker;
        private readonly INamedTypeSymbol? _expression;
        private readonly INamedTypeSymbol? _queryExtensions;
        private readonly INamedTypeSymbol? _relationalQueryExtensions;
        private readonly INamedTypeSymbol? _relationalDatabaseExtensions;
        private readonly ImmutableHashSet<INamedTypeSymbol> _operatorTypes;
        private readonly ImmutableHashSet<INamedTypeSymbol> _executionTypes;
        private readonly ImmutableHashSet<INamedTypeSymbol> _translatedTypes;

        private EntityFramework(Compilation compilation, INamedTypeSymbol dbContext, INamedTypeSymbol dbSet, INamedTypeSymbol queryable)
        {
            _dbContext = dbContext;
            _dbSet = dbSet;
            _queryable = queryable;
            _changeTracker = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker");
            _expression = compilation.GetTypeByMetadataName("System.Linq.Expressions.Expression`1");
            _queryExtensions = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions");
            _relationalQueryExtensions = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.RelationalQueryableExtensions");
            _relationalDatabaseExtensions = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions");

            var linqQueryable = compilation.GetTypeByMetadataName("System.Linq.Queryable");
            var enumerable = compilation.GetTypeByMetadataName("System.Linq.Enumerable");
            _operatorTypes = Set(linqQueryable, _queryExtensions, _relationalQueryExtensions);
            _executionTypes = Set(linqQueryable, enumerable, compilation.GetTypeByMetadataName("System.Linq.AsyncEnumerable"), _queryExtensions, dbSet);
            // Translated to SQL inside a projection, never evaluated on the client: LINQ operators and EF.Property.
            _translatedTypes = Set(linqQueryable, enumerable, compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.EF"));
        }

        public static EntityFramework? Create(Compilation compilation) =>
            compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbContext") is { } dbContext
            && compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbSet`1") is { } dbSet
            && compilation.GetTypeByMetadataName("System.Linq.IQueryable`1") is { } queryable
                ? new EntityFramework(compilation, dbContext, dbSet, queryable)
                : null;

        // SaveChanges of a context, an EF Core write in bulk or in SQL, or a change of what the context tracks.
        public bool IsWrite(IMethodSymbol method) =>
            (SaveMethods.Contains(method.Name) && Declares(method, _dbContext))
            || (TrackingMethods.Contains(method.Name) && (Declares(method, _dbContext) || Declares(method, _dbSet)))
            || (BulkMethods.Contains(method.Name) && IsExtensionOf(method, _queryExtensions, _relationalQueryExtensions))
            || (SqlMethods.Contains(method.Name) && IsExtensionOf(method, _relationalDatabaseExtensions));

        public bool IsTrackingOperator(IMethodSymbol method) => method.Name == "AsTracking" && IsExtensionOf(method, _queryExtensions);

        public bool IsFind(IMethodSymbol method) =>
            method.Name is "Find" or "FindAsync" && (Declares(method, _dbContext) || Declares(method, _dbSet));

        public bool IsExecution(IMethodSymbol method) => ExecutionMethods.Contains(method.Name) && IsExtensionOf(method, _executionTypes);

        public bool IsOperator(IMethodSymbol method) => !SqlRoots.Contains(method.Name) && IsExtensionOf(method, _operatorTypes);

        public bool IsSetMethod(IMethodSymbol method) => method.Name == "Set" && method.IsGenericMethod && Declares(method, _dbContext);

        public bool IsSqlRoot(IMethodSymbol method) => SqlRoots.Contains(method.Name) && IsExtensionOf(method, _relationalQueryExtensions);

        // AsNoTracking and AsNoTrackingWithIdentityResolution guarantee the read; AsTracking is reported on its own.
        public bool SetsTracking(IMethodSymbol method) =>
            (NoTrackingMethods.Contains(method.Name) || method.Name == "AsTracking") && IsExtensionOf(method, _queryExtensions);

        public bool IsTranslated(IMethodSymbol method) => IsExtensionOf(method, _translatedTypes);

        // A lambda converted to 'Expression<TDelegate>', as the operators of Queryable take them.
        public bool IsExpressionTree(IAnonymousFunctionOperation function)
        {
            for (var current = function.Parent; current is IConversionOperation or IDelegateCreationOperation; current = current.Parent)
            {
                if (current.Type is INamedTypeSymbol type && SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, _expression))
                    return true;
            }

            return false;
        }

        public bool IsChangeTracker(INamedTypeSymbol type) => Derives(type, _changeTracker);

        public bool IsSet(ITypeSymbol? type) => type is INamedTypeSymbol named && Derives(named, _dbSet);

        // The T of an IQueryable<T>, DbSet<T> included.
        public ITypeSymbol? QueryElement(ITypeSymbol? type)
        {
            if (type is not INamedTypeSymbol named)
                return null;

            if (SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, _queryable))
                return named.TypeArguments[0];

            return named.AllInterfaces.FirstOrDefault(@interface => SymbolEqualityComparer.Default.Equals(@interface.OriginalDefinition, _queryable))?.TypeArguments[0];
        }

        // Declared by the type or by one of its derived types overriding it: 'AppDbContext.SaveChangesAsync' overrides DbContext's.
        private static bool Declares(IMethodSymbol method, INamedTypeSymbol type)
        {
            for (var current = method.OriginalDefinition; current is not null; current = current.OverriddenMethod)
            {
                if (SymbolEqualityComparer.Default.Equals(current.ContainingType.OriginalDefinition, type))
                    return true;
            }

            return false;
        }

        private static bool IsExtensionOf(IMethodSymbol method, params INamedTypeSymbol?[] types) =>
            types.Any(type => type is not null && SymbolEqualityComparer.Default.Equals((method.ReducedFrom ?? method).ContainingType.OriginalDefinition, type));

        private static bool IsExtensionOf(IMethodSymbol method, ImmutableHashSet<INamedTypeSymbol> types) =>
            types.Contains((method.ReducedFrom ?? method).ContainingType.OriginalDefinition);

        private static bool Derives(INamedTypeSymbol type, INamedTypeSymbol? baseType)
        {
            for (INamedTypeSymbol? current = type; current is not null && baseType is not null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, baseType))
                    return true;
            }

            return false;
        }

        private static ImmutableHashSet<INamedTypeSymbol> Set(params INamedTypeSymbol?[] types) =>
            types.OfType<INamedTypeSymbol>().ToImmutableHashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
    }
}
