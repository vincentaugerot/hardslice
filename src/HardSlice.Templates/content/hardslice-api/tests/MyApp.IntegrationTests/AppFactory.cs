using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyApp.Shared.Persistence;

namespace MyApp.IntegrationTests;

// One running instance of the application on the test database. Several factories on the same database act as several instances.
// It runs in its own environment, IntegrationTests: the user secrets of the developer, which only Development loads, never reach a
// test. It keeps what Development changes for the tests: every service registration is validated when the host starts, and a request
// that cannot be read is thrown to the exception handlers. No hosted service that the application registers starts, those of its
// packages included, unless the test keeps it with KeepHostedService. ASP.NET Core registers its own after them: they always run.
public sealed class AppFactory(PostgresDatabase database) : WebApplicationFactory<Program>
{
    public const string TestEnvironment = "IntegrationTests";

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

    private void RemoveHostedServicesOfTheApplication(IServiceCollection services)
    {
        var hostedServices = services
            .Where(service => service.ServiceType == typeof(IHostedService) && !service.IsKeyedService)
            .Select(service => (Descriptor: service, Type: ImplementationOf(service)))
            .ToList();

        var unknown = _keptHostedServices.Except(hostedServices.Select(service => service.Type)).Select(type => type.Name).ToList();
        if (unknown.Count > 0)
            throw new InvalidOperationException(
                $"KeepHostedService: the application registers no hosted service {string.Join(", ", unknown)}. Register it with AddHostedService.");

        foreach (var service in hostedServices.Where(service => !_keptHostedServices.Contains(service.Type)))
            services.Remove(service.Descriptor);
    }

    // A hosted service registered with a factory is known by the type its factory returns.
    private static Type ImplementationOf(ServiceDescriptor service) =>
        service.ImplementationType ?? service.ImplementationInstance?.GetType() ?? service.ImplementationFactory!.Method.ReturnType;
}
