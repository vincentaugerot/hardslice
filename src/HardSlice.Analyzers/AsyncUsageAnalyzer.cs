using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace HardSlice.Analyzers;

// HS011: code never blocks on asynchronous work and never declares async void. The blocking members are recognized by their
// symbols in the base class library, never by their names: 'Result' of Task<TResult> and ValueTask<TResult>, the Wait
// methods of Task, and an explicit GetResult() on a task awaiter. Async void is a method, a local function, a lambda or an
// anonymous method that is async and returns void. Applies to every compilation, the domain included. Not applied to test projects.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AsyncUsageAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "HS011";

    private const string BlockingRule = "Code never blocks on asynchronous work.";

    private const string AsyncVoidRule = "Asynchronous code returns a task its caller awaits: nobody can await async void, and its exceptions crash the process.";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Code blocks on asynchronous work or declares async void",
        messageFormat: "{0}. {1} Fix: {2}. Skill: fix-architecture-violation. Doc: 'docs/rules/HS011.md'.",
        category: "HardSlice.Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: RuleSheet.Uri(DiagnosticId));

    private static readonly SymbolDisplayFormat DisplayFormat = ContainingMember.DisplayFormat;

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        // An auto-generated header must not exempt code from the rule.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (RuleActivation.IsTestProject(start.Options))
                return;

            if (BlockingMembers.Create(start.Compilation) is { } blocking)
            {
                start.RegisterOperationAction(
                    operationContext => AnalyzeBlockingMember(operationContext, blocking),
                    OperationKind.PropertyReference,
                    OperationKind.Invocation,
                    OperationKind.MethodReference);
            }

            start.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
            start.RegisterOperationAction(AnalyzeLocalFunction, OperationKind.LocalFunction);
            start.RegisterOperationAction(AnalyzeAnonymousFunction, OperationKind.AnonymousFunction);
        });
    }

    private static void AnalyzeBlockingMember(OperationAnalysisContext context, BlockingMembers blocking)
    {
        var member = context.Operation switch
        {
            IPropertyReferenceOperation property => (ISymbol)property.Property,
            IInvocationOperation invocation => invocation.TargetMethod,
            IMethodReferenceOperation reference => reference.Method,
            _ => null
        };
        if (member is null || !blocking.Contains(member) || ContainingMember.IsWithinNameOf(context.Operation))
            return;

        var fix = member.Name switch
        {
            "WaitAll" => "make the caller async and await 'Task.WhenAll' with the same tasks",
            "WaitAny" => "make the caller async and await 'Task.WhenAny' with the same tasks",
            "Wait" when member is IMethodSymbol { Parameters.Length: > 0 } => "make the caller async and await 'WaitAsync' with the same timeout or token",
            _ => "make the caller async and await the task"
        };

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            Location(context.Operation.Syntax),
            $"{ContainingMember.Describe(context.ContainingSymbol)} blocks on '{member.OriginalDefinition.ToDisplayString(DisplayFormat)}'",
            BlockingRule,
            fix));
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;
        // A partial method is analyzed once, through its definition, on the part that is async.
        if (method.PartialDefinitionPart is not null)
            return;

        var implementation = method.PartialImplementationPart ?? method;
        if (!implementation.IsAsync || !implementation.ReturnsVoid || implementation.Locations.FirstOrDefault() is not { IsInSource: true } location)
            return;

        // The signature of an override or an implementation is imposed by the member it overrides or implements.
        var imposedBy = method.OverriddenMethod ?? ImplementedMember(method);
        var fix = imposedBy is null
            ? "return Task instead of void, and await it where it is called"
            : $"make it synchronous, or, if the application declares '{imposedBy.OriginalDefinition.ToDisplayString(DisplayFormat)}', "
                + "make both return Task and await it where it is called";

        context.ReportDiagnostic(Diagnostic.Create(Rule, location, $"{ContainingMember.Describe(method)} is async void", AsyncVoidRule, fix));
    }

    private static void AnalyzeLocalFunction(OperationAnalysisContext context)
    {
        var function = ((ILocalFunctionOperation)context.Operation).Symbol;
        if (!function.IsAsync || !function.ReturnsVoid)
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            context.Operation.Syntax is LocalFunctionStatementSyntax syntax ? syntax.Identifier.GetLocation() : context.Operation.Syntax.GetLocation(),
            $"local function '{function.Name}' of {ContainingMember.Describe(context.ContainingSymbol)} is async void",
            AsyncVoidRule,
            "return Task instead of void, and await it where it is called"));
    }

    // 'list.ForEach(async item => ...)': the lambda is converted to Action<T>, whose Invoke returns void.
    private static void AnalyzeAnonymousFunction(OperationAnalysisContext context)
    {
        var operation = (IAnonymousFunctionOperation)context.Operation;
        if (!operation.Symbol.IsAsync || !operation.Symbol.ReturnsVoid)
            return;

        var creation = operation.Parent as IDelegateCreationOperation;
        var delegateType = creation?.Type as INamedTypeSymbol;
        var function = operation.Syntax is AnonymousMethodExpressionSyntax ? "an async anonymous method" : "an async lambda";
        var converted = delegateType is null ? "" : $": it is converted to '{delegateType.ToDisplayString(DisplayFormat)}', which returns void";

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            operation.Syntax is AnonymousFunctionExpressionSyntax { AsyncKeyword.RawKind: not 0 } syntax ? syntax.AsyncKeyword.GetLocation() : operation.Syntax.GetLocation(),
            $"{function} of {ContainingMember.Describe(context.ContainingSymbol)} is async void{converted}",
            AsyncVoidRule,
            AnonymousFunctionFix(creation, operation.Symbol)));
    }

    private static string AnonymousFunctionFix(IDelegateCreationOperation? creation, IMethodSymbol function)
    {
        IOperation? user = creation?.Parent;
        while (user is IConversionOperation conversion)
            user = conversion.Parent;

        if (user is IArgumentOperation { Parent: IInvocationOperation call } && call.TargetMethod.Name == "ForEach")
            return $"replace '{call.TargetMethod.OriginalDefinition.ToDisplayString(DisplayFormat)}' with a foreach loop that awaits each call";

        if (user is IEventAssignmentOperation)
            return "subscribe a synchronous handler, and hand the asynchronous work to code that awaits it";

        var parameters = function.Parameters.Select(parameter => parameter.Type.ToDisplayString(DisplayFormat)).Append("Task");
        return $"give it a delegate type that returns Task, such as 'Func<{string.Join(", ", parameters)}>', and await the task it returns";
    }

    private static ISymbol? ImplementedMember(IMethodSymbol method)
    {
        if (!method.ExplicitInterfaceImplementations.IsEmpty)
            return method.ExplicitInterfaceImplementations[0];

        foreach (var @interface in method.ContainingType.AllInterfaces)
        {
            foreach (var member in @interface.GetMembers(method.Name).OfType<IMethodSymbol>())
            {
                if (SymbolEqualityComparer.Default.Equals(method.ContainingType.FindImplementationForInterfaceMember(member), method))
                    return member;
            }
        }

        return null;
    }

    // The name of the member: 'Result' in 'task.Result', 'Wait' in 'task.Wait()', 'GetResult' in 'awaiter.GetResult()'.
    private static Location Location(SyntaxNode syntax) => syntax switch
    {
        InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess } => memberAccess.Name.Identifier.GetLocation(),
        InvocationExpressionSyntax { Expression: MemberBindingExpressionSyntax memberBinding } => memberBinding.Name.Identifier.GetLocation(),
        InvocationExpressionSyntax { Expression: SimpleNameSyntax name } => name.Identifier.GetLocation(),
        MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.GetLocation(),
        MemberBindingExpressionSyntax memberBinding => memberBinding.Name.Identifier.GetLocation(),
        _ => syntax.GetLocation()
    };

    // The members that block the calling thread until a task completes.
    private sealed class BlockingMembers(ImmutableHashSet<ISymbol> properties, INamedTypeSymbol? task, ImmutableHashSet<INamedTypeSymbol> awaiters)
    {
        private static readonly string[] AwaiterMetadataNames =
        [
            "System.Runtime.CompilerServices.TaskAwaiter",
            "System.Runtime.CompilerServices.TaskAwaiter`1",
            "System.Runtime.CompilerServices.ValueTaskAwaiter",
            "System.Runtime.CompilerServices.ValueTaskAwaiter`1",
            "System.Runtime.CompilerServices.ConfiguredTaskAwaitable+ConfiguredTaskAwaiter",
            "System.Runtime.CompilerServices.ConfiguredTaskAwaitable`1+ConfiguredTaskAwaiter",
            "System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable+ConfiguredValueTaskAwaiter",
            "System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable`1+ConfiguredValueTaskAwaiter"
        ];

        public static BlockingMembers? Create(Compilation compilation)
        {
            var properties = new[] { "System.Threading.Tasks.Task`1", "System.Threading.Tasks.ValueTask`1" }
                .Select(compilation.GetTypeByMetadataName)
                .OfType<INamedTypeSymbol>()
                .SelectMany(type => type.GetMembers("Result").OfType<IPropertySymbol>())
                .ToImmutableHashSet<ISymbol>(SymbolEqualityComparer.Default);
            var task = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
            var awaiters = AwaiterMetadataNames
                .Select(compilation.GetTypeByMetadataName)
                .OfType<INamedTypeSymbol>()
                .ToImmutableHashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            return properties.IsEmpty && task is null && awaiters.IsEmpty ? null : new BlockingMembers(properties, task, awaiters);
        }

        public bool Contains(ISymbol member) => member switch
        {
            IPropertySymbol property => properties.Contains(property.OriginalDefinition),
            IMethodSymbol { Name: "Wait" or "WaitAll" or "WaitAny" } method => SymbolEqualityComparer.Default.Equals(method.ContainingType.OriginalDefinition, task),
            IMethodSymbol { Name: "GetResult" } method => awaiters.Contains(method.ContainingType.OriginalDefinition),
            _ => false
        };
    }
}
