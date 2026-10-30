using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace HardSlice.Analyzers;

// The endpoints of ASP.NET Core as the endpoint rules (HS003, HS015) read them: a call to a mapping method, recognized by
// its symbol, never by its name, and the endpoint named by its HTTP method and its route.
internal static class EndpointMapping
{
    public static readonly SymbolDisplayFormat DisplayFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeContainingType,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

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

    public static string? HttpMethodOf(IInvocationOperation mapping) => MappingMethods[mapping.TargetMethod.Name];

    // The delegate argument of a mapping, the endpoint itself.
    public static IArgumentOperation? HandlerArgument(IInvocationOperation mapping) =>
        mapping.Arguments.LastOrDefault(argument => argument.Parameter?.Type.TypeKind == TypeKind.Delegate
            || argument.Parameter?.Type.SpecialType == SpecialType.System_Delegate);

    // 'POST /bookings/{id}' for a constant pattern, prefixed by the constant prefixes of the groups mapped in the same body.
    public static string Describe(IInvocationOperation mapping, Routing routing, CancellationToken cancellationToken)
    {
        var pattern = mapping.Arguments.FirstOrDefault(argument => argument.Parameter?.Name == "pattern")?.Value;
        var route = pattern is null
            ? null
            : Combine(GroupPrefix(Argument(mapping, 0), routing, depth: 0, cancellationToken), Pattern(pattern));

        var verb = mapping.TargetMethod.Name switch
        {
            "MapFallback" => "fallback",
            "MapMethods" => string.Join(",", HttpMethods(mapping)) is { Length: > 0 } methods ? methods : "MapMethods",
            _ => HttpMethodOf(mapping)
        };

        return verb is null ? route ?? "" : route is null ? verb : $"{verb} {route}";
    }

    // Where an endpoint is mapped: "slice 'Bookings/GetBooking'", or the type that maps it, such as "'Program'".
    public static string PlaceOf(ISymbol? container, SliceMap slices) =>
        container is not null && slices.SliceOf(container) is { } slice
            ? $"slice '{slice}'"
            : $"'{(container as INamedTypeSymbol ?? container?.ContainingType)?.ToDisplayString(DisplayFormat)}'";

    // Arguments are in evaluation order: named arguments may come in any order.
    public static IOperation? Argument(IInvocationOperation invocation, int ordinal) =>
        invocation.Arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == ordinal)?.Value;

    public static IOperation WithoutConversions(IOperation operation)
    {
        while (operation is IConversionOperation conversion)
            operation = conversion.Operand;

        return operation;
    }

    public static Location OperationLocation(SyntaxNode syntax) => syntax switch
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

    private static IEnumerable<string> HttpMethods(IInvocationOperation mapping) =>
        mapping.Arguments.FirstOrDefault(argument => argument.Parameter?.Name == "httpMethods") is { } methods
            ? methods.Value.DescendantsAndSelf()
                .Where(operation => operation.ConstantValue is { HasValue: true, Value: string })
                .Select(operation => (string)operation.ConstantValue.Value!)
            : [];

    private static string Pattern(IOperation pattern) =>
        pattern.ConstantValue is { HasValue: true, Value: string constant } ? constant : pattern.Syntax.ToString();

    private static string GroupPrefix(IOperation? receiver, Routing routing, int depth, CancellationToken cancellationToken)
    {
        receiver = receiver is null ? null : WithoutConversions(receiver);
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

    // The mapping methods of ASP.NET Core, when the compilation references its routing.
    internal sealed class Routing(INamedTypeSymbol extensions, INamedTypeSymbol? fallbackExtensions, INamedTypeSymbol? groupBuilder)
    {
        public static Routing? Create(Compilation compilation) =>
            compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Builder.EndpointRouteBuilderExtensions") is { } extensions
                ? new Routing(
                    extensions,
                    compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Builder.FallbackEndpointRouteBuilderExtensions"),
                    compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Routing.RouteGroupBuilder"))
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

        public bool IsGroupBuilder(ITypeSymbol? type)
        {
            for (var current = type as INamedTypeSymbol; current is not null && groupBuilder is not null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current, groupBuilder))
                    return true;
            }

            return false;
        }
    }
}
