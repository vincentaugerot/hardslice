namespace MyApp.Features.ModuleName;

// The public contract of the module: Program.cs registers and maps it, other modules use only the types of this namespace (HS012).
public static class ModuleNameModule
{
    // The route of the group that maps the endpoints of the module: the integration tests of its slices call theirs under it.
    public const string Route = "/module-route";

    // Handlers are registered by their contract only: endpoints reach them through IMediator, never directly.
    public static IServiceCollection AddModuleNameModule(this IServiceCollection services) =>
        services;

    // Each slice maps its endpoint on the group of the module: <Slice>Endpoint.Map(group).
    public static IEndpointRouteBuilder MapModuleNameModule(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(Route);
        return endpoints;
    }
}
