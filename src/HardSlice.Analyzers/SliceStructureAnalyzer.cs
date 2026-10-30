using System.Collections.Immutable;
using HardSlice.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace HardSlice.Analyzers;

// HS004: a slice '<AssemblyName>.Features.<Module>.<Slice>' holds exactly one request named after it, '<Slice>Command' or
// '<Slice>Query', its handler '<Slice>Handler' and its static endpoint '<Slice>Endpoint'; the validator of its request, if
// any, is '<Slice>Validator'. No request, handler or request validator lives outside a slice, and the folder
// 'Features/<Module>/<Slice>/' of the project and the namespace of the slice hold the same types. Requests and handlers
// are recognized by the contracts of hardslice.json, validators by IValidator<T> of FluentValidation. A handler or a
// validator of another slice is left to HS016. Generated code is not checked. Not applied to test projects.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SliceStructureAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "HS004";

    private const string FeaturesFolder = "Features";

    private const string StructureRule = "A slice holds one request named after it, its handler and its endpoint.";

    private const string OutsideRule = "A request, its handler and its validator belong to the slice of their use case.";

    private const string FolderRule = "The folder and the namespace of a slice hold the same types.";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "A slice is not one request, its handler and its endpoint, named after it and declared in its folder",
        messageFormat: "{0}. {1} Fix: {2}. Doc: 'docs/rules/HS004.md'.",
        category: "HardSlice.Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly SymbolDisplayFormat DisplayFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (RuleActivation.IsTestProject(start.Options))
                return;

            var compilation = start.Compilation;
            var slices = new SliceMap(compilation);
            // Without usable messaging contracts, HS900 reports the configuration: only the endpoint and the folders are checked.
            var messaging = Messaging.Create(
                DomainModel.Create(compilation, HardSliceConfiguration.Read(start.Options, start.CancellationToken)),
                compilation,
                start.CancellationToken);
            start.Options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.MSBuildProjectDirectory", out var projectDirectory);
            var files = new ProjectFiles(projectDirectory, start.Options);

            start.RegisterSymbolAction(symbolContext => AnalyzeSlice(symbolContext, slices, messaging, files), SymbolKind.Namespace);
            start.RegisterSymbolAction(symbolContext => AnalyzeType(symbolContext, slices, messaging, files), SymbolKind.NamedType);
        });
    }

    // The content of a slice, seen from its namespace: its request, its handler, its validator and its endpoint.
    private static void AnalyzeSlice(SymbolAnalysisContext context, SliceMap slices, Messaging? messaging, ProjectFiles files)
    {
        var ns = (INamespaceSymbol)context.Symbol;
        if (slices.FeaturesPathOfNamespace(ns) is not { Length: 2 } path)
            return;

        var module = path[0];
        var sliceName = path[1];
        var slice = $"{module}/{sliceName}";
        var types = TypesOf(ns).Where(type => SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, ns.ContainingAssembly)).ToArray();
        var sliceLocation = files.FirstLocation(ns.Locations);
        if (sliceLocation is null)
            return;

        // A type declared in generated code only is not checked, but it gives the slice its request or its endpoint.
        var checkedTypes = types.Where(type => files.FirstLocation(type) is not null).ToArray();

        INamedTypeSymbol? request = null;
        if (messaging is not null)
        {
            var requests = files.InSourceOrder(checkedTypes.Where(type => messaging.RequestKind(type) is not null)).ToArray();
            request = requests.FirstOrDefault(candidate => candidate.Name == ExpectedRequestName(messaging, candidate, sliceName)) ?? requests.FirstOrDefault();
            if (request is null)
            {
                if (types.Any(type => messaging.RequestKind(type) is not null))
                    return;

                Report(context, sliceLocation, $"slice '{slice}' declares no request", StructureRule, NoRequestFix(messaging, types, module, sliceName, context.Compilation.AssemblyName));
            }
            else
            {
                var expected = ExpectedRequestName(messaging, request, sliceName);
                if (request.Name != expected)
                    Report(context, files.FirstLocation(request), $"slice '{slice}' declares '{request.Name}'", StructureRule, $"rename the {messaging.RequestKind(request)} to '{expected}'");

                foreach (var extra in requests.Where(candidate => !SymbolEqualityComparer.Default.Equals(candidate, request)))
                {
                    var useCase = UseCaseOf(extra.Name, messaging.RequestKind(extra)!);
                    var target = useCase == sliceName || useCase.Length == 0 ? $"'Features/{module}/<Slice>/', named after its use case," : $"'Features/{module}/{useCase}/'";
                    Report(
                        context,
                        files.FirstLocation(extra),
                        $"slice '{slice}' declares a second request, '{extra.Name}', besides '{request.Name}'",
                        StructureRule,
                        $"move '{extra.Name}' into its own slice {target} with its handler and its endpoint");
                }

                foreach (var handled in requests)
                {
                    if (!messaging.HasHandler(handled))
                    {
                        var useCase = UseCaseOf(handled.Name, messaging.RequestKind(handled)!);
                        Report(
                            context,
                            files.FirstLocation(handled),
                            $"slice '{slice}' declares no handler for '{handled.Name}'",
                            StructureRule,
                            $"declare 'internal sealed class {(useCase.Length > 0 ? useCase : sliceName)}Handler : {messaging.HandlerContract(handled)}' in the slice");
                    }
                }

                // A handler or a validator of a second request moves with it; one of another slice is reported by HS016.
                foreach (var type in checkedTypes)
                {
                    CheckName(messaging.HandledRequests(type), "handler", "Handler");
                    CheckName(messaging.ValidatedRequests(type), "validator", "Validator");

                    void CheckName(ImmutableArray<INamedTypeSymbol> requestsOfType, string role, string suffix)
                    {
                        if (requestsOfType.IsEmpty
                            || type.Name == sliceName + suffix
                            || requestsOfType.Any(candidate => slices.SliceOf(candidate) is { } other && other != slice)
                            || requestsOfType.Any(candidate => requests.Contains(candidate, SymbolEqualityComparer.Default) && !SymbolEqualityComparer.Default.Equals(candidate, request)))
                        {
                            return;
                        }

                        Report(context, files.FirstLocation(type), $"slice '{slice}' declares '{type.Name}'", StructureRule, $"rename the {role} to '{sliceName}{suffix}'");
                    }
                }
            }
        }

        var endpointName = sliceName + "Endpoint";
        var endpoints = types.Where(type => type.Name == endpointName).ToArray();
        var endpointLocation = request is null ? sliceLocation : files.FirstLocation(request);
        if (endpoints.Length == 0)
        {
            var lookalikes = checkedTypes.Where(type => type.Name.EndsWith("Endpoint", StringComparison.Ordinal)).ToArray();
            var fix = lookalikes.Length == 1
                ? $"rename '{lookalikes[0].Name}' to '{endpointName}'{(lookalikes[0].IsStatic ? "" : " and declare it static")}"
                : $"declare the internal static class '{endpointName}' in the slice: its Map(RouteGroupBuilder group) method maps the endpoint, and '{module}Module' calls it";
            if (endpointLocation is not null)
                Report(context, endpointLocation, $"slice '{slice}' declares no endpoint '{endpointName}'", StructureRule, fix);
        }

        foreach (var endpoint in endpoints.Where(endpoint => !endpoint.IsStatic && checkedTypes.Contains(endpoint, SymbolEqualityComparer.Default)))
        {
            Report(
                context,
                files.FirstLocation(endpoint),
                $"endpoint '{endpoint.Name}' of slice '{slice}' is not static",
                StructureRule,
                $"declare '{endpoint.Name}' as a static class: its Map(RouteGroupBuilder group) method maps the endpoint, and '{module}Module' calls it");
        }
    }

    // A request, a handler or a request validator outside any slice, and the folder of each declaration of a type.
    private static void AnalyzeType(SymbolAnalysisContext context, SliceMap slices, Messaging? messaging, ProjectFiles files)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        var path = slices.FeaturesPathOf(type);
        var assemblyName = context.Compilation.AssemblyName;

        if (messaging is not null && path is not { Length: >= 2 } && files.FirstLocation(type) is { } location)
        {
            var where = path switch
            {
                null => type.ContainingNamespace is { IsGlobalNamespace: false } ns ? $"namespace '{ns.ToDisplayString()}'" : "the global namespace",
                { Length: 0 } => $"namespace '{assemblyName}.{FeaturesFolder}'",
                _ => $"module '{path[0]}'"
            };

            if (messaging.RequestKind(type) is { } kind)
            {
                var useCase = UseCaseOf(type.Name, kind) is { Length: > 0 } named ? named : "<Slice>";
                Report(
                    context,
                    location,
                    $"{kind} '{type.Name}' is declared in {where}, outside any slice",
                    OutsideRule,
                    MoveFix(path?.FirstOrDefault(), useCase, type.Name, ExpectedName(useCase, kind), assemblyName, "with its handler and its endpoint"));
            }

            ReportOutside(messaging.HandledRequests(type), "handler", "Handler");
            ReportOutside(messaging.ValidatedRequests(type), "validator", "Validator");

            void ReportOutside(ImmutableArray<INamedTypeSymbol> requests, string role, string suffix)
            {
                if (requests.IsEmpty)
                    return;

                // Into the slice of its request, or the slice its request is named after when the request is outside too.
                var handled = requests[0];
                string? module;
                string useCase;
                if (slices.SliceOf(handled) is { } slice)
                {
                    module = SliceMap.ModuleOf(slice);
                    useCase = slice.Substring(module.Length + 1);
                }
                else
                {
                    module = slices.FeaturesPathOf(handled)?.FirstOrDefault() ?? path?.FirstOrDefault();
                    useCase = messaging.RequestKind(handled) is { } kind ? UseCaseOf(handled.Name, kind) : "";
                    if (useCase.Length == 0)
                        useCase = "<Slice>";
                }

                Report(
                    context,
                    location,
                    $"{role} '{type.Name}' of '{handled.Name}' is declared in {where}, outside any slice",
                    OutsideRule,
                    MoveFix(module, useCase, type.Name, useCase + suffix, assemblyName, null));
            }
        }

        if (type.ContainingType is null)
            CheckFolders(context, type, path, files, assemblyName);
    }

    // Each declaration of a type of a slice is under the folder of its slice; a type declared under the folder of a slice
    // belongs to its namespace. Nested types are declared with their containing type.
    private static void CheckFolders(SymbolAnalysisContext context, INamedTypeSymbol type, string[]? path, ProjectFiles files, string? assemblyName)
    {
        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            var tree = reference.SyntaxTree;
            if (files.IsGenerated(tree) || files.RelativePath(tree.FilePath) is not { } file)
                continue;

            var location = reference.GetSyntax(context.CancellationToken) switch
            {
                BaseTypeDeclarationSyntax declaration => declaration.Identifier.GetLocation(),
                DelegateDeclarationSyntax declaration => declaration.Identifier.GetLocation(),
                var syntax => syntax.GetLocation()
            };
            var folders = file.Split('/');

            if (path is { Length: >= 2 })
            {
                var sliceFolder = $"{FeaturesFolder}/{path[0]}/{path[1]}/";
                if (!file.StartsWith(sliceFolder, StringComparison.Ordinal))
                {
                    Report(
                        context,
                        location,
                        $"'{type.Name}' of slice '{path[0]}/{path[1]}' is declared in '{file}', outside the folder of its slice",
                        FolderRule,
                        $"move the declaration into a file under '{sliceFolder}'");
                }
            }
            else if (folders.Length >= 4 && folders[0] == FeaturesFolder)
            {
                var sliceFolder = $"{FeaturesFolder}/{folders[1]}/{folders[2]}/";
                var ns = type.ContainingNamespace is { IsGlobalNamespace: false } containing ? containing.ToDisplayString() : null;
                var namespaceFolder = ns is not null && assemblyName is not null && (ns + ".").StartsWith(assemblyName + ".", StringComparison.Ordinal)
                    ? ns.Length == assemblyName.Length ? "the root of the project" : $"'{ns.Substring(assemblyName.Length + 1).Replace('.', '/')}/'"
                    : null;
                Report(
                    context,
                    location,
                    $"'{type.Name}' is declared in '{file}', in the folder of slice '{folders[1]}/{folders[2]}', but in {(ns is null ? "the global namespace" : $"namespace '{ns}'")}",
                    FolderRule,
                    $"declare it in namespace '{assemblyName}.{FeaturesFolder}.{folders[1]}.{folders[2]}' if it belongs to the slice, "
                        + (namespaceFolder is null ? "or move its file out of the folder of the slice" : $"or move its file to {namespaceFolder}"));
            }
        }
    }

    private static string MoveFix(string? module, string useCase, string name, string expectedName, string? assemblyName, string? companions)
    {
        module ??= "<Module>";
        var details = new[] { name == expectedName ? null : $"renamed '{expectedName}'", companions }.Where(detail => detail is not null).ToArray();
        var how = details.Length switch
        {
            0 => "",
            1 when companions is not null => $" {companions}",
            1 => $", {details[0]}",
            _ => $", {details[0]}, {details[1]}"
        };
        return $"move '{name}' into slice '{module}/{useCase}'{how}: folder '{FeaturesFolder}/{module}/{useCase}/', "
            + $"namespace '{assemblyName}.{FeaturesFolder}.{module}.{useCase}'";
    }

    private static string NoRequestFix(Messaging messaging, INamedTypeSymbol[] types, string module, string sliceName, string? assemblyName)
    {
        foreach (var (kind, contract) in new[] { ("command", messaging.Command), ("query", messaging.Query) })
        {
            if (types.FirstOrDefault(type => type.Name == ExpectedName(sliceName, kind)) is { } lookalike)
                return $"implement '{contract.ToDisplayString(DisplayFormat)}' on '{lookalike.Name}'";
        }

        return $"declare the command '{sliceName}Command', which implements '{messaging.Command.ToDisplayString(DisplayFormat)}', "
            + $"or the query '{sliceName}Query', which implements '{messaging.Query.ToDisplayString(DisplayFormat)}'; "
            + $"types the slices of the module share belong to namespace '{assemblyName}.{FeaturesFolder}.{module}'";
    }

    private static string ExpectedRequestName(Messaging messaging, INamedTypeSymbol request, string sliceName) =>
        ExpectedName(sliceName, messaging.RequestKind(request)!);

    private static string ExpectedName(string useCase, string kind) => useCase + (kind == "command" ? "Command" : "Query");

    // 'ArchiveBooking' for 'ArchiveBookingCommand', empty for a name without the suffix of its kind.
    private static string UseCaseOf(string name, string kind)
    {
        var suffix = kind == "command" ? "Command" : "Query";
        return name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal) ? name.Substring(0, name.Length - suffix.Length) : "";
    }

    // The types of a namespace and of the namespaces below it, nested types included.
    private static IEnumerable<INamedTypeSymbol> TypesOf(INamespaceSymbol ns)
    {
        foreach (var member in ns.GetMembers())
        {
            var nested = member switch
            {
                INamespaceSymbol child => TypesOf(child),
                INamedTypeSymbol type => WithNested(type),
                _ => []
            };
            foreach (var type in nested)
                yield return type;
        }

        static IEnumerable<INamedTypeSymbol> WithNested(INamedTypeSymbol type) =>
            new[] { type }.Concat(type.GetTypeMembers().SelectMany(WithNested));
    }

    private static void Report(SymbolAnalysisContext context, Location? location, string fault, string rule, string fix)
    {
        if (location is not null)
            context.ReportDiagnostic(Diagnostic.Create(Rule, location, fault, rule, fix));
    }

    // The messaging contracts of hardslice.json and IValidator<T> of FluentValidation, when they can classify the types
    // of this compilation.
    private sealed class Messaging
    {
        private readonly DomainModel _model;
        private readonly INamedTypeSymbol _handler;
        private readonly INamedTypeSymbol? _validator;
        private readonly Lazy<ImmutableHashSet<ISymbol>> _handledRequests;

        private Messaging(DomainModel model, INamedTypeSymbol command, INamedTypeSymbol query, INamedTypeSymbol handler, INamedTypeSymbol? validator, Compilation compilation, CancellationToken cancellationToken)
        {
            _model = model;
            Command = command;
            Query = query;
            _handler = handler;
            _validator = validator;
            _handledRequests = new Lazy<ImmutableHashSet<ISymbol>>(() => CollectHandledRequests(compilation, cancellationToken));
        }

        public INamedTypeSymbol Command { get; }

        public INamedTypeSymbol Query { get; }

        public static Messaging? Create(DomainModel model, Compilation compilation, CancellationToken cancellationToken) =>
            model.Contract(ContractKeys.Command) is { } command
            && model.Contract(ContractKeys.Query) is { } query
            && model.Contract(ContractKeys.RequestHandler) is { } handler
                ? new Messaging(model, command, query, handler, compilation.GetTypeByMetadataName("FluentValidation.IValidator`1"), compilation, cancellationToken)
                : null;

        // "command" or "query" for a class or a struct that can be sent; a type both is reported by HS901 and named as a command.
        public string? RequestKind(INamedTypeSymbol type)
        {
            if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct) || type.IsAbstract || type.IsStatic)
                return null;

            return _model.IsCommand(type) ? "command" : _model.IsQuery(type) ? "query" : null;
        }

        // The requests a concrete handler handles, by their definition: 'ArchiveBookingCommand<T>' for
        // 'IRequestHandler<ArchiveBookingCommand<T>, bool>', directly or through a base class. A request that is a
        // type parameter is not a request of a slice.
        public ImmutableArray<INamedTypeSymbol> HandledRequests(INamedTypeSymbol type) => Arguments(type, _handler, request => true);

        // The requests a concrete validator validates; a validator of another type, such as a DTO, is not a request validator.
        public ImmutableArray<INamedTypeSymbol> ValidatedRequests(INamedTypeSymbol type) =>
            _validator is null ? [] : Arguments(type, _validator, validated => RequestKind(validated) is not null);

        public bool HasHandler(INamedTypeSymbol request) => _handledRequests.Value.Contains(request.OriginalDefinition);

        // 'IRequestHandler<ArchiveBookingCommand, bool>'.
        public string HandlerContract(INamedTypeSymbol request)
        {
            var contract = _model.IsCommand(request) ? Command : Query;
            var response = DomainModel.Implementations(request, contract).FirstOrDefault()?.TypeArguments.FirstOrDefault();
            return $"{_handler.Name}<{request.ToDisplayString(DisplayFormat)}, {response?.ToDisplayString(DisplayFormat) ?? "TResponse"}>";
        }

        private ImmutableArray<INamedTypeSymbol> Arguments(INamedTypeSymbol type, INamedTypeSymbol contract, Func<INamedTypeSymbol, bool> accept)
        {
            if (type.TypeKind != TypeKind.Class || type.IsAbstract || type.IsStatic)
                return [];

            return DomainModel.Implementations(type, contract)
                .Select(implementation => implementation.TypeArguments.FirstOrDefault())
                .OfType<INamedTypeSymbol>()
                .Where(argument => argument.TypeKind != TypeKind.Error && accept(argument))
                .Select(argument => argument.OriginalDefinition)
                .Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default)
                .ToImmutableArray();
        }

        // The requests the handlers of this compilation handle, wherever they are declared: a request whose handler is
        // outside its slice is reported once, on the handler.
        private ImmutableHashSet<ISymbol> CollectHandledRequests(Compilation compilation, CancellationToken cancellationToken)
        {
            var requests = ImmutableHashSet.CreateBuilder<ISymbol>(SymbolEqualityComparer.Default);
            foreach (var type in TypesOf(compilation.Assembly.GlobalNamespace))
            {
                cancellationToken.ThrowIfCancellationRequested();
                requests.UnionWith(HandledRequests(type));
            }

            return requests.ToImmutable();
        }
    }

    // The source files of the project: their path below the project directory, and whether they hold generated code.
    private sealed class ProjectFiles(string? projectDirectory, AnalyzerOptions options)
    {
        private readonly string? _projectDirectory = string.IsNullOrEmpty(projectDirectory) ? null : Normalize(projectDirectory!).TrimEnd('/') + "/";

        // 'Features/Bookings/ConfirmBooking/ConfirmBooking.cs', the full path of a file outside the project directory, or
        // null when the project directory is unknown. A relative path is relative to the project directory.
        public string? RelativePath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return null;

            var path = Normalize(filePath);
            if (!Path.IsPathRooted(filePath))
                return path;

            if (_projectDirectory is null)
                return null;

            return path.StartsWith(_projectDirectory, StringComparison.OrdinalIgnoreCase) ? path.Substring(_projectDirectory.Length) : path;
        }

        // As the compiler recognizes generated code: its file name, its auto-generated header or 'generated_code' in .editorconfig.
        public bool IsGenerated(SyntaxTree tree)
        {
            if (options.AnalyzerConfigOptionsProvider.GetOptions(tree).TryGetValue("generated_code", out var generated))
                return string.Equals(generated, "true", StringComparison.OrdinalIgnoreCase);

            var fileName = Path.GetFileName(tree.FilePath ?? "");
            if (fileName.StartsWith("TemporaryGeneratedFile_", StringComparison.OrdinalIgnoreCase))
                return true;

            var withoutExtension = Path.GetFileNameWithoutExtension(fileName);
            if (new[] { ".designer", ".generated", ".g", ".g.i" }.Any(suffix => withoutExtension.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
                return true;

            return tree.GetRoot().GetLeadingTrivia().Any(trivia =>
                (trivia.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.MultiLineCommentTrivia))
                && (trivia.ToString().Contains("<auto-generated") || trivia.ToString().Contains("<autogenerated")));
        }

        // The first location in source order outside generated code, so that the compiler does not drop the diagnostic.
        public Location? FirstLocation(ImmutableArray<Location> locations) =>
            locations
                .Where(location => location.IsInSource && !IsGenerated(location.SourceTree!))
                .OrderBy(location => location.SourceTree!.FilePath, StringComparer.Ordinal)
                .ThenBy(location => location.SourceSpan.Start)
                .FirstOrDefault();

        public Location? FirstLocation(ISymbol symbol) => FirstLocation(symbol.Locations);

        public IEnumerable<T> InSourceOrder<T>(IEnumerable<T> symbols) where T : ISymbol =>
            symbols
                .Select(symbol => (Symbol: symbol, Location: FirstLocation(symbol)))
                .OrderBy(entry => entry.Location?.SourceTree!.FilePath ?? "", StringComparer.Ordinal)
                .ThenBy(entry => entry.Location?.SourceSpan.Start ?? 0)
                .Select(entry => entry.Symbol);

        private static string Normalize(string path) => path.Replace('\\', '/');
    }
}
