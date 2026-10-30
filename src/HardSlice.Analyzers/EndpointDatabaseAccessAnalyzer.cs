using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace HardSlice.Analyzers;

// HS003: an endpoint never reaches the database: it sends a command or a query through the mediator.
// An endpoint is a delegate given to a mapping method of ASP.NET Core, recognized by its symbol, never by its name.
// The methods it calls are not followed. Not applied to test projects.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EndpointDatabaseAccessAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "HS003";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "An endpoint reaches the database",
        messageFormat: "endpoint '{0}' in {1} uses '{2}' ({3}). Endpoints never reach the database. "
            + "Fix: move the data access into the handler of {4} and send {5} with IMediator. Doc: 'docs/rules/HS003.md'.",
        category: "HardSlice.Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly SymbolDisplayFormat DisplayFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeContainingType);

    // The mapping methods of EndpointRouteBuilderExtensions that take an endpoint, and their HTTP method.
    private static readonly ImmutableDictionary<string, string?> MappingMethods = new Dictionary<string, string?>
    {
        ["MapGet"] = "GET",
        ["MapPost"] = "POST",
        ["MapPut"] = "PUT",
        ["MapDelete"] = "DELETE",
        ["MapPatch"] = "PATCH",
        ["MapMethods"] = null,
        ["Map"] = null,
        ["MapFallback"] = null
    }.ToImmutableDictionary();

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        // An auto-generated header must not exempt an endpoint from the rule.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (RuleActivation.IsTestProject(start.Options)
                || Routing.Create(start.Compilation) is not { } routing
                || DatabaseTypes.Create(start.Compilation) is not { } database)
            {
                return;
            }

            var slices = new SliceMap(start.Compilation);
            start.RegisterOperationAction(
                operationContext => AnalyzeMapping(operationContext, routing, database, slices),
                OperationKind.Invocation);
        });
    }

    private static void AnalyzeMapping(OperationAnalysisContext context, Routing routing, DatabaseTypes database, SliceMap slices)
    {
        var mapping = (IInvocationOperation)context.Operation;
        if (!routing.IsMapping(mapping.TargetMethod)
            || mapping.Arguments.LastOrDefault(argument => argument.Parameter?.Type.TypeKind == TypeKind.Delegate
                || argument.Parameter?.Type.SpecialType == SpecialType.System_Delegate) is not { } handlerArgument
            || Handler.Find(handlerArgument.Value, context) is not { } handler)
        {
            return;
        }

        var accesses = new DatabaseAccesses(database, context.CancellationToken);
        accesses.AddParameters(handler.Method.Parameters);
        foreach (var body in handler.Bodies)
            accesses.Walk(body);

        if (accesses.Found.Count == 0)
            return;

        var mappingTree = mapping.Syntax.SyntaxTree;
        var mappingLocation = OperationLocation(mapping.Syntax);
        var httpMethod = MappingMethods[mapping.TargetMethod.Name];
        var endpoint = Describe(mapping, httpMethod, routing, context.CancellationToken);
        var place = context.ContainingSymbol is { } container && slices.SliceOf(container) is { } slice
            ? $"slice '{slice}'"
            : $"'{(context.ContainingSymbol as INamedTypeSymbol ?? context.ContainingSymbol?.ContainingType)?.ToDisplayString(DisplayFormat)}'";
        var (request, sent) = httpMethod switch
        {
            "GET" => ("a query", "the query"),
            null => ("a command or a query", "it"),
            _ => ("a command", "the command")
        };

        foreach (var access in accesses.Found)
        {
            // A method group declared in another file is reported on the mapping, where the IDE analyzes it.
            var location = access.Location.SourceTree == mappingTree ? access.Location : handlerArgument.Value.Syntax.GetLocation();
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                location,
                location == access.Location ? new[] { mappingLocation } : new[] { mappingLocation, access.Location },
                endpoint,
                place,
                access.Type.ToDisplayString(DisplayFormat),
                access.Description,
                request,
                sent));
        }
    }

    // 'POST /bookings/{id}' for a constant pattern, prefixed by the constant prefixes of the groups mapped in the same body.
    private static string Describe(IInvocationOperation mapping, string? httpMethod, Routing routing, CancellationToken cancellationToken)
    {
        var pattern = mapping.Arguments.FirstOrDefault(argument => argument.Parameter?.Name == "pattern")?.Value;
        var route = pattern is null
            ? null
            : Combine(GroupPrefix(Argument(mapping, 0), routing, depth: 0, cancellationToken), Pattern(pattern));

        var verb = mapping.TargetMethod.Name switch
        {
            "MapFallback" => "fallback",
            "MapMethods" => string.Join(",", HttpMethods(mapping)) is { Length: > 0 } methods ? methods : "MapMethods",
            _ => httpMethod
        };

        return verb is null ? route ?? "" : route is null ? verb : $"{verb} {route}";
    }

    private static IEnumerable<string> HttpMethods(IInvocationOperation mapping) =>
        mapping.Arguments.FirstOrDefault(argument => argument.Parameter?.Name == "httpMethods") is { } methods
            ? methods.Value.DescendantsAndSelf()
                .Where(operation => operation.ConstantValue is { HasValue: true, Value: string })
                .Select(operation => (string)operation.ConstantValue.Value!)
            : [];

    private static string Pattern(IOperation pattern) =>
        pattern.ConstantValue is { HasValue: true, Value: string constant } ? constant : pattern.Syntax.ToString();

    // Arguments are in evaluation order: named arguments may come in any order.
    private static IOperation? Argument(IInvocationOperation invocation, int ordinal) =>
        invocation.Arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == ordinal)?.Value;

    private static string GroupPrefix(IOperation? receiver, Routing routing, int depth, CancellationToken cancellationToken)
    {
        while (receiver is IConversionOperation conversion)
            receiver = conversion.Operand;

        if (depth > 8)
            return "";

        if (receiver is IInvocationOperation group && routing.IsGroup(group.TargetMethod) && Argument(group, 1) is { } prefix)
            return Combine(GroupPrefix(Argument(group, 0), routing, depth + 1, cancellationToken), Pattern(prefix));

        // 'var group = endpoints.MapGroup("/bookings");' in the same body.
        if (receiver is ILocalReferenceOperation local
            && receiver.SemanticModel is { } semanticModel
            && local.Local.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(cancellationToken) is VariableDeclaratorSyntax declarator
            && declarator.SyntaxTree == semanticModel.SyntaxTree
            && semanticModel.GetOperation(declarator, cancellationToken) is IVariableDeclaratorOperation { Initializer.Value: { } value })
        {
            return GroupPrefix(value, routing, depth + 1, cancellationToken);
        }

        return "";
    }

    private static string Combine(string prefix, string pattern)
    {
        if (prefix.Length == 0)
            return pattern;

        var trimmed = pattern.TrimStart('/');
        return trimmed.Length == 0 ? prefix : $"{prefix.TrimEnd('/')}/{trimmed}";
    }

    private static Location OperationLocation(SyntaxNode syntax) => syntax switch
    {
        InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess } => memberAccess.Name.Identifier.GetLocation(),
        InvocationExpressionSyntax { Expression: MemberBindingExpressionSyntax memberBinding } => memberBinding.Name.Identifier.GetLocation(),
        InvocationExpressionSyntax { Expression: SimpleNameSyntax name } => name.Identifier.GetLocation(),
        MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.GetLocation(),
        MemberBindingExpressionSyntax memberBinding => memberBinding.Name.Identifier.GetLocation(),
        ObjectCreationExpressionSyntax creation => creation.Type.GetLocation(),
        ImplicitObjectCreationExpressionSyntax creation => creation.NewKeyword.GetLocation(),
        CastExpressionSyntax cast => cast.Type.GetLocation(),
        BinaryExpressionSyntax binary => binary.Right.GetLocation(),
        AwaitExpressionSyntax await => await.AwaitKeyword.GetLocation(),
        _ => syntax.GetLocation()
    };

    // The mapping methods of ASP.NET Core, when the compilation references its routing.
    private sealed class Routing(INamedTypeSymbol extensions, INamedTypeSymbol? fallbackExtensions)
    {
        public static Routing? Create(Compilation compilation) =>
            compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Builder.EndpointRouteBuilderExtensions") is { } extensions
                ? new Routing(extensions, compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Builder.FallbackEndpointRouteBuilderExtensions"))
                : null;

        public bool IsMapping(IMethodSymbol method)
        {
            var definition = (method.ReducedFrom ?? method).OriginalDefinition;
            return MappingMethods.ContainsKey(definition.Name)
                && (SymbolEqualityComparer.Default.Equals(definition.ContainingType, extensions)
                    || definition.Name == "MapFallback" && SymbolEqualityComparer.Default.Equals(definition.ContainingType, fallbackExtensions));
        }

        public bool IsGroup(IMethodSymbol method)
        {
            var definition = (method.ReducedFrom ?? method).OriginalDefinition;
            return definition.Name == "MapGroup" && SymbolEqualityComparer.Default.Equals(definition.ContainingType, extensions);
        }
    }

    // The types that reach the database: a value whose type is or derives from one of them is a database access.
    private sealed class DatabaseTypes(ImmutableHashSet<INamedTypeSymbol> types)
    {
        private static readonly string[] MetadataNames =
        [
            "Microsoft.EntityFrameworkCore.DbContext",
            "Microsoft.EntityFrameworkCore.DbSet`1",
            "Microsoft.EntityFrameworkCore.IDbContextFactory`1",
            "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade",
            "System.Data.Common.DbConnection",
            "System.Data.IDbConnection",
            "System.Data.Common.DbDataSource"
        ];

        public static DatabaseTypes? Create(Compilation compilation)
        {
            var types = MetadataNames
                .Select(compilation.GetTypeByMetadataName)
                .OfType<INamedTypeSymbol>()
                .ToImmutableHashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            return types.IsEmpty ? null : new DatabaseTypes(types);
        }

        public bool Contains(ITypeSymbol? type) => type switch
        {
            ITypeParameterSymbol parameter => parameter.ConstraintTypes.Any(Contains),
            INamedTypeSymbol { TypeKind: not TypeKind.Error } named =>
                IsOrDerivesFrom(named) || named.AllInterfaces.Any(@interface => types.Contains(@interface.OriginalDefinition)),
            _ => false
        };

        private bool IsOrDerivesFrom(INamedTypeSymbol type)
        {
            for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
            {
                if (types.Contains(current.OriginalDefinition))
                    return true;
            }

            return false;
        }
    }

    // The delegate an endpoint is mapped to: a lambda, an anonymous method or a method group declared in the compilation.
    private sealed class Handler(IMethodSymbol method, ImmutableArray<IOperation> bodies)
    {
        public IMethodSymbol Method { get; } = method;

        public ImmutableArray<IOperation> Bodies { get; } = bodies;

        public static Handler? Find(IOperation value, OperationAnalysisContext context)
        {
            while (true)
            {
                switch (value)
                {
                    case IConversionOperation conversion:
                        value = conversion.Operand;
                        continue;
                    case IDelegateCreationOperation creation:
                        value = creation.Target;
                        continue;
                    case IAnonymousFunctionOperation function:
                        return new Handler(function.Symbol, [function.Body]);
                    case IMethodReferenceOperation reference:
                        return FromMethod(reference.Method, value, context);
                    default:
                        // A delegate from a variable, a field or a call cannot be analyzed.
                        return null;
                }
            }
        }

        private static Handler? FromMethod(IMethodSymbol method, IOperation reference, OperationAnalysisContext context)
        {
            var definition = (method.ReducedFrom ?? method).OriginalDefinition;
            definition = definition.PartialImplementationPart ?? definition;
            var bodies = ImmutableArray.CreateBuilder<IOperation>();
            foreach (var declaration in definition.DeclaringSyntaxReferences)
            {
                var syntax = declaration.GetSyntax(context.CancellationToken);
                var semanticModel = syntax.SyntaxTree == reference.Syntax.SyntaxTree
                    ? reference.SemanticModel
                    // A method group may be declared in another file of the compilation. The analyzer driver caches
                    // the semantic models it gives out, so this one is not built again for each mapping.
#pragma warning disable RS1030
                    : context.Compilation.GetSemanticModel(syntax.SyntaxTree);
#pragma warning restore RS1030
                if (semanticModel?.GetOperation(syntax, context.CancellationToken) is { } body)
                    bodies.Add(body);
            }

            // A method of an assembly in metadata has no body to analyze.
            return bodies.Count == 0 ? null : new Handler(method, bodies.ToImmutable());
        }
    }

    // Where a database type enters the endpoint: a parameter it receives, a field or a property it reads, a call, a
    // creation, a cast or an await that obtains it, or a local that holds it. A value obtained from another database
    // value, such as 'db.Bookings' or 'factory.CreateDbContext()', is part of the access already found.
    private sealed class DatabaseAccesses(DatabaseTypes database, CancellationToken cancellationToken)
    {
        private readonly HashSet<ISymbol> _symbols = new(SymbolEqualityComparer.Default);

        public List<Access> Found { get; } = [];

        public void AddParameters(ImmutableArray<IParameterSymbol> parameters)
        {
            foreach (var parameter in parameters)
            {
                // The parameters of a generic method group are constructed: the body refers to their definition.
                if (database.Contains(parameter.Type))
                    Add(parameter.OriginalDefinition, parameter.Type, $"parameter '{parameter.Name}'", parameter.Locations.FirstOrDefault());
            }
        }

        public void Walk(IOperation operation)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (operation is INameOfOperation)
                return;

            Visit(operation);
            foreach (var child in operation.ChildOperations)
                Walk(child);
        }

        private void Visit(IOperation operation)
        {
            switch (operation)
            {
                case IAnonymousFunctionOperation function:
                    AddParameters(function.Symbol.Parameters);
                    return;

                case ILocalFunctionOperation function:
                    AddParameters(function.Symbol.Parameters);
                    return;

                case IVariableDeclaratorOperation declarator when database.Contains(declarator.Symbol.Type):
                    // 'var db = factory.CreateDbContext();': the value that initializes the local is the access.
                    if (declarator.Initializer?.Value is { } value && database.Contains(WithoutImplicitConversions(value).Type))
                        _symbols.Add(declarator.Symbol);
                    else
                        AddLocal(declarator.Symbol);
                    return;

                case ILocalReferenceOperation local when database.Contains(local.Local.Type):
                    AddLocal(local.Local);
                    return;

                case IDeclarationPatternOperation { DeclaredSymbol: ILocalSymbol local } when database.Contains(local.Type):
                    AddLocal(local);
                    return;

                case IRecursivePatternOperation { DeclaredSymbol: ILocalSymbol local } when database.Contains(local.Type):
                    AddLocal(local);
                    return;

                // A parameter captured from the enclosing method or a primary constructor.
                case IParameterReferenceOperation reference when database.Contains(reference.Parameter.Type):
                    Add(reference.Parameter.OriginalDefinition, reference.Parameter.Type, $"parameter '{reference.Parameter.Name}'", OperationLocation(reference.Syntax));
                    return;
            }

            if (!database.Contains(operation.Type) || DerivesFromDatabase(Source(operation)))
                return;

            switch (operation)
            {
                case IFieldReferenceOperation field:
                    Add(field.Field.OriginalDefinition, operation.Type!, $"field '{field.Field.Name}'", OperationLocation(operation.Syntax));
                    break;
                case IPropertyReferenceOperation property:
                    Add(property.Property.OriginalDefinition, operation.Type!, $"property '{property.Property.Name}'", OperationLocation(operation.Syntax));
                    break;
                case IInvocationOperation invocation:
                    Add(invocation.TargetMethod.OriginalDefinition, operation.Type!, $"call '{invocation.TargetMethod.Name}'", OperationLocation(operation.Syntax));
                    break;
                case IObjectCreationOperation creation:
                    Add(creation.Constructor?.OriginalDefinition ?? (ISymbol)operation.Type!, operation.Type!, "object creation", OperationLocation(operation.Syntax));
                    break;
                // An implicit conversion, such as 'null' given to a parameter, obtains nothing.
                case IConversionOperation { IsImplicit: false }:
                    Add(operation.Type!, operation.Type!, "cast", OperationLocation(operation.Syntax));
                    break;
                case IAwaitOperation await:
                    var awaited = WithoutImplicitConversions(await.Operation) is IInvocationOperation call ? call.TargetMethod.OriginalDefinition : null;
                    Add(awaited ?? (ISymbol)operation.Type!, operation.Type!, awaited is null ? "await" : $"call '{awaited.Name}'", OperationLocation(operation.Syntax));
                    break;
            }
        }

        private void AddLocal(ILocalSymbol local) =>
            Add(local, local.Type, $"local '{local.Name}'", local.Locations.FirstOrDefault());

        private void Add(ISymbol symbol, ITypeSymbol type, string description, Location? location)
        {
            if (location is { IsInSource: true } && _symbols.Add(symbol))
                Found.Add(new Access(type, description, location));
        }

        private bool DerivesFromDatabase(IOperation? operation)
        {
            for (var current = operation; current is not null; current = Source(current))
            {
                if (database.Contains(current.Type))
                    return true;
            }

            return false;
        }

        // The value an operation is obtained from: the receiver of a member or a call, the operand of a cast or an await.
        private static IOperation? Source(IOperation operation) => operation switch
        {
            IInvocationOperation invocation => invocation.Instance
                ?? (invocation.TargetMethod.IsExtensionMethod && invocation.Arguments.Length > 0 ? invocation.Arguments[0].Value : null),
            IMemberReferenceOperation member => member.Instance,
            IConversionOperation conversion => conversion.Operand,
            IAwaitOperation await => await.Operation,
            _ => null
        };

        private static IOperation WithoutImplicitConversions(IOperation operation)
        {
            while (operation is IConversionOperation { IsImplicit: true } conversion)
                operation = conversion.Operand;

            return operation;
        }
    }

    private sealed class Access(ITypeSymbol type, string description, Location location)
    {
        public ITypeSymbol Type { get; } = type;

        public string Description { get; } = description;

        public Location Location { get; } = location;
    }
}
