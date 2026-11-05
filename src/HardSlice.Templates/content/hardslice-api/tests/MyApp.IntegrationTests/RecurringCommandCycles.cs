using System.Runtime.ExceptionServices;
using System.Threading.Channels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using MyApp.Shared.Messaging;

namespace MyApp.IntegrationTests;

// Runs the cycles of a command that AddRecurringCommand sends, one at a time, in an instance of AppFactory that keeps its
// RecurringCommandService and replaces the clock of the application with Clock, a FakeTimeProvider set to the instant start of
// the constructor, TestInstants.Start by default: the application starts at that instant, its schedule counts from it, and its
// cycle at start of RecurringSchedule.AlsoAtStart is due then.
// RunNextAsync waits until the service waits for its next instant, moves Clock to it and waits for the end of the cycle it
// starts, whatever the schedule: an interval, a time of day or a retry. No test waits for the real instant. The other instances
// of AppFactory never start the recurring command.
public sealed class RecurringCommandCycles<TCommand, TResponse>
    where TCommand : ICommand<TResponse>, new()
{
    private static readonly TimeSpan CycleTimeout = TimeSpan.FromSeconds(30);

    private readonly CycleMonitor _monitor = new();
    private readonly WebApplicationFactory<Program> _app;
    private int _failures;
    private DateTimeOffset? _due;

    // Call it before the first request and the first use of Services of app. configure sets up the instance that runs the
    // cycles, as WithWebHostBuilder would, such as a fake HTTP handler of a client that the command reads, or a setting with
    // builder.UseSetting: it runs before the clock of the cycles replaces the one of the application. start is the instant at
    // which the application starts, such as 02:00 UTC for a cycle at start on the evening before in America/Toronto: Clock is
    // set to it before the host starts, so a test never moves Clock with SetUtcNow before the first use of Services.
    public RecurringCommandCycles(AppFactory app, Action<IWebHostBuilder>? configure = null, DateTimeOffset? start = null)
    {
        Clock = new FakeTimeProvider(start ?? TestInstants.Start);
        _app = app.KeepHostedService<RecurringCommandService<TCommand, TResponse>>().WithWebHostBuilder(builder =>
        {
            configure?.Invoke(builder);
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<TimeProvider>(Clock);
                services.AddSingleton<IRecurringCommandMonitor<TCommand, TResponse>>(_monitor);
                // The outermost behavior of the command, so that FailNext fails a cycle before the pipeline starts.
                var failing = ServiceDescriptor.Scoped<IPipelineBehavior<TCommand, TResponse>>(_ => new FailingCycles(this));
                var behaviors = services.TakeWhile(service =>
                    !(service.ServiceType.IsGenericType && service.ServiceType.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>))).Count();
                services.Insert(behaviors, failing);
            });
        });
    }

    public FakeTimeProvider Clock { get; }

    // The services of the instance that runs the cycles, such as a scope to read the database or the options of the module.
    public IServiceProvider Services => _app.Services;

    // Makes the next cycles, as many as cycles, throw a FailedCycleException before the pipeline of the command: for a test of
    // the retries of its schedule. An endpoint that sends the command in the meantime fails the same way.
    public void FailNext(int cycles) => Interlocked.Exchange(ref _failures, cycles);

    // The instant of the next cycle, once the service waits for it, without moving Clock.
    public async Task<DateTimeOffset> NextDueAsync()
    {
        _due ??= await ReadAsync(_monitor.Waits, "did not wait for a next instant");
        return _due.Value;
    }

    // Moves Clock to the instant of the next cycle, then returns the response of that cycle, or throws the exception that failed
    // it. RecurringCommandService logs that exception, then sends the command again or waits for the next instant of its schedule.
    // A cycle that an IRecurringCommandCondition<TCommand> holds back throws a HeldCycleException, whose message is the reason.
    // With RecurringSchedule.AlsoAtStart, the first cycle is due at the start of the constructor: RunNextAsync runs it without
    // moving Clock.
    public async Task<TResponse> RunNextAsync()
    {
        var due = await NextDueAsync();
        _due = null;
        if (due > Clock.GetUtcNow())
            Clock.SetUtcNow(due);

        var outcome = await ReadAsync(_monitor.Outcomes, $"was not sent within {CycleTimeout.TotalSeconds} s of moving the clock to its instant, {due:O}");
        return outcome();
    }

    private async Task<T> ReadAsync<T>(ChannelReader<T> reader, string failure)
    {
        _ = Services;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(CycleTimeout);
        try
        {
            return await reader.ReadAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!TestContext.Current.CancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"{typeof(TCommand).Name} {failure}.");
        }
    }

    private sealed class CycleMonitor : IRecurringCommandMonitor<TCommand, TResponse>
    {
        private readonly Channel<DateTimeOffset> _waits = Channel.CreateUnbounded<DateTimeOffset>();
        private readonly Channel<Func<TResponse>> _outcomes = Channel.CreateUnbounded<Func<TResponse>>();

        public ChannelReader<DateTimeOffset> Waits => _waits.Reader;

        public ChannelReader<Func<TResponse>> Outcomes => _outcomes.Reader;

        public void Waiting(DateTimeOffset due) => _waits.Writer.TryWrite(due);

        public void Completed(TResponse response) => _outcomes.Writer.TryWrite(() => response);

        public void Failed(Exception exception)
        {
            var captured = ExceptionDispatchInfo.Capture(exception);
            _outcomes.Writer.TryWrite(() =>
            {
                captured.Throw();
                return default!;
            });
        }

        public void Held(string reason) => _outcomes.Writer.TryWrite(() => throw new HeldCycleException(reason));
    }

    private sealed class FailingCycles(RecurringCommandCycles<TCommand, TResponse> cycles) : IPipelineBehavior<TCommand, TResponse>
    {
        public Task<TResponse> Handle(TCommand request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var left = Interlocked.Decrement(ref cycles._failures);
            if (left >= 0)
                throw new FailedCycleException($"{typeof(TCommand).Name} failed through FailNext, before its pipeline: {left} more to fail.");
            Interlocked.Exchange(ref cycles._failures, 0);
            return next();
        }
    }
}

// The failure that RecurringCommandCycles.FailNext gives a cycle.
public sealed class FailedCycleException(string message) : Exception(message);

// What RecurringCommandCycles.RunNextAsync throws for a cycle that a condition of the command held back: the command was not
// sent, and the message is the reason that the condition gave.
public sealed class HeldCycleException(string reason) : Exception(reason);
