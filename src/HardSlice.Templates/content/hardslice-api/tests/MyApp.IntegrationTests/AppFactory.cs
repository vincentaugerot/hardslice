using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyApp.Shared.Messaging;
using MyApp.Shared.Persistence;

namespace MyApp.IntegrationTests;

// One running instance of the application on the test database. Several factories on the same database act as several instances.
// It runs in its own environment, IntegrationTests: the user secrets of the developer, which only Development loads, never reach a
// test. It keeps what Development changes for the tests: every service registration is validated when the host starts, and a request
// that cannot be read is thrown to the exception handlers. No hosted service that the application registers starts, those of its
// packages included, unless the test keeps it with KeepHostedService, and each one it removes is logged before the host starts.
// ASP.NET Core registers its own after them: they always run.
// An endpoint that requires an authorization policy is called with app.CreateAuthorizedClient(policy), from AuthorizedClients.
// The test of the 409 of two commands that change the same aggregate holds the first one before its commit with
// app.HoldBeforeCommit<TCommand>(), lets the second one commit, then releases the first, which saves nothing and throws
// ConflictException, answered with 409.
// A store outside the database that the application registers in Shared/Persistence, such as a file or object store, gets a copy of
// its own for each instance, given in ConfigureWebHost, such as a temporary folder by builder.UseSetting and deleted with the
// factory: no test writes to the store of the developer, nor reads what another instance wrote.
public sealed class AppFactory : WebApplicationFactory<Program>
{
    public const string TestEnvironment = "IntegrationTests";

    private readonly string _connectionString;

    // An instance on the database that the tests share.
    public AppFactory(PostgresDatabase database) => _connectionString = database.ConnectionString;

    // An instance on a database of one test alone, from database.CreateIsolatedAsync(), as the tests of a job over every row that
    // matches a rule start it. It behaves as one on the shared database.
    public AppFactory(IsolatedDatabase database) => _connectionString = database.ConnectionString;

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

    private readonly CommitHolds _commitHolds = new();

    // Holds the next command TCommand that this instance runs, or an instance derived from it with WithWebHostBuilder: once its
    // handler has returned, before TransactionBehavior publishes its domain events, saves and commits, until the test calls
    // Release() on the hold it returns. await hold.WaitUntilHeldAsync() waits until the command is there, still uncommitted: a
    // second command that changes the same aggregate then loads what the first one loaded, commits, and the first one, released,
    // saves nothing and throws ConflictException. An aggregate that a command leaves unchanged writes nothing and meets no token:
    // the second command must change it too. Commands of another type, and those of TCommand after the first, are never held.
    // It can be called at any time. Disposing of the hold or of this instance releases it.
    public CommitHold HoldBeforeCommit<TCommand>() where TCommand : notnull
    {
        if (!typeof(TCommand).GetInterfaces().Any(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ICommand<>)))
            throw new InvalidOperationException(
                $"HoldBeforeCommit: {Describe(typeof(TCommand))} is no ICommand<TResponse>. Only a command runs in a transaction that commits.");

        return _commitHolds.Add(typeof(TCommand));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(TestEnvironment);
        builder.UseDefaultServiceProvider(options => options.ValidateScopes = options.ValidateOnBuild = true);
        builder.UseSetting($"ConnectionStrings:{PersistenceServiceCollectionExtensions.ConnectionStringName}", _connectionString);
        builder.ConfigureTestServices(services =>
        {
            services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
            RemoveHostedServicesOfTheApplication(services);
            // Registered after the behaviors of the application, so inside TransactionBehavior: between the handler and the commit.
            services.AddSingleton(_commitHolds);
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(HoldBeforeCommitBehavior<,>));
        });
    }

    public override async ValueTask DisposeAsync()
    {
        _commitHolds.ReleaseAll();
        await base.DisposeAsync();
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

    // A command held before its commit by HoldBeforeCommit<TCommand>().
    public sealed class CommitHold : IDisposable
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

        private readonly TaskCompletionSource _held = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal CommitHold(Type command) => Command = command;

        internal Type Command { get; }

        // Waits until the command is held, for 30 seconds at most.
        public async Task WaitUntilHeldAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _held.Task.WaitAsync(Timeout, cancellationToken);
            }
            catch (TimeoutException)
            {
                throw new TimeoutException(
                    $"HoldBeforeCommit: no command {Describe(Command)} reached its commit within {Timeout.TotalSeconds} s. Send it after "
                    + "HoldBeforeCommit, to this AppFactory or an instance derived from it, and check that its validation and its handler passed.");
            }
        }

        // Lets the command save and commit.
        public void Release() => _released.TrySetResult();

        public void Dispose() => Release();

        internal async Task HoldAsync(CancellationToken cancellationToken)
        {
            _held.TrySetResult();
            await _released.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class CommitHolds
    {
        private readonly List<CommitHold> _waiting = [];
        private readonly List<CommitHold> _all = [];

        public CommitHold Add(Type command)
        {
            var hold = new CommitHold(command);
            lock (_waiting)
            {
                _waiting.Add(hold);
                _all.Add(hold);
            }

            return hold;
        }

        // The first hold of that command that holds none yet.
        public CommitHold? Take(Type command)
        {
            lock (_waiting)
            {
                var hold = _waiting.FirstOrDefault(hold => hold.Command == command);
                if (hold is not null)
                    _waiting.Remove(hold);
                return hold;
            }
        }

        public void ReleaseAll()
        {
            lock (_waiting)
            {
                foreach (var hold in _all)
                    hold.Release();
            }
        }
    }

    private sealed class HoldBeforeCommitBehavior<TCommand, TResponse>(CommitHolds holds) : IPipelineBehavior<TCommand, TResponse>
        where TCommand : ICommand<TResponse>
    {
        public async Task<TResponse> Handle(TCommand command, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var response = await next();
            if (holds.Take(typeof(TCommand)) is { } hold)
                await hold.HoldAsync(cancellationToken);
            return response;
        }
    }
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
