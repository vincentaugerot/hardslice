using System.Reflection;
using System.Runtime.CompilerServices;
using FluentValidation;
using Microsoft.AspNetCore.Routing;
using MyApp.Shared.Messaging;

namespace MyApp.ArchitectureTests;

// HS016: slices never reference each other, and their handlers, domain event handlers, validators and endpoints are internal.
// HardSlice.Analyzers reports the same references in the source at build time; this test reads the compiled assembly.
public sealed class SliceTests
{
    [Fact]
    public void Slices_never_use_types_of_another_slice()
    {
        Violations.ShouldBeNone(
            from type in Application.TypesOf(Application.WebAssembly)
            let source = Location.Of(type)
            where source.Slice is not null
            from used in Application.TypesUsedBy(type)
            where Application.IsDeclaredIn(used, Application.WebAssembly)
            let target = Location.Of(used)
            where target.Slice is not null && target.Slice != source.Slice
            select $"HS016: '{Application.DisplayName(type)}' in slice '{source.Slice}' uses '{Application.DisplayName(used)}' from slice '{target.Slice}'. "
                + "Slices never reference each other. Fix: move the shared rule into a domain aggregate, raise a domain event, "
                + $"or use the public contract of module '{target.Module}'. Doc: 'docs/rules/HS016.md'.");
    }

    // Includes protected nesting reachable through inheritance, but excludes nesting inside internal/private containers.
    [Fact]
    public void Handlers_validators_and_endpoints_are_internal()
    {
        Violations.ShouldBeNone(
            from type in Application.WebAssembly.GetTypes()
            where TypeVisibility.IsExternallyAccessible(type) && !type.IsDefined(typeof(CompilerGeneratedAttribute))
            let role = RoleOf(type)
            where role is not null
            select $"HS016: {role} '{type.Name}' {Location.Of(type.Namespace).Describe()} is accessible outside the assembly. Handlers, domain event handlers, validators and endpoints are internal: "
                + "other code reaches a slice through IMediator and its public request. "
                + $"Fix: declare '{type.Name}' internal. Doc: 'docs/rules/HS016.md'.");
    }

    private static string? RoleOf(Type type)
    {
        if (Implements(type, typeof(IRequestHandler<,>)))
            return "handler";
        if (Implements(type, typeof(IDomainEventHandler<>)))
            return "domain event handler";
        if (Implements(type, typeof(IValidator<>)))
            return "validator";
        return Location.Of(type.Namespace).Slice is not null && IsEndpoint(type) ? "endpoint" : null;
    }

    private static bool Implements(Type type, Type genericInterface) =>
        type.GetInterfaces().Any(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == genericInterface);

    // An endpoint is named '<Slice>Endpoint' or maps a route: it declares a method that takes the route builder of its module.
    private static bool IsEndpoint(Type type) =>
        type.Name.EndsWith("Endpoint", StringComparison.Ordinal)
        || type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Any(method => method.GetParameters().Any(parameter => typeof(IEndpointRouteBuilder).IsAssignableFrom(parameter.ParameterType)));
}
