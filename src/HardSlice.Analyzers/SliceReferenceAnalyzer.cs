using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace HardSlice.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SliceReferenceAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "HS016";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "A slice uses a type from another slice",
        // One line an agent can act on: offending symbol, both slices, the rule, the expected fix and the rule doc.
        messageFormat: "'{0}' in slice '{1}' uses '{2}' from slice '{3}'. Slices never reference each other. "
            + "Fix: move the shared rule into a domain aggregate, raise a domain event, or use the public contract of module '{4}'. "
            + "Skill: extract-to-domain. Doc: 'docs/rules/HS016.md'.",
        category: "HardSlice.Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: RuleSheet.Uri(DiagnosticId));

    private static readonly SymbolDisplayFormat DisplayFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeContainingType);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        // An auto-generated header must not exempt a slice from the rule.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var slices = new SliceMap(start.Compilation);
            var reported = new ConcurrentDictionary<DiagnosticKey, byte>(DiagnosticKeyComparer.Instance);
            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeName(nodeContext, slices, reported),
                SyntaxKind.IdentifierName,
                SyntaxKind.GenericName);
            start.RegisterOperationAction(
                operationContext => AnalyzeInvocation(operationContext, slices, reported),
                OperationKind.Invocation);
            start.RegisterOperationAction(
                operationContext => AnalyzeObjectCreation(operationContext, slices, reported),
                OperationKind.ObjectCreation);
            start.RegisterOperationAction(
                operationContext => AnalyzeBinaryOperator(operationContext, slices, reported),
                OperationKind.Binary);
            start.RegisterOperationAction(
                operationContext => AnalyzeUnaryOperator(operationContext, slices, reported),
                OperationKind.Unary);
            start.RegisterOperationAction(
                operationContext => AnalyzeIncrementOrDecrement(operationContext, slices, reported),
                OperationKind.Increment,
                OperationKind.Decrement);
            start.RegisterOperationAction(
                operationContext => AnalyzeConversion(operationContext, slices, reported),
                OperationKind.Conversion);
            start.RegisterOperationAction(
                operationContext => AnalyzePropertyReference(operationContext, slices, reported),
                OperationKind.PropertyReference);
            start.RegisterOperationAction(
                operationContext => AnalyzeFieldReference(operationContext, slices, reported),
                OperationKind.FieldReference);
            start.RegisterOperationAction(
                operationContext => AnalyzeEventReference(operationContext, slices, reported),
                OperationKind.EventReference);
            start.RegisterOperationAction(
                operationContext => AnalyzeDeconstruction(operationContext, slices, reported),
                OperationKind.DeconstructionAssignment);
            start.RegisterOperationAction(
                operationContext => AnalyzeDelegateCreation(operationContext, slices, reported),
                OperationKind.DelegateCreation);
        });
    }

    // Each name that binds to another slice is reported once, where it appears. Using directives are skipped
    // because they sit outside any type: the aliased or imported symbols are reported where they are used.
    private static void AnalyzeName(
        SyntaxNodeAnalysisContext context,
        SliceMap slices,
        ConcurrentDictionary<DiagnosticKey, byte> reported)
    {
        // A name of a documentation comment, such as '<see cref="..."/>', is no use of the type: nothing depends on it.
        var name = (SimpleNameSyntax)context.Node;
        if (name.IsVar || name.IsPartOfStructuredTrivia())
            return;

        var sourceType = context.ContainingSymbol as INamedTypeSymbol ?? context.ContainingSymbol?.ContainingType;
        if (sourceType is null || slices.SliceOf(sourceType) is not { } sourceSlice)
            return;

        if (name is IdentifierNameSyntax identifier
            && context.SemanticModel.GetAliasInfo(identifier, context.CancellationToken)?.Target is ITypeSymbol aliasTarget)
        {
            var foreignTypes = FindForeignTypes(aliasTarget, sourceSlice, slices);
            foreach (var foreignType in foreignTypes)
            {
                Report(
                    identifier.Identifier.GetLocation(),
                    sourceType,
                    sourceSlice,
                    foreignType.Type,
                    foreignType.Slice,
                    reported,
                    context.ReportDiagnostic);
            }

            if (!foreignTypes.IsEmpty)
                return;
        }

        var symbolInfo = context.SemanticModel.GetSymbolInfo(name, context.CancellationToken);
        var target = ReferencedSymbol(symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault());
        if (target is null || slices.SliceOf(target) is not { } targetSlice || targetSlice == sourceSlice)
            return;

        if (QualifierNamesTypeOfSlice(name, targetSlice, context, slices))
            return;

        Report(
            name.Identifier.GetLocation(),
            sourceType,
            sourceSlice,
            target,
            targetSlice,
            reported,
            context.ReportDiagnostic);
    }

    private static ISymbol? ReferencedSymbol(ISymbol? symbol) => symbol switch
    {
        INamedTypeSymbol { TypeKind: TypeKind.Error } => null,
        INamedTypeSymbol type => type.OriginalDefinition,
        IMethodSymbol { MethodKind: MethodKind.Constructor } constructor => constructor.ContainingType.OriginalDefinition,
        IMethodSymbol method => (method.ReducedFrom ?? method).OriginalDefinition,
        IPropertySymbol or IFieldSymbol or IEventSymbol => symbol.OriginalDefinition,
        _ => null
    };

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        SliceMap slices,
        ConcurrentDictionary<DiagnosticKey, byte> reported)
    {
        if (IsWithinNameOf(context.Operation))
            return;

        var operation = (IInvocationOperation)context.Operation;
        var method = (operation.TargetMethod.ReducedFrom ?? operation.TargetMethod).OriginalDefinition;
        AnalyzeContractResult(context, operation.Type, method, slices, reported);
        AnalyzeContractArguments(context, operation.Arguments, method, slices, reported);
    }

    private static void AnalyzeObjectCreation(
        OperationAnalysisContext context,
        SliceMap slices,
        ConcurrentDictionary<DiagnosticKey, byte> reported)
    {
        if (IsWithinNameOf(context.Operation))
            return;

        var operation = (IObjectCreationOperation)context.Operation;
        if (operation.Constructor is { } constructor)
            AnalyzeContractArguments(context, operation.Arguments, constructor.OriginalDefinition, slices, reported);

        if (operation.Syntax is not ImplicitObjectCreationExpressionSyntax creation
            || !TryGetSource(context.ContainingSymbol, slices, out var sourceType, out var sourceSlice))
        {
            return;
        }

        foreach (var target in FindForeignTypes(operation.Type, sourceSlice, slices))
        {
            Report(
                creation.NewKeyword.GetLocation(),
                sourceType,
                sourceSlice,
                target.Type,
                target.Slice,
                reported,
                context.ReportDiagnostic);
        }
    }

    private static void AnalyzeBinaryOperator(
        OperationAnalysisContext context,
        SliceMap slices,
        ConcurrentDictionary<DiagnosticKey, byte> reported) =>
        AnalyzeOperator(context, ((IBinaryOperation)context.Operation).OperatorMethod, slices, reported);

    private static void AnalyzeUnaryOperator(
        OperationAnalysisContext context,
        SliceMap slices,
        ConcurrentDictionary<DiagnosticKey, byte> reported) =>
        AnalyzeOperator(context, ((IUnaryOperation)context.Operation).OperatorMethod, slices, reported);

    private static void AnalyzeIncrementOrDecrement(
        OperationAnalysisContext context,
        SliceMap slices,
        ConcurrentDictionary<DiagnosticKey, byte> reported) =>
        AnalyzeOperator(context, ((IIncrementOrDecrementOperation)context.Operation).OperatorMethod, slices, reported);

    private static void AnalyzeConversion(
        OperationAnalysisContext context,
        SliceMap slices,
        ConcurrentDictionary<DiagnosticKey, byte> reported) =>
        AnalyzeOperator(context, ((IConversionOperation)context.Operation).OperatorMethod, slices, reported);

    private static void AnalyzeOperator(
        OperationAnalysisContext context,
        IMethodSymbol? operatorMethod,
        SliceMap slices,
        ConcurrentDictionary<DiagnosticKey, byte> reported)
    {
        if (IsWithinNameOf(context.Operation)
            || operatorMethod?.ContainingType is not { } declaringType
            || !TryGetSource(context.ContainingSymbol, slices, out var sourceType, out var sourceSlice))
        {
            return;
        }

        var target = declaringType.OriginalDefinition;
        if (slices.SliceOf(target) is not { } targetSlice || targetSlice == sourceSlice)
            return;

        var semanticModel = context.Operation.SemanticModel;
        if (semanticModel is not null
            && ContainsExplicitReference(context.Operation.Syntax, target, semanticModel, context.CancellationToken))
        {
            return;
        }

        Report(
            OperationLocation(context.Operation.Syntax),
            sourceType,
            sourceSlice,
            target,
            targetSlice,
            reported,
            context.ReportDiagnostic);
    }

    private static void AnalyzePropertyReference(
        OperationAnalysisContext context,
        SliceMap slices,
        ConcurrentDictionary<DiagnosticKey, byte> reported)
    {
        if (IsWithinNameOf(context.Operation))
            return;

        var operation = (IPropertyReferenceOperation)context.Operation;
        var property = operation.Property.OriginalDefinition;
        if (!TryGetSource(context.ContainingSymbol, slices, out var sourceType, out var sourceSlice))
            return;

        if (property.IsIndexer
            && property.ContainingType is { } declaringType
            && slices.SliceOf(declaringType.OriginalDefinition) is { } targetSlice
            && targetSlice != sourceSlice)
        {
            var target = declaringType.OriginalDefinition;
            var semanticModel = context.Operation.SemanticModel;
            if (semanticModel is null
                || !ContainsExplicitReference(context.Operation.Syntax, target, semanticModel, context.CancellationToken))
            {
                Report(
                    OperationLocation(context.Operation.Syntax),
                    sourceType,
                    sourceSlice,
                    target,
                    targetSlice,
                    reported,
                    context.ReportDiagnostic);
            }

            return;
        }

        AnalyzeContractResult(context, operation.Type, property, slices, reported);
        AnalyzeContractArguments(context, operation.Arguments, property, slices, reported);
    }

    private static void AnalyzeFieldReference(
        OperationAnalysisContext context,
        SliceMap slices,
        ConcurrentDictionary<DiagnosticKey, byte> reported)
    {
        if (IsWithinNameOf(context.Operation))
            return;

        var operation = (IFieldReferenceOperation)context.Operation;
        AnalyzeContractResult(context, operation.Type, operation.Field.OriginalDefinition, slices, reported);
    }

    private static void AnalyzeEventReference(
        OperationAnalysisContext context,
        SliceMap slices,
        ConcurrentDictionary<DiagnosticKey, byte> reported)
    {
        if (IsWithinNameOf(context.Operation))
            return;

        var operation = (IEventReferenceOperation)context.Operation;
        AnalyzeContractResult(context, operation.Event.Type, operation.Event.OriginalDefinition, slices, reported);
    }

    private static void AnalyzeDeconstruction(
        OperationAnalysisContext context,
        SliceMap slices,
        ConcurrentDictionary<DiagnosticKey, byte> reported)
    {
        if (IsWithinNameOf(context.Operation)
            || !TryGetSource(context.ContainingSymbol, slices, out var sourceType, out var sourceSlice))
        {
            return;
        }

        var operation = (IDeconstructionAssignmentOperation)context.Operation;
        var typesAlreadyExposedByValue = new HashSet<INamedTypeSymbol>(
            FindForeignTypes(operation.Value.Type, sourceSlice, slices).Select(target => target.Type),
            SymbolEqualityComparer.Default);
        var semanticModel = context.Operation.SemanticModel;
        foreach (var target in FindForeignTypes(context.Operation.Type, sourceSlice, slices))
        {
            if (typesAlreadyExposedByValue.Contains(target.Type)
                || semanticModel is not null
                && ContainsExplicitReference(context.Operation.Syntax, target.Type, semanticModel, context.CancellationToken))
            {
                continue;
            }

            Report(
                OperationLocation(context.Operation.Syntax),
                sourceType,
                sourceSlice,
                target.Type,
                target.Slice,
                reported,
                context.ReportDiagnostic);
        }
    }

    private static void AnalyzeDelegateCreation(
        OperationAnalysisContext context,
        SliceMap slices,
        ConcurrentDictionary<DiagnosticKey, byte> reported)
    {
        if (IsWithinNameOf(context.Operation))
            return;

        var operation = (IDelegateCreationOperation)context.Operation;
        if (operation.Target is not IMethodReferenceOperation methodReference)
            return;

        var method = (methodReference.Method.ReducedFrom ?? methodReference.Method).OriginalDefinition;
        AnalyzeContractResult(context, operation.Type, method, slices, reported);
    }

    private static void AnalyzeContractResult(
        OperationAnalysisContext context,
        ITypeSymbol? resultType,
        ISymbol member,
        SliceMap slices,
        ConcurrentDictionary<DiagnosticKey, byte> reported)
    {
        if (!TryGetSource(context.ContainingSymbol, slices, out var sourceType, out var sourceSlice)
            || !IsContractMember(member, sourceType, slices))
        {
            return;
        }

        var semanticModel = context.Operation.SemanticModel;
        foreach (var target in FindForeignTypes(resultType, sourceSlice, slices))
        {
            if (semanticModel is not null
                && ContainsExplicitReference(context.Operation.Syntax, target.Type, semanticModel, context.CancellationToken))
            {
                continue;
            }

            Report(
                OperationLocation(context.Operation.Syntax),
                sourceType,
                sourceSlice,
                target.Type,
                target.Slice,
                reported,
                context.ReportDiagnostic);
        }
    }

    private static void AnalyzeContractArguments(
        OperationAnalysisContext context,
        ImmutableArray<IArgumentOperation> arguments,
        ISymbol member,
        SliceMap slices,
        ConcurrentDictionary<DiagnosticKey, byte> reported)
    {
        if (!TryGetSource(context.ContainingSymbol, slices, out var sourceType, out var sourceSlice)
            || !IsContractMember(member, sourceType, slices))
        {
            return;
        }

        var semanticModel = context.Operation.SemanticModel;
        foreach (var argument in arguments)
        {
            ReportArgumentTypes(argument.Parameter?.Type);
            ReportArgumentTypes(argument.Value.Type);

            void ReportArgumentTypes(ITypeSymbol? argumentType)
            {
                foreach (var target in FindForeignTypes(argumentType, sourceSlice, slices))
                {
                    if (semanticModel is not null
                        && ContainsExplicitReference(argument.Syntax, target.Type, semanticModel, context.CancellationToken))
                    {
                        continue;
                    }

                    Report(
                        ArgumentLocation(argument),
                        sourceType,
                        sourceSlice,
                        target.Type,
                        target.Slice,
                        reported,
                        context.ReportDiagnostic);
                }
            }
        }
    }

    private static bool IsContractMember(ISymbol member, INamedTypeSymbol sourceType, SliceMap slices) =>
        SymbolEqualityComparer.Default.Equals(member.ContainingAssembly, sourceType.ContainingAssembly)
        && slices.SliceOf(member) is null;

    private static bool TryGetSource(
        ISymbol? containingSymbol,
        SliceMap slices,
        out INamedTypeSymbol sourceType,
        out string sourceSlice)
    {
        sourceType = containingSymbol as INamedTypeSymbol ?? containingSymbol?.ContainingType!;
        if (sourceType is not null && slices.SliceOf(sourceType) is { } slice)
        {
            sourceSlice = slice;
            return true;
        }

        sourceType = null!;
        sourceSlice = null!;
        return false;
    }

    private static ImmutableArray<ForeignType> FindForeignTypes(
        ITypeSymbol? type,
        string sourceSlice,
        SliceMap slices)
    {
        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var targets = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var result = ImmutableArray.CreateBuilder<ForeignType>();
        CollectForeignTypes(type, sourceSlice, slices, visited, targets, result);
        return result.ToImmutable();
    }

    private static void CollectForeignTypes(
        ITypeSymbol? type,
        string sourceSlice,
        SliceMap slices,
        HashSet<ITypeSymbol> visited,
        HashSet<INamedTypeSymbol> targets,
        ImmutableArray<ForeignType>.Builder result)
    {
        if (type is null || !visited.Add(type))
            return;

        switch (type)
        {
            case INamedTypeSymbol { TypeKind: not TypeKind.Error } named:
                var definition = named.OriginalDefinition;
                if (slices.SliceOf(definition) is { } slice && slice != sourceSlice && targets.Add(definition))
                    result.Add(new ForeignType(definition, slice));

                CollectForeignTypes(named.ContainingType, sourceSlice, slices, visited, targets, result);

                foreach (var argument in named.TypeArguments)
                    CollectForeignTypes(argument, sourceSlice, slices, visited, targets, result);

                if (named.TypeKind == TypeKind.Delegate && named.DelegateInvokeMethod is { } invokeMethod)
                {
                    CollectForeignTypes(invokeMethod.ReturnType, sourceSlice, slices, visited, targets, result);
                    foreach (var parameter in invokeMethod.Parameters)
                        CollectForeignTypes(parameter.Type, sourceSlice, slices, visited, targets, result);
                }

                break;

            case IArrayTypeSymbol array:
                CollectForeignTypes(array.ElementType, sourceSlice, slices, visited, targets, result);
                break;

            case IPointerTypeSymbol pointer:
                CollectForeignTypes(pointer.PointedAtType, sourceSlice, slices, visited, targets, result);
                break;

            case IFunctionPointerTypeSymbol functionPointer:
                CollectForeignTypes(functionPointer.Signature.ReturnType, sourceSlice, slices, visited, targets, result);

                foreach (var parameter in functionPointer.Signature.Parameters)
                    CollectForeignTypes(parameter.Type, sourceSlice, slices, visited, targets, result);

                break;
        }
    }

    private static bool ContainsExplicitReference(
        SyntaxNode syntax,
        INamedTypeSymbol target,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        foreach (var name in syntax.DescendantNodesAndSelf().OfType<SimpleNameSyntax>())
        {
            if (name.IsVar)
                continue;

            if (name is IdentifierNameSyntax identifier
                && semanticModel.GetAliasInfo(identifier, cancellationToken)?.Target is ITypeSymbol aliasTarget
                && ContainsType(aliasTarget, target))
            {
                return true;
            }

            var symbolInfo = semanticModel.GetSymbolInfo(name, cancellationToken);
            if (ReferencedSymbol(symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault()) is { } referenced
                && DeclaringType(referenced) is { } declaringType
                && SymbolEqualityComparer.Default.Equals(declaringType.OriginalDefinition, target.OriginalDefinition))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsType(ITypeSymbol? type, INamedTypeSymbol target)
    {
        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        return ContainsType(type, target.OriginalDefinition, visited);
    }

    private static bool ContainsType(
        ITypeSymbol? type,
        INamedTypeSymbol target,
        HashSet<ITypeSymbol> visited)
    {
        if (type is null || !visited.Add(type))
            return false;

        return type switch
        {
            INamedTypeSymbol named =>
                SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, target)
                || ContainsType(named.ContainingType, target, visited)
                || named.TypeArguments.Any(argument => ContainsType(argument, target, visited))
                || DelegateContainsType(named, target, visited),
            IArrayTypeSymbol array => ContainsType(array.ElementType, target, visited),
            IPointerTypeSymbol pointer => ContainsType(pointer.PointedAtType, target, visited),
            IFunctionPointerTypeSymbol functionPointer =>
                ContainsType(functionPointer.Signature.ReturnType, target, visited)
                || functionPointer.Signature.Parameters.Any(parameter => ContainsType(parameter.Type, target, visited)),
            _ => false
        };
    }

    private static bool DelegateContainsType(
        INamedTypeSymbol type,
        INamedTypeSymbol target,
        HashSet<ITypeSymbol> visited) =>
        type.TypeKind == TypeKind.Delegate
        && type.DelegateInvokeMethod is { } invokeMethod
        && (ContainsType(invokeMethod.ReturnType, target, visited)
            || invokeMethod.Parameters.Any(parameter => ContainsType(parameter.Type, target, visited)));

    private static INamedTypeSymbol? DeclaringType(ISymbol symbol) => symbol switch
    {
        INamedTypeSymbol type => type,
        IMethodSymbol method => method.ContainingType,
        IPropertySymbol property => property.ContainingType,
        IFieldSymbol field => field.ContainingType,
        IEventSymbol @event => @event.ContainingType,
        _ => null
    };

    private static Location OperationLocation(SyntaxNode syntax) => syntax switch
    {
        InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess } =>
            memberAccess.Name.Identifier.GetLocation(),
        InvocationExpressionSyntax { Expression: MemberBindingExpressionSyntax memberBinding } =>
            memberBinding.Name.Identifier.GetLocation(),
        InvocationExpressionSyntax { Expression: SimpleNameSyntax name } => name.Identifier.GetLocation(),
        MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.GetLocation(),
        ElementAccessExpressionSyntax elementAccess => elementAccess.GetLocation(),
        ImplicitObjectCreationExpressionSyntax creation => creation.NewKeyword.GetLocation(),
        BinaryExpressionSyntax binary => binary.OperatorToken.GetLocation(),
        AssignmentExpressionSyntax assignment => assignment.OperatorToken.GetLocation(),
        PrefixUnaryExpressionSyntax prefix => prefix.OperatorToken.GetLocation(),
        PostfixUnaryExpressionSyntax postfix => postfix.OperatorToken.GetLocation(),
        _ => syntax.GetLocation()
    };

    private static Location ArgumentLocation(IArgumentOperation argument)
    {
        var argumentSyntax = argument.Syntax.FirstAncestorOrSelf<ArgumentSyntax>()
            ?? argument.Value.Syntax.FirstAncestorOrSelf<ArgumentSyntax>();
        if (argumentSyntax is { RefKindKeyword.RawKind: not 0 }
            || argumentSyntax?.DescendantNodesAndSelf().Any(node => node.IsKind(SyntaxKind.SuppressNullableWarningExpression)) == true)
        {
            return argumentSyntax.GetLocation();
        }

        return OperationLocation(argument.Value.Syntax);
    }

    private static bool IsWithinNameOf(IOperation operation)
    {
        for (var current = operation; current is not null; current = current.Parent)
        {
            if (current is INameOfOperation)
                return true;
        }

        return false;
    }

    private static void Report(
        Location location,
        INamedTypeSymbol sourceType,
        string sourceSlice,
        ISymbol target,
        string targetSlice,
        ConcurrentDictionary<DiagnosticKey, byte> reported,
        Action<Diagnostic> reportDiagnostic)
    {
        var identity = target.OriginalDefinition;
        if (location.SourceTree is not { } tree
            || !reported.TryAdd(new DiagnosticKey(tree, location.SourceSpan, identity), 0))
        {
            return;
        }

        reportDiagnostic(Diagnostic.Create(
            Rule,
            location,
            sourceType.ToDisplayString(DisplayFormat),
            sourceSlice,
            target.ToDisplayString(DisplayFormat),
            targetSlice,
            SliceMap.ModuleOf(targetSlice)));
    }

    // In 'ConfirmBookingHandler.Create()' or 'ConfirmBookingHandler.Result', the qualifier is already reported.
    private static bool QualifierNamesTypeOfSlice(SimpleNameSyntax name, string slice, SyntaxNodeAnalysisContext context, SliceMap slices)
    {
        var qualifier = name.Parent switch
        {
            MemberAccessExpressionSyntax memberAccess when memberAccess.Name == name => memberAccess.Expression,
            QualifiedNameSyntax qualifiedName when qualifiedName.Right == name => qualifiedName.Left,
            _ => null
        };

        return qualifier is not null
            && context.SemanticModel.GetSymbolInfo(qualifier, context.CancellationToken).Symbol is INamedTypeSymbol qualifierType
            && slices.SliceOf(qualifierType.OriginalDefinition) == slice;
    }

    private readonly struct DiagnosticKey(SyntaxTree tree, TextSpan span, ISymbol target)
    {
        public SyntaxTree Tree { get; } = tree;

        public TextSpan Span { get; } = span;

        public ISymbol Target { get; } = target;
    }

    private readonly struct ForeignType(INamedTypeSymbol type, string slice)
    {
        public INamedTypeSymbol Type { get; } = type;

        public string Slice { get; } = slice;
    }

    private sealed class DiagnosticKeyComparer : IEqualityComparer<DiagnosticKey>
    {
        public static DiagnosticKeyComparer Instance { get; } = new();

        public bool Equals(DiagnosticKey x, DiagnosticKey y) =>
            ReferenceEquals(x.Tree, y.Tree)
            && x.Span == y.Span
            && SymbolEqualityComparer.Default.Equals(x.Target, y.Target);

        public int GetHashCode(DiagnosticKey key)
        {
            unchecked
            {
                var hash = RuntimeHelpers.GetHashCode(key.Tree);
                hash = (hash * 397) ^ key.Span.GetHashCode();
                hash = (hash * 397) ^ SymbolEqualityComparer.Default.GetHashCode(key.Target);
                return hash;
            }
        }
    }
}
