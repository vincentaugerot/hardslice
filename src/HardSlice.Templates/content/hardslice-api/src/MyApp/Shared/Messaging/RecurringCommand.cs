using Microsoft.Extensions.Options;

namespace MyApp.Shared.Messaging;

public static class RecurringCommandServiceCollectionExtensions
{
    // The longest period a timer accepts, about 49 days.
    private static readonly TimeSpan LongestInterval = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

    // Sends a new TCommand through IMediator every interval, the first one an interval after the application starts, for a
    // periodic task such as collecting a feed or purging old rows. The module of the command registers it, beside its handler.
    // The configuration key <section>:Interval, such as Realtime:CollectRealtimeFeeds:Interval, replaces the interval passed
    // here: an invalid one stops the start of the application.
    public static IServiceCollection AddRecurringCommand<TCommand, TResponse>(this IServiceCollection services, string section, TimeSpan interval)
        where TCommand : ISystemCommand<TResponse>, new()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(section);
        if (services.Any(service => service.ServiceType == typeof(IHostedService)
            && service.ImplementationType == typeof(RecurringCommandService<TCommand, TResponse>)))
        {
            throw new InvalidOperationException(
                $"AddRecurringCommand: {typeof(TCommand).Name} is already sent at regular intervals. Register it once, with one interval.");
        }

        services.AddOptions<RecurringCommandOptions<TCommand>>()
            .Configure(options => options.Interval = interval)
            .BindConfiguration(section)
            .Validate(
                options => options.Interval > TimeSpan.Zero && options.Interval <= LongestInterval,
                $"{section}:Interval, the interval of {typeof(TCommand).Name}, must be longer than zero and at most {LongestInterval}.")
            .ValidateOnStart();
        return services.AddHostedService<RecurringCommandService<TCommand, TResponse>>();
    }
}

// The interval of one recurring command, bound from its configuration section.
internal sealed class RecurringCommandOptions<TCommand>
{
    public TimeSpan Interval { get; set; }
}

// Sends a new TCommand at each tick of a PeriodicTimer on the TimeProvider of the container, each time in a scope of its own, as
// an HTTP request would: the command goes through the whole pipeline, its transaction included. A failed cycle is logged and
// the next one runs: the application keeps running. Cycles never overlap: a cycle longer than the interval delays the next one.
// The integration tests start it with AppFactory.KeepHostedService only.
internal sealed class RecurringCommandService<TCommand, TResponse>(
    IServiceScopeFactory scopes,
    TimeProvider timeProvider,
    IOptions<RecurringCommandOptions<TCommand>> options,
    ILogger<RecurringCommandService<TCommand, TResponse>> logger) : BackgroundService
    where TCommand : ISystemCommand<TResponse>, new()
{
    private PeriodicTimer? _timer;

    // The timer starts with the host, before ExecuteAsync runs in the background: a clock advanced right after the start
    // never misses its first tick.
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new PeriodicTimer(options.Value.Interval, timeProvider);
        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timer = _timer ?? throw new InvalidOperationException("StartAsync creates the timer before ExecuteAsync runs.");
        logger.LogInformation("{Command} is sent every {Interval}", typeof(TCommand).Name, options.Value.Interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await SendAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The application stops.
        }
    }

    private async Task SendAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new TCommand(), stoppingToken);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            // Since .NET 8, an exception that leaves ExecuteAsync stops the host. The command has no parameter, so its
            // exception quotes no value of a request.
            logger.LogError(exception, "{Command} failed: the next one is sent in {Interval}", typeof(TCommand).Name, options.Value.Interval);
        }
    }

    public override void Dispose()
    {
        _timer?.Dispose();
        base.Dispose();
    }
}
