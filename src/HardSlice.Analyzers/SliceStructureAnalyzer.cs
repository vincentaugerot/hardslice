using System.Collections.Concurrent;
using System.Collections.Immutable;
using HardSlice.Analyzers.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using static HardSlice.Analyzers.EndpointMapping;

namespace HardSlice.Analyzers;

// HS004: a slice '<AssemblyName>.Features.<Module>.<Slice>' has exactly one entry. Either one request named after it,
// '<Slice>Command' or '<Slice>Query', its handler '<Slice>Handler' and its static endpoint '<Slice>Endpoint', the validator
// of its request, if any, being '<Slice>Validator'; or, for a reaction slice, one domain event handler '<Slice>Handler'
// that handles one event, without request nor endpoint. No request, handler or request validator lives outside a slice,
// and the folder 'Features/<Module>/<Slice>/' of the project and the namespace of the slice hold the same types. Requests
// and handlers are recognized by the contracts of hardslice.json, validators by IValidator<T> of FluentValidation. A handler
// or a validator of another slice is left to HS016. A system command, declared by the optional contract 'systemCommand', is
// sent by the application only: its slice has no endpoint, no HTTP mapping sends it, and a type of its module other than its
// handler and its validator creates it or names it as a generic argument, which is known at the end of the compilation only.
// Generated code is not checked. Not applied to test projects.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SliceStructureAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "HS004";

    private const string FeaturesFolder = "Features";

    private const string StructureRule = "A slice holds one request named after it, its handler and its endpoint.";

    private const string ReactionRule = "A reaction slice holds one domain event handler named after it, and no request nor endpoint.";

    private const string OutsideRule = "A request, its handler and its validator belong to the slice of their use case.";

    private const string OutsideReactionRule = "A domain event handler belongs to a reaction slice of its own.";

    private const string FolderRule = "The folder and the namespace of a slice hold the same types.";

    private const string SystemRule = "A system command is sent by the application only: its slice has no endpoint, and a type of its module sends it.";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "A slice is not shaped as one entry named after it",
        messageFormat: "{0}. {1} Fix: {2}. Skill: add-slice. Doc: 'docs/rules/HS004.md'.",
        category: "HardSlice.Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: RuleSheet.Uri(DiagnosticId));

    // Whether a type of its module sends a system command is known at the end of the compilation only: in the IDE, this part of
    // the rule is reported by full solution analysis.
    private static readonly DiagnosticDescriptor UnsentRule = new(
        DiagnosticId,
        title: "A slice is not shaped as one entry named after it",
        messageFormat: "{0}. {1} Fix: {2}. Skill: add-slice. Doc: 'docs/rules/HS004.md'.",
        category: "HardSlice.Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: RuleSheet.Uri(DiagnosticId),
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    private static readonly SymbolDisplayFormat DisplayFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule, UnsentRule);

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
            // Without usable messaging contracts, HS900 reports the configuration: only the folders and the existing endpoints
            // are checked, since a missing endpoint cannot be told from a reaction slice.
            var messaging = Messaging.Create(DomainModel.For(compilation, start.Options, start.CancellationToken), compilation);
            start.Options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.MSBuildProjectDirectory", out var projectDirectory);
            var files = new ProjectFiles(projectDirectory, start.Options);

            start.RegisterSymbolAction(symbolContext => AnalyzeSlice(symbolContext, slices, messaging, files), SymbolKind.Namespace);
            start.RegisterSymbolAction(symbolContext => AnalyzeType(symbolContext, slices, messaging, files), SymbolKind.NamedType);

            // Only an application that declares the systemCommand contract pays for the analysis of its senders.
            if (messaging?.SystemCommand is null)
                return;

            var senders = new SystemCommandSenders(slices, messaging);
            var routing = Routing.Create(compilation);
            start.RegisterSymbolAction(
                symbolContext =>
                {
                    var type = (INamedTypeSymbol)symbolContext.Symbol;
                    senders.RecordDeclaration(type);
                    if (messaging.IsSystemCommand(type) && slices.SliceOf(type) is { } slice && files.FirstLocation(type) is { } location)
                        senders.Expect(type, slice, location);
                },
                SymbolKind.NamedType);
            start.RegisterOperationAction(
                operationContext => senders.RecordType(operationContext.ContainingSymbol, operationContext.Operation.Type),
                OperationKind.ObjectCreation);
            start.RegisterOperationAction(
                operationContext =>
                {
                    var invocation = (IInvocationOperation)operationContext.Operation;
                    senders.RecordTypeArguments(operationContext.ContainingSymbol, invocation.TargetMethod.TypeArguments);
                    if (routing is not null && routing.IsMapping(invocation.TargetMethod))
                        AnalyzeMapping(operationContext, invocation, routing, slices, messaging);
                },
                OperationKind.Invocation);
            start.RegisterCompilationEndAction(senders.ReportUnsent);
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
            var eventHandlers = files.InSourceOrder(checkedTypes.Where(messaging.IsEventHandler)).ToArray();
            request = requests.FirstOrDefault(candidate => candidate.Name == ExpectedRequestName(messaging, candidate, sliceName)) ?? requests.FirstOrDefault();
            if (request is null)
            {
                if (types.Any(type => messaging.RequestKind(type) is not null))
                    return;

                if (eventHandlers.Length > 0)
                {
                    AnalyzeReaction(context, messaging, files, slice, module, sliceName, checkedTypes, eventHandlers);
                    return;
                }

                Report(
                    context,
                    sliceLocation,
                    $"slice '{slice}' declares no request nor domain event handler",
                    StructureRule,
                    NoRequestFix(messaging, types, module, sliceName, context.Compilation.AssemblyName));
            }
            else
            {
                foreach (var eventHandler in eventHandlers)
                {
                    Report(
                        context,
                        files.FirstLocation(eventHandler),
                        $"slice '{slice}' declares domain event handler '{eventHandler.Name}' besides request '{request.Name}'",
                        ReactionRule,
                        ReactionFix(messaging, eventHandler, module));
                }

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

        // A system command has no HTTP entry point: a type of its module sends it.
        if (request is not null && messaging!.IsSystemCommand(request))
        {
            foreach (var endpoint in checkedTypes.Where(type => type.Name.EndsWith("Endpoint", StringComparison.Ordinal)))
            {
                Report(
                    context,
                    files.FirstLocation(endpoint),
                    $"slice '{slice}' of system command '{request.Name}' declares endpoint '{endpoint.Name}'",
                    SystemRule,
                    $"remove '{endpoint.Name}' and its call from '{module}Module': the application sends '{request.Name}'; "
                        + $"an HTTP entry point is a command slice of its own, whose command implements '{messaging.Command.ToDisplayString(DisplayFormat)}'");
            }

            return;
        }

        var endpointName = sliceName + "Endpoint";
        var endpoints = types.Where(type => type.Name == endpointName).ToArray();
        var endpointLocation = request is null ? sliceLocation : files.FirstLocation(request);
        if (endpoints.Length == 0 && messaging is not null)
        {
            var lookalikes = checkedTypes.Where(type => type.Name.EndsWith("Endpoint", StringComparison.Ordinal)).ToArray();
            var fix = lookalikes.Length == 1
                ? $"rename '{lookalikes[0].Name}' to '{endpointName}'{(lookalikes[0].IsStatic ? "" : " and declare it static")}"
                : $"declare the internal static class '{endpointName}' in the slice: its Map(RouteGroupBuilder group) method maps the endpoint, and '{module}Module' calls it";
            if (lookalikes.Length != 1 && request is not null && messaging.RequestKind(request) == "command" && messaging.SystemCommand is { } systemCommand)
                fix += $"; a command that only the application sends, such as a job, implements '{systemCommand.ToDisplayString(DisplayFormat)}' instead and has no endpoint";
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

            if (messaging.IsEventHandler(type))
            {
                Report(
                    context,
                    location,
                    $"domain event handler '{type.Name}' of {messaging.DescribeEvents(type)} is declared in {where}, outside any slice",
                    OutsideReactionRule,
                    ReactionFix(messaging, type, path?.FirstOrDefault(), assemblyName));
            }

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

    // An HTTP mapping whose endpoint creates a system command, or takes one as a parameter. The endpoint class of its own slice
    // is reported with the slice.
    private static void AnalyzeMapping(OperationAnalysisContext context, IInvocationOperation mapping, Routing routing, SliceMap slices, Messaging messaging)
    {
        if (HandlerArgument(mapping)?.Value is not { } handler || SentSystemCommand(handler, messaging) is not { } command)
            return;

        var container = context.ContainingSymbol;
        var commandSlice = slices.SliceOf(command);
        for (var type = container as INamedTypeSymbol ?? container.ContainingType; type is not null; type = type.ContainingType)
        {
            if (commandSlice is not null && slices.SliceOf(type) == commandSlice && type.Name.EndsWith("Endpoint", StringComparison.Ordinal))
                return;
        }

        var module = commandSlice is null ? "<Module>" : SliceMap.ModuleOf(commandSlice);
        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            OperationLocation(mapping.Syntax),
            $"endpoint '{Describe(mapping, routing, context.CancellationToken)}' in {PlaceOf(container, slices)} sends system command '{command.Name}'",
            SystemRule,
            $"remove the endpoint: a type of module '{module}' sends '{command.Name}'; "
                + $"an HTTP entry point is a command slice of its own, whose command implements '{messaging.Command.ToDisplayString(DisplayFormat)}'"));
    }

    // The system command an endpoint sends: a parameter of its delegate, or a type its body creates or reads.
    private static INamedTypeSymbol? SentSystemCommand(IOperation handler, Messaging messaging)
    {
        var parameters = WithoutConversions(handler) switch
        {
            IDelegateCreationOperation { Target: IAnonymousFunctionOperation function } => function.Symbol.Parameters,
            IDelegateCreationOperation { Target: IMethodReferenceOperation reference } => reference.Method.Parameters,
            IAnonymousFunctionOperation function => function.Symbol.Parameters,
            _ => ImmutableArray<IParameterSymbol>.Empty
        };

        return parameters.Select(parameter => parameter.Type)
            .Concat(handler.DescendantsAndSelf().Select(operation => operation.Type))
            .OfType<INamedTypeSymbol>()
            .FirstOrDefault(messaging.IsSystemCommand)
            ?.OriginalDefinition;
    }

    // A slice without request whose entry is a domain event handler: one handler named after the slice, for one event,
    // and no endpoint. Validators and other types are free, as in any slice.
    private static void AnalyzeReaction(
        SymbolAnalysisContext context,
        Messaging messaging,
        ProjectFiles files,
        string slice,
        string module,
        string sliceName,
        INamedTypeSymbol[] checkedTypes,
        INamedTypeSymbol[] eventHandlers)
    {
        var expected = sliceName + "Handler";
        var handler = eventHandlers.FirstOrDefault(candidate => candidate.Name == expected) ?? eventHandlers[0];
        if (handler.Name != expected)
            Report(context, files.FirstLocation(handler), $"slice '{slice}' declares '{handler.Name}'", ReactionRule, $"rename the domain event handler to '{expected}'");

        foreach (var extra in eventHandlers.Where(candidate => !SymbolEqualityComparer.Default.Equals(candidate, handler)))
        {
            Report(
                context,
                files.FirstLocation(extra),
                $"slice '{slice}' declares a second domain event handler, '{extra.Name}', besides '{handler.Name}'",
                ReactionRule,
                ReactionFix(messaging, extra, module));
        }

        foreach (var eventHandler in eventHandlers.Where(candidate => messaging.HandledEventCount(candidate) > 1))
        {
            Report(
                context,
                files.FirstLocation(eventHandler),
                $"domain event handler '{eventHandler.Name}' of slice '{slice}' handles {messaging.DescribeEvents(eventHandler)}",
                ReactionRule,
                "keep one event in this handler and react to each other event in a reaction slice of its own");
        }

        foreach (var endpoint in checkedTypes.Where(type => type.Name.EndsWith("Endpoint", StringComparison.Ordinal)))
        {
            Report(
                context,
                files.FirstLocation(endpoint),
                $"reaction slice '{slice}' declares endpoint '{endpoint.Name}'",
                ReactionRule,
                $"remove '{endpoint.Name}' and its call from '{module}Module': a domain event triggers the reaction; an HTTP entry point is a command slice of its own");
        }
    }

    // Into a reaction slice of its own: the one its name gives, 'AuditBookingConfirmed' for 'AuditBookingConfirmedHandler',
    // or one named after what it does, such as 'NotifyOnBookingConfirmed'.
    private static string ReactionFix(Messaging messaging, INamedTypeSymbol eventHandler, string? module, string? assemblyName = null)
    {
        module ??= "<Module>";
        var named = eventHandler.Name.Length > "Handler".Length && eventHandler.Name.EndsWith("Handler", StringComparison.Ordinal);
        var reaction = named
            ? eventHandler.Name.Substring(0, eventHandler.Name.Length - "Handler".Length)
            : messaging.HandledEvents(eventHandler).FirstOrDefault() is { } handled ? $"NotifyOn{handled.Name}" : "NotifyOn<Event>";
        var where = assemblyName is null
            ? $"'{FeaturesFolder}/{module}/{reaction}/'"
            : $"'{module}/{reaction}': folder '{FeaturesFolder}/{module}/{reaction}/', namespace '{assemblyName}.{FeaturesFolder}.{module}.{reaction}'";
        return named
            ? $"move '{eventHandler.Name}' into a reaction slice of its own, {where}"
            : $"move '{eventHandler.Name}' into a reaction slice of its own, named after what it does, such as {where}, renamed '<Reaction>Handler'";
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
            + $"or the query '{sliceName}Query', which implements '{messaging.Query.ToDisplayString(DisplayFormat)}', "
            + $"or, for a reaction to a domain event, the handler '{sliceName}Handler', which implements '{messaging.EventHandler.ToDisplayString(DisplayFormat)}'; "
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

        private Messaging(
            DomainModel model,
            INamedTypeSymbol command,
            INamedTypeSymbol query,
            INamedTypeSymbol handler,
            INamedTypeSymbol eventHandler,
            INamedTypeSymbol? validator,
            Compilation compilation)
        {
            _model = model;
            Command = command;
            SystemCommand = model.Contract(ContractKeys.SystemCommand);
            Query = query;
            _handler = handler;
            EventHandler = eventHandler;
            _validator = validator;
            // Without the token of the compilation start: the IDE reuses this start for analyses canceled by their own token,
            // and a Lazy would keep the exception of a canceled evaluation for the whole compilation.
            _handledRequests = new Lazy<ImmutableHashSet<ISymbol>>(() => CollectHandledRequests(compilation), LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public INamedTypeSymbol Command { get; }

        public INamedTypeSymbol Query { get; }

        // The optional contract of the commands that only the application sends.
        public INamedTypeSymbol? SystemCommand { get; }

        public INamedTypeSymbol RequestHandler => _handler;

        public INamedTypeSymbol? Validator => _validator;

        public INamedTypeSymbol EventHandler { get; }

        public static Messaging? Create(DomainModel model, Compilation compilation) =>
            model.Contract(ContractKeys.Command) is { } command
            && model.Contract(ContractKeys.Query) is { } query
            && model.Contract(ContractKeys.RequestHandler) is { } handler
            && model.Contract(ContractKeys.DomainEventHandler) is { } eventHandler
                ? new Messaging(model, command, query, handler, eventHandler, compilation.GetTypeByMetadataName("FluentValidation.IValidator`1"), compilation)
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
        // A concrete command that implements the systemCommand contract.
        public bool IsSystemCommand(INamedTypeSymbol type) =>
            SystemCommand is not null && RequestKind(type) == "command" && _model.IsSystemCommand(type);

        public ImmutableArray<INamedTypeSymbol> HandledRequests(INamedTypeSymbol type) => Arguments(type, _handler, request => true);

        // The requests a concrete validator validates; a validator of another type, such as a DTO, is not a request validator.
        public ImmutableArray<INamedTypeSymbol> ValidatedRequests(INamedTypeSymbol type) =>
            _validator is null ? [] : Arguments(type, _validator, validated => RequestKind(validated) is not null);

        public bool HasHandler(INamedTypeSymbol request) => _handledRequests.Value.Contains(request.OriginalDefinition);

        // A concrete class that implements the domainEventHandler contract, for an event or a type parameter.
        public bool IsEventHandler(INamedTypeSymbol type) =>
            type is { TypeKind: TypeKind.Class, IsAbstract: false, IsStatic: false } && _model.IsDomainEventHandler(type);

        public int HandledEventCount(INamedTypeSymbol type) => DomainModel.Implementations(type, EventHandler).Count();

        // The events a concrete domain event handler handles, by their definition.
        public ImmutableArray<INamedTypeSymbol> HandledEvents(INamedTypeSymbol type) => Arguments(type, EventHandler, domainEvent => true);

        // "'BookingConfirmed'", or "2 events, 'BookingConfirmed' and 'BookingCancelled'".
        public string DescribeEvents(INamedTypeSymbol type)
        {
            var names = DomainModel.Implementations(type, EventHandler)
                .Select(implementation => $"'{implementation.TypeArguments[0].ToDisplayString(DisplayFormat)}'")
                .Distinct()
                .ToArray();
            return names.Length switch
            {
                0 => "an event",
                1 => names[0],
                _ => $"{names.Length} events, {string.Join(", ", names.Take(names.Length - 1))} and {names[names.Length - 1]}"
            };
        }

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
        private ImmutableHashSet<ISymbol> CollectHandledRequests(Compilation compilation)
        {
            var requests = ImmutableHashSet.CreateBuilder<ISymbol>(SymbolEqualityComparer.Default);
            foreach (var type in TypesOf(compilation.Assembly.GlobalNamespace))
                requests.UnionWith(HandledRequests(type));

            return requests.ToImmutable();
        }
    }

    // The system commands of the slices, and the types of their modules that send them: a type that creates one, or names it as
    // a generic argument of a method it calls or of a type it derives from, such as 'AddRecurringCommand<TCommand>' or
    // 'RecurringCommandService<TCommand>'. Its handler and its validator do not send it, nor do the contracts that name it to
    // register them, such as 'IRequestHandler<TCommand, TResponse>'.
    private sealed class SystemCommandSenders(SliceMap slices, Messaging messaging)
    {
        private readonly ConcurrentDictionary<ISymbol, byte> _sent = new(SymbolEqualityComparer.Default);
        private readonly ConcurrentQueue<(INamedTypeSymbol Command, string Slice, Location Location)> _expected = new();

        public void Expect(INamedTypeSymbol command, string slice, Location location) => _expected.Enqueue((command.OriginalDefinition, slice, location));

        public void RecordDeclaration(INamedTypeSymbol type)
        {
            if (type.BaseType is { } baseType)
                RecordTypeArguments(type, baseType.TypeArguments);
            foreach (var @interface in type.Interfaces)
                RecordTypeArguments(type, @interface.TypeArguments);
        }

        public void RecordType(ISymbol? sender, ITypeSymbol? type)
        {
            if (type is INamedTypeSymbol named)
                Record(sender, named, depth: 0);
        }

        public void RecordTypeArguments(ISymbol? sender, ImmutableArray<ITypeSymbol> arguments)
        {
            foreach (var argument in arguments)
                RecordType(sender, argument);
        }

        public void ReportUnsent(CompilationAnalysisContext context)
        {
            foreach (var (command, slice, location) in _expected)
            {
                if (_sent.ContainsKey(command))
                    continue;

                var module = SliceMap.ModuleOf(slice);
                context.ReportDiagnostic(Diagnostic.Create(
                    UnsentRule,
                    location,
                    $"system command '{command.Name}' of slice '{slice}' is sent by no type of module '{module}'",
                    SystemRule,
                    $"send '{command.Name}' through IMediator from a type of module '{module}' other than its handler and its validator, "
                        + $"such as a hosted service in 'Features/{slice}/' that creates it in a scope of its own, registered by '{module}Module'; "
                        + $"a command sent over HTTP implements '{messaging.Command.ToDisplayString(DisplayFormat)}' instead, with its endpoint, "
                        + "and a command that nothing sends is removed with its slice"));
            }
        }

        private void Record(ISymbol? sender, INamedTypeSymbol type, int depth)
        {
            if (type.TypeKind == TypeKind.Error || depth > 8)
                return;

            if (messaging.IsSystemCommand(type))
            {
                if (IsSenderOf(sender, type.OriginalDefinition))
                    _sent.TryAdd(type.OriginalDefinition, 0);
                return;
            }

            var definition = type.OriginalDefinition;
            if (SymbolEqualityComparer.Default.Equals(definition, messaging.RequestHandler) || SymbolEqualityComparer.Default.Equals(definition, messaging.Validator))
                return;

            foreach (var argument in type.TypeArguments.OfType<INamedTypeSymbol>())
                Record(sender, argument, depth + 1);
        }

        // A type of the module of the command, outside the command, its handler and its validator, and the types they contain.
        private bool IsSenderOf(ISymbol? sender, INamedTypeSymbol command)
        {
            if (slices.SliceOf(command) is not { } slice)
                return false;

            var outermost = sender as INamedTypeSymbol ?? sender?.ContainingType;
            if (outermost is null)
                return false;

            for (var type = outermost; type is not null; type = type.ContainingType)
            {
                if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, command)
                    || messaging.HandledRequests(type).Contains(command, SymbolEqualityComparer.Default)
                    || messaging.ValidatedRequests(type).Contains(command, SymbolEqualityComparer.Default))
                {
                    return false;
                }

                outermost = type;
            }

            return slices.FeaturesPathOf(outermost) is { Length: >= 1 } path && path[0] == SliceMap.ModuleOf(slice);
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
