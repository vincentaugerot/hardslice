namespace MyApp.Shared.Persistence;

public static class BuiltAtStartServiceCollectionExtensions
{
    // Registers TService as a singleton that factory builds once, when the host starts and before its first request: a store that a
    // module loads from files and checks, such as reference data, or the values that a module builds from the store of another, which
    // factory reads from the provider. A factory runs only once every module is registered, so the order of the Add<Module>Module lines
    // of Program.cs does not matter. An exception of factory stops the application at start, and fails the start of every AppFactory of
    // the integration tests, which never start the hosted services of the application: invalid data never wait for the first request
    // that reads them.
    public static IServiceCollection AddSingletonBuiltAtStart<TService>(this IServiceCollection services, Func<IServiceProvider, TService> factory)
        where TService : class
    {
        services.AddSingleton(factory);
        services.AddTransient<IStartupFilter, BuildAtStart<TService>>();
        return services;
    }

    // ASP.NET Core resolves every IStartupFilter when its server starts, in production as under AppFactory: resolving this one
    // builds TService. It changes nothing of the HTTP pipeline.
    private sealed class BuildAtStart<TService> : IStartupFilter
        where TService : class
    {
        public BuildAtStart(TService service) =>
            ArgumentNullException.ThrowIfNull(service);

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => next;
    }
}
