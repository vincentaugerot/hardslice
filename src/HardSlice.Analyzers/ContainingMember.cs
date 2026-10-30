using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace HardSlice.Analyzers;

// Names the member that holds the code a diagnostic points at.
internal static class ContainingMember
{
    public static readonly SymbolDisplayFormat DisplayFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeContainingType,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    // 'ConfirmBookingHandler.Handle', or the constructor, the property or the top-level statements the code is in.
    public static string Describe(ISymbol? symbol) => symbol switch
    {
        IMethodSymbol { MethodKind: MethodKind.Constructor } constructor => $"the constructor of '{constructor.ContainingType.ToDisplayString(DisplayFormat)}'",
        IMethodSymbol { MethodKind: MethodKind.StaticConstructor } constructor => $"the static constructor of '{constructor.ContainingType.ToDisplayString(DisplayFormat)}'",
        IMethodSymbol { MethodKind: MethodKind.PropertyGet or MethodKind.PropertySet or MethodKind.EventAdd or MethodKind.EventRemove, AssociatedSymbol: { } associated } =>
            $"'{associated.ToDisplayString(DisplayFormat)}'",
        IMethodSymbol { Name: WellKnownMemberNames.TopLevelStatementsEntryPointMethodName } entryPoint =>
            $"the top-level statements of '{entryPoint.ContainingType.ToDisplayString(DisplayFormat)}'",
        null => "the code",
        _ => $"'{symbol.ToDisplayString(DisplayFormat)}'"
    };

    public static bool IsWithinNameOf(IOperation operation)
    {
        for (var current = operation.Parent; current is not null; current = current.Parent)
        {
            if (current is INameOfOperation)
                return true;
        }

        return false;
    }
}
