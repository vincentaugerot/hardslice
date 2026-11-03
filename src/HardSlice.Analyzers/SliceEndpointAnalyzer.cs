using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using static HardSlice.Analyzers.EndpointMapping;

namespace HardSlice.Analyzers;

// HS015: the endpoint of a slice is mapped in its slice, on the group of its module, by a method its module calls, and
// returns a type of Microsoft.AspNetCore.Http.HttpResults. Applies to the mappings declared under '<AssemblyName>.Features',
// recognized as HS003 recognizes them; the endpoints of Program are not slice endpoints. Not applied to test projects.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SliceEndpointAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "HS015";

    private const string Title = "A slice endpoint leaves its slice, the group of its module or its TypedResults";

    private const string MessageFormat = "endpoint '{0}' in {1} {2}. {3} Fix: {4}. Skill: add-slice. Doc: 'docs/rules/HS015.md'.";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        category: "HardSlice.Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: RuleSheet.Uri(DiagnosticId));

    // Whether the module calls the method that maps an endpoint is known at the end of the compilation only: in the IDE,
    // this part of the rule is reported by full solution analysis.
    private static readonly DiagnosticDescriptor RegistrationRule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        category: "HardSlice.Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: RuleSheet.Uri(DiagnosticId),
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    private const string HttpResultsNamespace = "Microsoft.AspNetCore.Http.HttpResults";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule, RegistrationRule);

    public override void Initialize(AnalysisContext context)
    {
        // An auto-generated header must not exempt an endpoint from the rule.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (RuleActivation.IsTestProject(start.Options) || Routing.Create(start.Compilation) is not { } routing)
                return;

            var slices = new SliceMap(start.Compilation);
            var resultType = start.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Http.IResult");
            var registrations = new Registrations(slices);
            start.RegisterOperationAction(
                operationContext =>
                {
                    var invocation = (IInvocationOperation)operationContext.Operation;
                    registrations.RecordCall(operationContext.ContainingSymbol, invocation.TargetMethod);
                    if (routing.IsMapping(invocation.TargetMethod))
                        AnalyzeMapping(operationContext, invocation, routing, slices, resultType, registrations);
                },
                OperationKind.Invocation);
            // 'Action<RouteGroupBuilder>[] endpoints = [GetBookingEndpoint.Map, ...]' in the module calls them too.
            start.RegisterOperationAction(
                operationContext => registrations.RecordCall(
                    operationContext.ContainingSymbol,
                    ((IMethodReferenceOperation)operationContext.Operation).Method),
                OperationKind.MethodReference);
            start.RegisterCompilationEndAction(registrations.ReportUncalled);
        });
    }

    private static void AnalyzeMapping(
        OperationAnalysisContext context,
        IInvocationOperation mapping,
        Routing routing,
        SliceMap slices,
        INamedTypeSymbol? resultType,
        Registrations registrations)
    {
        if (context.ContainingSymbol is not { } container || slices.FeaturesPathOf(container) is not { } path)
            return;

        var endpoint = Describe(mapping, routing, context.CancellationToken);
        var place = PlaceOf(container, slices);
        var mappingLocation = OperationLocation(mapping.Syntax);
        var module = path.Length > 0 ? path[0] : null;
        var moduleType = $"{module ?? "<Module>"}Module";

        if (path.Length < 2)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                mappingLocation,
                endpoint,
                place,
                "is mapped outside a slice",
                "An endpoint belongs to the slice of its use case: its module only maps the endpoints of its slices on its group.",
                "move the mapping into the 'Map(RouteGroupBuilder group)' method of an internal endpoint class in "
                    + $"'Features/{module ?? "<Module>"}/<Slice>/', and call that method from '{moduleType}' with the group of the module"));
        }

        if (Argument(mapping, 0) is { } receiver
            && WithoutConversions(receiver).Type is { TypeKind: not TypeKind.Error } receiverType
            && !routing.IsGroupBuilder(receiverType))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                receiver.Syntax.GetLocation(),
                new[] { mappingLocation },
                endpoint,
                place,
                $"is mapped on '{receiverType.ToDisplayString(DisplayFormat)}', not on a group",
                "A slice endpoint is mapped on the RouteGroupBuilder of its module, which owns the route prefix and the policies of its endpoints.",
                $"take the 'RouteGroupBuilder group' that '{moduleType}' creates with MapGroup as a parameter, and map the endpoint on it"));
        }

        if (HandlerArgument(mapping) is { } handlerArgument
            && ReturnTypeOf(handlerArgument.Value) is { TypeKind: not TypeKind.Error } returnType
            && Response(returnType) is { TypeKind: not TypeKind.Error } response
            && !IsHttpResult(response))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                ResultLocation(handlerArgument.Value.Syntax),
                new[] { mappingLocation },
                endpoint,
                place,
                $"returns '{response.ToDisplayString(DisplayFormat)}'",
                "A slice endpoint returns TypedResults, so its responses are part of its signature.",
                ResultFix(response, resultType)));
        }

        if (path.Length >= 2)
            registrations.Expect(container, module!, Diagnostic.Create(
                RegistrationRule,
                mappingLocation,
                endpoint,
                place,
                $"is mapped by '{container.ToDisplayString(DisplayFormat)}', which '{moduleType}' never calls",
                "A slice endpoint is exposed only through its module, which maps every endpoint of its slices.",
                $"call '{container.ToDisplayString(DisplayFormat)}' with the group of the module from the mapping method of '{moduleType}'"));
    }

    // The return type of the endpoint: of its lambda, its anonymous method or its method group, or the delegate type of a
    // variable. A delegate typed only as 'Delegate' has no return type to check.
    private static ITypeSymbol? ReturnTypeOf(IOperation value)
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
                    return function.Symbol.ReturnType;
                case IMethodReferenceOperation reference:
                    return reference.Method.ReturnType;
                default:
                    return (value.Type as INamedTypeSymbol)?.DelegateInvokeMethod?.ReturnType;
            }
        }
    }

    // The response an endpoint returns: T of Task<T> or ValueTask<T>.
    private static ITypeSymbol Response(ITypeSymbol returnType) =>
        returnType is INamedTypeSymbol { Name: "Task" or "ValueTask", Arity: 1 } awaitable
        && awaitable.ContainingNamespace?.ToDisplayString() == "System.Threading.Tasks"
            ? awaitable.TypeArguments[0]
            : returnType;

    private static bool IsHttpResult(ITypeSymbol type) =>
        type is INamedTypeSymbol named && named.ContainingNamespace?.ToDisplayString() == HttpResultsNamespace;

    private static string ResultFix(ITypeSymbol response, INamedTypeSymbol? resultType)
    {
        if (response.SpecialType == SpecialType.System_Void
            || response is INamedTypeSymbol { Name: "Task" or "ValueTask", Arity: 0 } && response.ContainingNamespace?.ToDisplayString() == "System.Threading.Tasks")
        {
            return "return the response built with TypedResults, such as 'TypedResults.NoContent()', and declare its type, "
                + "such as 'Task<NoContent>', as the return type of the endpoint";
        }

        var isResult = resultType is not null
            && (SymbolEqualityComparer.Default.Equals(response, resultType)
                || response.AllInterfaces.Any(@interface => SymbolEqualityComparer.Default.Equals(@interface, resultType)));
        if (isResult || response.SpecialType == SpecialType.System_Object)
        {
            return "build every response with TypedResults instead of Results, and declare them all as the return type of the endpoint, "
                + "such as 'Task<Results<Ok<T>, NotFound>>'";
        }

        var dto = response.ToDisplayString(DisplayFormat);
        return $"return 'TypedResults.Ok(...)', typed 'Ok<{dto}>', or declare 'Results<Ok<{dto}>, NotFound>' as the return type "
            + "of the endpoint when it has several responses";
    }

    // The declared return type of a lambda, its arrow otherwise; the method group where it is mapped.
    private static Location ResultLocation(SyntaxNode handler) => handler switch
    {
        ParenthesizedLambdaExpressionSyntax { ReturnType: { } returnType } => returnType.GetLocation(),
        LambdaExpressionSyntax lambda => lambda.ArrowToken.GetLocation(),
        AnonymousMethodExpressionSyntax method => method.DelegateKeyword.GetLocation(),
        _ => handler.GetLocation()
    };

    // The methods that the type '<Module>Module' of each module calls, and the methods of slices that map an endpoint:
    // a method of a slice that its own module never calls maps endpoints nobody exposes.
    private sealed class Registrations(SliceMap slices)
    {
        private readonly ConcurrentDictionary<ISymbol, ConcurrentDictionary<string, byte>> _calledByModules = new(SymbolEqualityComparer.Default);
        private readonly ConcurrentQueue<(ISymbol Method, string Module, Diagnostic Diagnostic)> _expected = new();

        public void RecordCall(ISymbol? caller, IMethodSymbol method)
        {
            if (ModuleOf(caller) is { } module)
                _calledByModules.GetOrAdd(Key(method.ReducedFrom ?? method), _ => new ConcurrentDictionary<string, byte>()).TryAdd(module, 0);
        }

        public void Expect(ISymbol method, string module, Diagnostic diagnostic) => _expected.Enqueue((Key(method), module, diagnostic));

        public void ReportUncalled(CompilationAnalysisContext context)
        {
            foreach (var (method, module, diagnostic) in _expected)
            {
                if (!(_calledByModules.TryGetValue(method, out var modules) && modules.ContainsKey(module)))
                    context.ReportDiagnostic(diagnostic);
            }
        }

        // The module whose type '<Module>Module', declared in 'Features.<Module>', contains the caller.
        private string? ModuleOf(ISymbol? caller)
        {
            for (var type = caller as INamedTypeSymbol ?? caller?.ContainingType; type is not null; type = type.ContainingType)
            {
                if (type.ContainingType is null && slices.FeaturesPathOf(type) is { Length: 1 } path && type.Name == $"{path[0]}Module")
                    return path[0];
            }

            return null;
        }

        // A call names the definition of a partial method; the mapping is analyzed in its implementation.
        private static ISymbol Key(ISymbol symbol) =>
            symbol is IMethodSymbol method ? (method.PartialDefinitionPart ?? method).OriginalDefinition : symbol.OriginalDefinition;
    }
}
