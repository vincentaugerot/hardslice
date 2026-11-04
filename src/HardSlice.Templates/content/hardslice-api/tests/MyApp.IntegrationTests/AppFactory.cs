using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyApp.Shared.Persistence;

namespace MyApp.IntegrationTests;

// One running instance of the application on the test database. Several factories on the same database act as several instances.
// It runs in its own environment, IntegrationTests: the user secrets of the developer, which only Development loads, never reach a
// test. It keeps what Development changes for the tests: every service registration is validated when the host starts, and a request
// that cannot be read is thrown to the exception handlers. No hosted service that the application registers starts, those of its
// packages included, unless the test keeps it with KeepHostedService, and each one it removes is logged before the host starts.
// ASP.NET Core registers its own after them: they always run.
// An endpoint that requires an authorization policy is called with app.CreateAuthorizedClient(policy), from AuthorizedClients.
public sealed class AppFactory(PostgresDatabase database) : WebApplicationFactory<Program>
{
    public const string TestEnvironment = "IntegrationTests";

    // The client of each authorization policy of the application, by the name of the policy, filled once here: it makes a client of
    // this instance satisfy the policy, such as one that sends the test key that ConfigureWebHost gives the application with
    // builder.UseSetting, as in [AdminKey.Policy] = client => client.DefaultRequestHeaders.Add(AdminKey.Header, AdminApiKey).
    internal static readonly Dictionary<string, Action<HttpClient>> AuthorizedClients = new(StringComparer.OrdinalIgnoreCase)
    {
    };

    private readonly HashSet<Type> _keptHostedServices = [];

    // Starts THostedService, a hosted service that the application registers, in this instance only: for the tests of that service.
    // Call it before the first request and the first use of Services.
    public AppFactory KeepHostedService<THostedService>() where THostedService : IHostedService
    {
        _keptHostedServices.Add(typeof(THostedService));
        return this;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(TestEnvironment);
        builder.UseDefaultServiceProvider(options => options.ValidateScopes = options.ValidateOnBuild = true);
        builder.UseSetting($"ConnectionStrings:{PersistenceServiceCollectionExtensions.ConnectionStringName}", database.ConnectionString);
        builder.ConfigureTestServices(services =>
        {
            services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
            RemoveHostedServicesOfTheApplication(services);
        });
    }

    // As WebApplicationFactory builds and starts the host, with a line for each hosted service of the application that it does not
    // start: a test that waits for its work, or for the check of a setting it makes at start, reads why in the logs it captures.
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = builder.Build();
        var logger = host.Services.GetRequiredService<ILogger<AppFactory>>();
        foreach (var service in host.Services.GetRequiredService<RemovedHostedServices>().Types)
        {
            var name = Describe(service);
            logger.LogInformation(
                "AppFactory removed the hosted service {HostedService} of the application: a test that needs it calls KeepHostedService<{KeptService}>() on its AppFactory",
                name,
                name);
        }

        host.Start();
        return host;
    }

    private void RemoveHostedServicesOfTheApplication(IServiceCollection services)
    {
        var hostedServices = services
            .Where(service => service.ServiceType == typeof(IHostedService) && !service.IsKeyedService)
            .Select(service => (Descriptor: service, Type: ImplementationOf(service)))
            .ToList();

        var unknown = _keptHostedServices.Except(hostedServices.Select(service => service.Type)).Select(Describe).ToList();
        if (unknown.Count > 0)
            throw new InvalidOperationException(
                $"KeepHostedService: the application registers no hosted service {string.Join(", ", unknown)}. Register it with AddHostedService, "
                + "or with AddRecurringCommand<TCommand, TResponse> for a RecurringCommandService.");

        var removed = hostedServices.Where(service => !_keptHostedServices.Contains(service.Type)).ToList();
        foreach (var service in removed)
            services.Remove(service.Descriptor);
        services.AddSingleton(new RemovedHostedServices([.. removed.Select(service => service.Type).Where(type => type.FullName != AspNetCoreHostedService)]));
    }

    private static string Describe(Type type) =>
        type.IsGenericType
            ? $"{type.Name[..type.Name.IndexOf('`')]}<{string.Join(", ", type.GetGenericArguments().Select(Describe))}>"
            : type.Name;

    // ASP.NET Core registers this hosted service of its own once more after the test services: the server always runs, so its
    // removal is not logged.
    private const string AspNetCoreHostedService = "Microsoft.AspNetCore.Hosting.GenericWebHostService";

    private sealed record RemovedHostedServices(IReadOnlyList<Type> Types);

    // A hosted service registered with a factory is known by the type its factory returns.
    private static Type ImplementationOf(ServiceDescriptor service) =>
        service.ImplementationType ?? service.ImplementationInstance?.GetType() ?? service.ImplementationFactory!.Method.ReturnType;
}

public static class AppFactoryClients
{
    // A client of the application that satisfies the authorization policy <policy>, from AppFactory.AuthorizedClients, for the tests
    // of an endpoint that requires it: on an AppFactory, or on an instance derived from it with WithWebHostBuilder. A request without
    // authentication goes through CreateClient(), as the test of its 401 does.
    public static HttpClient CreateAuthorizedClient(this WebApplicationFactory<Program> app, string policy)
    {
        if (!AppFactory.AuthorizedClients.TryGetValue(policy, out var authorize))
            throw new InvalidOperationException(
                $"AppFactory has no client for the authorization policy '{policy}'. Add one to AuthorizedClients, in AppFactory.cs of the "
                + "integration tests: an entry named after the policy that makes a client satisfy it, such as one that sends the test key "
                + "that ConfigureWebHost gives the application with builder.UseSetting.");

        var client = app.CreateClient();
        authorize(client);
        return client;
    }
}
