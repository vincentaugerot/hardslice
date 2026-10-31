using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace HardSlice.Analyzers;

// HS000: a suppression of a HardSlice rule written in source carries its justification, '// hardslice-justification: <reason>'
// on its line or on the line above, the reason in at least ten characters that are not spaces. Checked: '#pragma warning
// disable' of an HS identifier or, outside generated code, of every warning, and the SuppressMessage and
// UnconditionalSuppressMessage attributes of an HS identifier, recognized by their symbols, on any target. HS000 itself cannot
// be suppressed. Applied to test projects and to generated code. Not checked: <NoWarn> and the severities of .editorconfig
// and global config files.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SuppressionJustificationAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "HS000";

    // The form of a justification, followed by the reason.
    public const string Justification = "// hardslice-justification: ";

    // The fewest characters of a reason, spaces excluded.
    public const int MinimumReasonLength = 10;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "A HardSlice rule is suppressed without a justification",
        messageFormat: "{0} in '{1}' {2}. A HardSlice rule is suppressed only with a reason a reviewer can read. Fix: {3}. Doc: 'docs/rules/HS000.md'.",
        category: "HardSlice.Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        // HS000 would hide the suppressions it reports. NotConfigurable: no pragma, <NoWarn> or severity setting turns it off.
        // Compiler: the compiler does not apply SuppressMessage attributes to it, which NotConfigurable alone does not prevent.
        customTags: [WellKnownDiagnosticTags.NotConfigurable, WellKnownDiagnosticTags.Compiler]);

    private static readonly ImmutableArray<string> SuppressionAttributeNames =
    [
        "System.Diagnostics.CodeAnalysis.SuppressMessageAttribute",
        "System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessageAttribute"
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        // An auto-generated header must not hide a suppression.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxTreeAction(AnalyzePragmas);
        context.RegisterCompilationStartAction(start =>
        {
            var attributes = SuppressionAttributeNames
                .Select(start.Compilation.GetTypeByMetadataName)
                .OfType<INamedTypeSymbol>()
                .ToImmutableArray();
            if (!attributes.IsEmpty)
                start.RegisterSyntaxNodeAction(nodeContext => AnalyzeAttribute(nodeContext, attributes), SyntaxKind.Attribute);
        });
    }

    private static void AnalyzePragmas(SyntaxTreeAnalysisContext context)
    {
        var root = context.Tree.GetRoot(context.CancellationToken);
        if (!root.ContainsDirectives)
            return;

        for (var directive = root.GetFirstDirective(); directive is not null; directive = directive.GetNextDirective())
        {
            // A directive of an inactive #if region suppresses nothing.
            if (directive is not PragmaWarningDirectiveTriviaSyntax pragma
                || !pragma.IsActive
                || !pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword))
            {
                continue;
            }

            if (pragma.ErrorCodes.Count == 0)
            {
                // Source generators, such as the one of [GeneratedRegex], start their output with it: nobody can justify it there.
                if (context.IsGeneratedCode)
                    continue;

                ReportIfUnjustified(
                    context.ReportDiagnostic,
                    root,
                    pragma.GetLocation(),
                    pragma.Span,
                    "'#pragma warning disable'",
                    "disables every warning, the HardSlice rules included, and ",
                    "name the warnings it disables; for a HardSlice rule, ");
                continue;
            }

            // Numeric codes are compiler warnings. Identifiers of other analyzers are not checked.
            foreach (var code in pragma.ErrorCodes)
            {
                if (code is not IdentifierNameSyntax identifier || !IsRuleId(identifier.Identifier.ValueText))
                    continue;

                var subject = $"'#pragma warning disable {identifier.Identifier.ValueText}'";
                if (identifier.Identifier.ValueText == DiagnosticId)
                    ReportHS000(context.ReportDiagnostic, code.GetLocation(), subject);
                else
                    ReportIfUnjustified(context.ReportDiagnostic, root, code.GetLocation(), pragma.Span, subject, "", "");
            }
        }
    }

    // [SuppressMessage(category, checkId)] on an assembly, a module, a type, a member, a parameter or a return value.
    private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context, ImmutableArray<INamedTypeSymbol> attributes)
    {
        var attribute = (AttributeSyntax)context.Node;
        if (attribute.ArgumentList is not { } arguments
            || context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is not IMethodSymbol constructor
            || !attributes.Contains(constructor.ContainingType, SymbolEqualityComparer.Default))
        {
            return;
        }

        var checkIdArgument = Argument(arguments, "checkId", 1);
        if (checkIdArgument is null
            || context.SemanticModel.GetConstantValue(checkIdArgument.Expression, context.CancellationToken).Value is not string checkId
            || RuleIdOf(checkId) is not { } ruleId)
        {
            return;
        }

        var category = Argument(arguments, "category", 0) is { } categoryArgument
            ? context.SemanticModel.GetConstantValue(categoryArgument.Expression, context.CancellationToken).Value as string
            : null;
        var name = constructor.ContainingType.Name;
        var subject = $"'[{name.Substring(0, name.Length - "Attribute".Length)}(\"{category}\", \"{checkId}\")]'";
        var location = checkIdArgument.Expression.GetLocation();
        if (ruleId == DiagnosticId)
            ReportHS000(context.ReportDiagnostic, location, subject);
        else
            ReportIfUnjustified(context.ReportDiagnostic, attribute.SyntaxTree.GetRoot(context.CancellationToken), location, attribute.Span, subject, "", "");
    }

    // The argument of the constructor parameter at this position, or named after it.
    private static AttributeArgumentSyntax? Argument(AttributeArgumentListSyntax arguments, string parameter, int position)
    {
        if (arguments.Arguments.FirstOrDefault(argument => argument.NameColon?.Name.Identifier.ValueText == parameter) is { } named)
            return named;

        var positional = arguments.Arguments.Where(argument => argument.NameEquals is null && argument.NameColon is null).ToList();
        return position < positional.Count ? positional[position] : null;
    }

    // 'HS016' for 'HS016' and 'HS016:SliceReference': the compiler reads the identifier before the colon, as written.
    private static string? RuleIdOf(string checkId)
    {
        var colon = checkId.IndexOf(':');
        var id = colon < 0 ? checkId : checkId.Substring(0, colon);
        return IsRuleId(id) ? id : null;
    }

    // HS followed by digits. The compiler compares identifiers by their case: 'hs016' suppresses nothing.
    private static bool IsRuleId(string identifier)
    {
        if (identifier.Length < 3 || !identifier.StartsWith("HS", StringComparison.Ordinal))
            return false;

        for (var i = 2; i < identifier.Length; i++)
        {
            if (identifier[i] is < '0' or > '9')
                return false;
        }

        return true;
    }

    private static void ReportHS000(Action<Diagnostic> report, Location location, string subject) =>
        report(Diagnostic.Create(
            Rule,
            location,
            subject,
            FileName(location),
            "suppresses HS000, which no justification allows",
            "remove it; HS000 reports every suppression of a HardSlice rule that has no justification"));

    private static void ReportIfUnjustified(
        Action<Diagnostic> report,
        SyntaxNode root,
        Location location,
        TextSpan suppression,
        string subject,
        string scope,
        string fixPrefix)
    {
        var reason = ReasonOf(root, suppression);
        if (reason is not null && reason.Count(character => !char.IsWhiteSpace(character)) >= MinimumReasonLength)
            return;

        var (problem, justify) = reason is null
            ? ("has no justification", $"add '{Justification}<reason>' on the line above")
            : ($"has a justification of fewer than {MinimumReasonLength} characters", $"give the reason in at least {MinimumReasonLength} characters after '{Justification.TrimEnd()}'");
        report(Diagnostic.Create(
            Rule,
            location,
            subject,
            FileName(location),
            scope + problem,
            $"{fixPrefix}remove the suppression and fix the code; if a maintainer approved it, {justify}"));
    }

    // The reason of the justification on the first line of the suppression, or else on the line above; null when neither has one.
    private static string? ReasonOf(SyntaxNode root, TextSpan suppression)
    {
        var text = root.SyntaxTree.GetText();
        var line = text.Lines.GetLineFromPosition(suppression.Start).LineNumber;
        return ReasonOn(root, text, line) ?? (line > 0 ? ReasonOn(root, text, line - 1) : null);
    }

    // The justification that ends a line, after code, after a directive or alone on its line.
    private static string? ReasonOn(SyntaxNode root, SourceText text, int lineNumber)
    {
        var line = text.Lines[lineNumber];
        var end = line.End;
        while (end > line.Start && char.IsWhiteSpace(text[end - 1]))
            end--;

        if (end == line.Start)
            return null;

        var trivia = root.FindTrivia(end - 1, findInsideTrivia: true);
        if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
            return null;

        // '// hardslice-justification:' with no space before the reason is accepted too.
        var prefix = Justification.TrimEnd();
        var comment = trivia.ToString();
        return comment.StartsWith(prefix, StringComparison.Ordinal) ? comment.Substring(prefix.Length) : null;
    }

    private static string FileName(Location location) => Path.GetFileName(location.SourceTree?.FilePath ?? "");
}
