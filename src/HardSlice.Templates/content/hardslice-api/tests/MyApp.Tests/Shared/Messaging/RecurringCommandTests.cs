using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using MyApp.Shared.Messaging;

namespace MyApp.Tests.Shared.Messaging;

// AddRecurringCommand on a host of its own and a FakeTimeProvider: each cycle runs when the test advances the clock, without
// any real wait.
public sealed class RecurringCommandTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Sends_a_new_command_at_each_interval_and_never_before_the_first_one()
    {
        var cycles = new Cycles();
        using var host = await StartAsync(cycles);
        var clock = host.Services.GetRequiredService<FakeTimeProvider>();

        clock.Advance(Interval - TimeSpan.FromMilliseconds(1));
        cycles.Sent.ShouldBe(0);

        for (var cycle = 1; cycle <= 3; cycle++)
        {
            clock.Advance(cycle == 1 ? TimeSpan.FromMilliseconds(1) : Interval);
            (await cycles.NextAsync()).ShouldBe(cycle);
        }

        cycles.Commands.Distinct(ReferenceEqualityComparer.Instance).Count().ShouldBe(3, "Each cycle sends a new command.");
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Logs_a_failed_cycle_and_runs_the_next_one_without_stopping_the_host()
    {
        var cycles = new Cycles(failing: 1);
        var logs = new RecordingLoggerProvider();
        using var host = await StartAsync(cycles, logs: logs);
        var clock = host.Services.GetRequiredService<FakeTimeProvider>();

        clock.Advance(Interval);
        (await cycles.NextAsync()).ShouldBe(1);
        clock.Advance(Interval);
        (await cycles.NextAsync()).ShouldBe(2);

        host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.IsCancellationRequested.ShouldBeFalse();
        var failure = logs.Entries.Where(entry => entry.Level == LogLevel.Error).ShouldHaveSingleItem();
        failure.Message.ShouldBe("Tick failed: the next one is sent in 00:00:30");
        failure.Exception.ShouldBeOfType<InvalidOperationException>();
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Stops_with_the_host_while_it_waits_for_the_next_cycle()
    {
        var cycles = new Cycles();
        using var host = await StartAsync(cycles);
        var service = host.Services.GetServices<IHostedService>().OfType<RecurringCommandService<Tick, int>>().ShouldHaveSingleItem();
        // After a first cycle, the service waits for the next tick: stopping before, it may never have run.
        host.Services.GetRequiredService<FakeTimeProvider>().Advance(Interval);
        (await cycles.NextAsync()).ShouldBe(1);

        await host.StopAsync(TestContext.Current.CancellationToken);

        service.ExecuteTask.ShouldNotBeNull().IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public async Task Reads_its_interval_from_its_configuration_section()
    {
        var cycles = new Cycles();
        using var host = await StartAsync(cycles, configuredInterval: "00:01:00");
        var clock = host.Services.GetRequiredService<FakeTimeProvider>();

        clock.Advance(Interval);
        cycles.Sent.ShouldBe(0);
        clock.Advance(Interval);

        (await cycles.NextAsync()).ShouldBe(1);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("00:00:00")]
    [InlineData("-00:00:30")]
    [InlineData("50.00:00:00")]
    public async Task Refuses_to_start_with_an_interval_a_timer_cannot_wait(string configuredInterval)
    {
        var exception = await Should.ThrowAsync<OptionsValidationException>(() => StartAsync(new Cycles(), configuredInterval: configuredInterval));

        exception.Message.ShouldContain("Ticks:Interval, the interval of Tick, must be longer than zero");
    }

    [Fact]
    public void Refuses_a_command_registered_twice()
    {
        var services = new ServiceCollection().AddRecurringCommand<Tick, int>("Ticks", Interval);

        var exception = Should.Throw<InvalidOperationException>(() => services.AddRecurringCommand<Tick, int>("Ticks", Interval));

        exception.Message.ShouldContain("Tick is already sent at regular intervals");
    }

    private static async Task<IHost> StartAsync(Cycles cycles, string? configuredInterval = null, RecordingLoggerProvider? logs = null)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        if (configuredInterval is not null)
            builder.Configuration["Ticks:Interval"] = configuredInterval;
        if (logs is not null)
            builder.Logging.AddProvider(logs);

        var clock = new FakeTimeProvider(new DateTimeOffset(2100, 1, 4, 9, 0, 0, TimeSpan.Zero));
        builder.Services
            .AddSingleton(clock)
            .AddSingleton<TimeProvider>(clock)
            .AddSingleton(cycles)
            .AddMediator()
            .AddScoped<IRequestHandler<Tick, int>, TickHandler>()
            .AddRecurringCommand<Tick, int>("Ticks", Interval);

        var host = builder.Build();
        try
        {
            await host.StartAsync(TestContext.Current.CancellationToken);
            return host;
        }
        catch
        {
            host.Dispose();
            throw;
        }
    }

    public sealed record Tick : ISystemCommand<int>;

    // Counts the commands its handler receives, and makes the cycle numbered failing throw.
    private sealed class Cycles(int failing = 0)
    {
        private readonly Channel<int> _completed = Channel.CreateUnbounded<int>();

        public ConcurrentQueue<Tick> Commands { get; } = new();

        public int Sent => Commands.Count;

        public int Record(Tick command)
        {
            Commands.Enqueue(command);
            var cycle = Commands.Count;
            _completed.Writer.TryWrite(cycle);
            if (cycle == failing)
                throw new InvalidOperationException($"Cycle {cycle} fails.");
            return cycle;
        }

        // The number of the next cycle that reached the handler, within 10 seconds.
        public async Task<int> NextAsync()
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            return await _completed.Reader.ReadAsync(timeout.Token);
        }
    }

    private sealed class TickHandler(Cycles cycles) : IRequestHandler<Tick, int>
    {
        public Task<int> Handle(Tick command, CancellationToken cancellationToken) => Task.FromResult(cycles.Record(command));
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new Logger(Entries);

        public void Dispose()
        {
        }

        private sealed class Logger(ConcurrentQueue<(LogLevel Level, string Message, Exception? Exception)> entries) : ILogger
        {
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                entries.Enqueue((logLevel, formatter(state, exception), exception));

            public bool IsEnabled(LogLevel logLevel) => true;

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        }
    }
}
