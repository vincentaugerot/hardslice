using System.Runtime.ExceptionServices;
using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using MyApp.Shared.Messaging;

namespace MyApp.IntegrationTests;

// Runs the cycles of a command that AddRecurringCommand sends, one at a time, in an instance of AppFactory that keeps its
// RecurringCommandService and replaces the clock of the application with Clock, a FakeTimeProvider set to TestInstants.Start.
// RunNextAsync advances Clock by the interval of the command and waits for the end of the cycle it starts: no test waits for the
// real interval. The other instances of AppFactory never start the recurring command.
public sealed class RecurringCommandCycles<TCommand, TResponse>
    where TCommand : ISystemCommand<TResponse>, new()
{
    private static readonly TimeSpan CycleTimeout = TimeSpan.FromSeconds(30);

    private readonly Channel<Func<TResponse>> _outcomes = Channel.CreateUnbounded<Func<TResponse>>();
    private readonly WebApplicationFactory<Program> _app;

    // Call it before the first request and the first use of Services of app.
    public RecurringCommandCycles(AppFactory app)
    {
        _app = app.KeepHostedService<RecurringCommandService<TCommand, TResponse>>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<TimeProvider>(Clock);
                // The outermost behavior of the command: it sees the outcome of the whole pipeline, as RecurringCommandService does.
                var probe = ServiceDescriptor.Scoped<IPipelineBehavior<TCommand, TResponse>>(_ => new CycleProbe(_outcomes.Writer));
                var behaviors = services.TakeWhile(service =>
                    !(service.ServiceType.IsGenericType && service.ServiceType.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>))).Count();
                services.Insert(behaviors, probe);
            }));
    }

    public FakeTimeProvider Clock { get; } = new(TestInstants.Start);

    // The services of the instance that runs the cycles, such as a scope to read the database or the options of the module.
    public IServiceProvider Services => _app.Services;

    // Advances Clock by the interval of the command, then returns the response of the cycle that it starts, or throws the exception
    // that failed it. RecurringCommandService logs that exception and runs the next cycle.
    public async Task<TResponse> RunNextAsync()
    {
        var interval = Services.GetRequiredService<IOptions<RecurringCommandOptions<TCommand>>>().Value.Interval;
        Clock.Advance(interval);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(CycleTimeout);
        try
        {
            var outcome = await _outcomes.Reader.ReadAsync(timeout.Token);
            return outcome();
        }
        catch (OperationCanceledException) when (!TestContext.Current.CancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"{typeof(TCommand).Name} was not sent within {CycleTimeout.TotalSeconds} s of advancing the clock by its interval, {interval}.");
        }
    }

    private sealed class CycleProbe(ChannelWriter<Func<TResponse>> outcomes) : IPipelineBehavior<TCommand, TResponse>
    {
        public async Task<TResponse> Handle(TCommand request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            try
            {
                var response = await next();
                outcomes.TryWrite(() => response);
                return response;
            }
            catch (Exception exception)
            {
                var captured = ExceptionDispatchInfo.Capture(exception);
                outcomes.TryWrite(() =>
                {
                    captured.Throw();
                    return default!;
                });
                throw;
            }
        }
    }
}
