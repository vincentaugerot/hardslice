using System.Collections.Concurrent;
using System.Globalization;
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
    private static readonly DateTimeOffset Start = new(2100, 1, 4, 9, 0, 0, TimeSpan.Zero);
    private static readonly RecurringSchedule Nightly = RecurringSchedule.DailyAt(new TimeOnly(4, 30), "America/Toronto");

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
        var cycles = new Cycles(1);
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
        using var host = await StartAsync(cycles, configuration: new() { ["Ticks:Interval"] = "00:01:00" });
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
        var exception = await Should.ThrowAsync<OptionsValidationException>(
            () => StartAsync(new Cycles(), configuration: new() { ["Ticks:Interval"] = configuredInterval }));

        exception.Message.ShouldContain("Ticks:Interval, the interval of Tick, must be longer than zero");
    }

    [Fact]
    public void Refuses_a_command_registered_twice()
    {
        var services = new ServiceCollection().AddRecurringCommand<Tick, int>("Ticks", Interval);

        var exception = Should.Throw<InvalidOperationException>(() => services.AddRecurringCommand<Tick, int>("Ticks", Interval));

        exception.Message.ShouldContain("Tick is already sent at regular intervals");
    }

    // 2100-01-04 09:00 UTC is 04:00 in Toronto, UTC-5 in winter: the first cycle is half an hour later, the next one a day later.
    [Fact]
    public async Task Sends_a_new_command_every_day_at_its_local_time()
    {
        var cycles = new Cycles();
        var waits = new Waits();
        using var host = await StartAsync(cycles, Nightly, waits: waits);
        var clock = host.Services.GetRequiredService<FakeTimeProvider>();

        for (var night = 0; night < 2; night++)
        {
            var due = await waits.NextAsync();
            due.ShouldBe(new DateTimeOffset(2100, 1, 4 + night, 9, 30, 0, TimeSpan.Zero));
            clock.SetUtcNow(due - TimeSpan.FromMilliseconds(1));
            cycles.Sent.ShouldBe(night);
            clock.SetUtcNow(due);
            (await cycles.NextAsync()).ShouldBe(night + 1);
        }

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    // In Toronto, 2100-03-14 02:00 does not exist (the clocks go forward to 03:00) and 2100-11-07 01:00 to 02:00 happens twice
    // (back from EDT, UTC-4, to EST, UTC-5).
    [Theory]
    [InlineData("04:30", "2100-03-13T00:00:00-05:00", "2100-03-13T04:30:00-05:00", "2100-03-14T04:30:00-04:00")]
    [InlineData("02:30", "2100-03-13T12:00:00-05:00", "2100-03-14T03:30:00-04:00", "2100-03-15T02:30:00-04:00")]
    [InlineData("01:30", "2100-11-06T12:00:00-04:00", "2100-11-07T01:30:00-04:00", "2100-11-08T01:30:00-05:00")]
    [InlineData("04:30", "2100-11-06T12:00:00-04:00", "2100-11-07T04:30:00-05:00", "2100-11-08T04:30:00-05:00")]
    public void A_daily_time_runs_once_a_day_across_a_change_of_the_clocks(string time, string after, string first, string second)
    {
        var schedule = RecurringSchedule.DailyAt(TimeOnly.Parse(time, CultureInfo.InvariantCulture), "America/Toronto");

        var next = schedule.NextAfter(Start, DateTimeOffset.Parse(after, CultureInfo.InvariantCulture));
        var following = schedule.NextAfter(Start, next);

        next.ShouldBe(DateTimeOffset.Parse(first, CultureInfo.InvariantCulture));
        following.ShouldBe(DateTimeOffset.Parse(second, CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Sends_a_failed_cycle_again_at_each_retry_then_waits_for_the_next_night()
    {
        var cycles = new Cycles(1, 2, 3, 4);
        var waits = new Waits();
        var logs = new RecordingLoggerProvider();
        using var host = await StartAsync(cycles, Nightly.WithRetries(TimeSpan.FromHours(1), 3), waits: waits, logs: logs);
        var clock = host.Services.GetRequiredService<FakeTimeProvider>();
        var night = new DateTimeOffset(2100, 1, 4, 9, 30, 0, TimeSpan.Zero);

        for (var attempt = 0; attempt <= 3; attempt++)
        {
            var due = await waits.NextAsync();
            due.ShouldBe(night.AddHours(attempt));
            clock.SetUtcNow(due);
            (await cycles.NextAsync()).ShouldBe(attempt + 1);
        }

        (await waits.NextAsync()).ShouldBe(night.AddDays(1));
        logs.Entries.Where(entry => entry.Level == LogLevel.Error).Select(entry => entry.Message).ShouldBe([
            "Tick failed: retry 1 of 3 is sent in 01:00:00",
            "Tick failed: retry 2 of 3 is sent in 01:00:00",
            "Tick failed: retry 3 of 3 is sent in 01:00:00",
            "Tick failed: the next one is sent in 21:00:00"]);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_retry_that_succeeds_ends_the_retries()
    {
        var cycles = new Cycles(1);
        var waits = new Waits();
        using var host = await StartAsync(cycles, RecurringSchedule.Every(Interval).WithRetries(TimeSpan.FromSeconds(5), 3), waits: waits);
        var clock = host.Services.GetRequiredService<FakeTimeProvider>();

        foreach (var expected in new[] { Start + Interval, Start + Interval + TimeSpan.FromSeconds(5) })
        {
            var due = await waits.NextAsync();
            due.ShouldBe(expected);
            clock.SetUtcNow(due);
            await cycles.NextAsync();
        }

        (await waits.NextAsync()).ShouldBe(Start + 2 * Interval, "After a retry, the next cycle comes at the next instant of the interval.");
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Sends_a_command_that_an_endpoint_also_sends()
    {
        var cycles = new Cycles();
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        var clock = new FakeTimeProvider(Start);
        builder.Services
            .AddSingleton<TimeProvider>(clock)
            .AddSingleton(cycles)
            .AddMediator()
            .AddScoped<IRequestHandler<Ping, int>, PingHandler>()
            .AddRecurringCommand<Ping, int>("Pings", Interval);
        using var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);

        clock.Advance(Interval);

        (await cycles.NextAsync()).ShouldBe(1);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Reads_its_daily_time_its_time_zone_and_its_retries_from_its_configuration_section()
    {
        var cycles = new Cycles(1);
        var waits = new Waits();
        using var host = await StartAsync(cycles, Nightly, waits: waits, configuration: new()
        {
            ["Ticks:DailyAt"] = "06:15",
            ["Ticks:TimeZone"] = "UTC",
            ["Ticks:RetryDelay"] = "00:10:00",
            ["Ticks:Retries"] = "2",
        });
        var clock = host.Services.GetRequiredService<FakeTimeProvider>();

        var due = await waits.NextAsync();
        due.ShouldBe(new DateTimeOffset(2100, 1, 5, 6, 15, 0, TimeSpan.Zero));
        clock.SetUtcNow(due);
        await cycles.NextAsync();

        (await waits.NextAsync()).ShouldBe(due.AddMinutes(10));
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("Ticks:DailyAt", "04:30", "exactly one of them is set")]
    [InlineData("Ticks:TimeZone", "UTC", "Ticks:TimeZone, the time zone of Tick, must be the IANA id")]
    [InlineData("Ticks:Retries", "-1", "Ticks:Retries, the number of times Tick is sent again after a failed cycle, must be zero or more")]
    [InlineData("Ticks:Retries", "3", "Ticks:RetryDelay, the delay between two of them, longer than zero")]
    public async Task Refuses_to_start_with_a_schedule_it_cannot_follow(string key, string value, string message)
    {
        var exception = await Should.ThrowAsync<OptionsValidationException>(
            () => StartAsync(new Cycles(), configuration: new() { [key] = value }));

        exception.Message.ShouldContain(message);
    }

    [Fact]
    public async Task Refuses_to_start_with_a_time_zone_this_machine_does_not_know()
    {
        var exception = await Should.ThrowAsync<OptionsValidationException>(
            () => StartAsync(new Cycles(), RecurringSchedule.DailyAt(new TimeOnly(4, 30), "Europe/Nowhere")));

        exception.Message.ShouldContain("Ticks:TimeZone, the time zone of Tick, must be the IANA id of a time zone of this machine");
    }

    [Fact]
    public async Task Holds_the_command_back_while_its_condition_gives_a_reason_and_logs_it_once()
    {
        var cycles = new Cycles();
        var waits = new Waits();
        var logs = new RecordingLoggerProvider();
        var gate = new Gate { Reason = "Ticks:Key is not set" };
        using var host = await StartAsync(cycles, waits: waits, logs: logs, gate: gate);
        var clock = host.Services.GetRequiredService<FakeTimeProvider>();

        for (var cycle = 1; cycle <= 3; cycle++)
        {
            clock.SetUtcNow(await waits.NextAsync());
            (await waits.HeldAsync()).ShouldBe("Ticks:Key is not set");
        }

        cycles.Sent.ShouldBe(0);
        gate.Checks.ShouldBe(3);
        host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.IsCancellationRequested.ShouldBeFalse();
        logs.Messages("Tick is").ShouldBe(["Tick is sent every 00:00:30", "Tick is not sent: Ticks:Key is not set"]);
        logs.Entries.ShouldNotContain(entry => entry.Level >= LogLevel.Warning);

        gate.Reason = null;
        var due = await waits.NextAsync();
        due.ShouldBe(Start + 4 * Interval, "A held cycle is not retried: the next instant of the schedule comes.");
        clock.SetUtcNow(due);
        (await cycles.NextAsync()).ShouldBe(1);
        logs.Messages("Tick is").Last().ShouldBe("Tick is sent again: its conditions hold");
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Logs_the_reason_again_when_it_changes()
    {
        var cycles = new Cycles();
        var waits = new Waits();
        var logs = new RecordingLoggerProvider();
        var gate = new Gate { Reason = "Ticks:Key is not set" };
        using var host = await StartAsync(cycles, waits: waits, logs: logs, gate: gate);
        var clock = host.Services.GetRequiredService<FakeTimeProvider>();

        foreach (var reason in new[] { "Ticks:Key is not set", "Ticks:Key is not set", "a version covers the day", "a version covers the day" })
        {
            gate.Reason = reason;
            clock.SetUtcNow(await waits.NextAsync());
            (await waits.HeldAsync()).ShouldBe(reason);
        }

        logs.Messages("Tick is not sent").ShouldBe(["Tick is not sent: Ticks:Key is not set", "Tick is not sent: a version covers the day"]);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Checks_its_conditions_in_a_scope_of_their_own_before_each_send()
    {
        var cycles = new Cycles();
        var waits = new Waits();
        var gate = new Gate();
        using var host = await StartAsync(cycles, waits: waits, gate: gate);
        var clock = host.Services.GetRequiredService<FakeTimeProvider>();

        clock.SetUtcNow(await waits.NextAsync());
        (await cycles.NextAsync()).ShouldBe(1);

        gate.Checks.ShouldBe(1);
        gate.Scopes.ShouldHaveSingleItem().ShouldNotBeSameAs(cycles.Scopes.ShouldHaveSingleItem());
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_condition_that_throws_fails_the_cycle_and_its_retries_check_it_again()
    {
        var cycles = new Cycles();
        var waits = new Waits();
        var logs = new RecordingLoggerProvider();
        var gate = new Gate { Throws = true };
        using var host = await StartAsync(cycles, RecurringSchedule.Every(Interval).WithRetries(TimeSpan.FromSeconds(5), 1), waits: waits, logs: logs, gate: gate);
        var clock = host.Services.GetRequiredService<FakeTimeProvider>();

        clock.SetUtcNow(await waits.NextAsync());
        (await waits.NextAsync()).ShouldBe(Start + Interval + TimeSpan.FromSeconds(5));
        gate.Throws = false;
        clock.SetUtcNow(Start + Interval + TimeSpan.FromSeconds(5));

        (await cycles.NextAsync()).ShouldBe(1);
        gate.Checks.ShouldBe(2);
        var failure = logs.Entries.Where(entry => entry.Level == LogLevel.Error).ShouldHaveSingleItem();
        failure.Message.ShouldBe("Tick failed: retry 1 of 1 is sent in 00:00:05");
        failure.Exception.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("The condition fails.");
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    // A condition that holds back the cycle at start only, as an import at start that a stored version makes useless.
    [Theory]
    [InlineData(null, 2)]
    [InlineData("a version covers the day", 1)]
    public async Task Sends_the_command_at_start_when_its_condition_holds_then_at_each_instant(string? reasonAtStart, int sent)
    {
        var cycles = new Cycles();
        var waits = new Waits();
        var gate = new Gate { ReasonAtStart = reasonAtStart };
        using var host = await StartAsync(cycles, Nightly.AlsoAtStart(), waits: waits, gate: gate);
        var clock = host.Services.GetRequiredService<FakeTimeProvider>();
        var night = new DateTimeOffset(2100, 1, 4, 9, 30, 0, TimeSpan.Zero);

        (await waits.NextAsync()).ShouldBe(Start, "The cycle at start is due as soon as the host starts.");
        if (reasonAtStart is null)
            (await cycles.NextAsync()).ShouldBe(1);
        else
            (await waits.HeldAsync()).ShouldBe(reasonAtStart);
        (await waits.NextAsync()).ShouldBe(night, "Then the schedule goes on from its first instant.");
        clock.SetUtcNow(night);
        (await cycles.NextAsync()).ShouldBe(sent);

        gate.Cycles.ShouldBe([new RecurringCycle(Start, true, 0), new RecurringCycle(night, false, 0)]);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Tells_its_conditions_which_retry_of_the_cycle_at_start_they_decide_on()
    {
        var cycles = new Cycles(1);
        var waits = new Waits();
        var gate = new Gate();
        using var host = await StartAsync(cycles, Nightly.AlsoAtStart().WithRetries(TimeSpan.FromHours(1), 2), waits: waits, gate: gate);
        var clock = host.Services.GetRequiredService<FakeTimeProvider>();

        (await waits.NextAsync()).ShouldBe(Start);
        (await cycles.NextAsync()).ShouldBe(1);
        clock.SetUtcNow(await waits.NextAsync());
        (await cycles.NextAsync()).ShouldBe(2);

        gate.Cycles.ShouldBe([new RecurringCycle(Start, true, 0), new RecurringCycle(Start.AddHours(1), true, 1)]);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Reads_whether_it_sends_the_command_at_start_from_its_configuration_section()
    {
        var cycles = new Cycles();
        using var host = await StartAsync(cycles, configuration: new() { ["Ticks:AtStart"] = "true" });

        (await cycles.NextAsync()).ShouldBe(1);

        host.Services.GetRequiredService<FakeTimeProvider>().GetUtcNow().ShouldBe(Start);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<IHost> StartAsync(
        Cycles cycles,
        RecurringSchedule? schedule = null,
        Dictionary<string, string?>? configuration = null,
        Waits? waits = null,
        RecordingLoggerProvider? logs = null,
        Gate? gate = null)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        foreach (var (key, value) in configuration ?? [])
            builder.Configuration[key] = value;
        if (logs is not null)
            builder.Logging.AddProvider(logs);
        if (waits is not null)
            builder.Services.AddSingleton<IRecurringCommandMonitor<Tick, int>>(waits);
        if (gate is not null)
            builder.Services.AddSingleton(gate).AddScoped<IRecurringCommandCondition<Tick>, TickCondition>();

        var clock = new FakeTimeProvider(Start);
        builder.Services
            .AddSingleton(clock)
            .AddSingleton<TimeProvider>(clock)
            .AddSingleton(cycles)
            .AddScoped<ScopeMarker>()
            .AddMediator()
            .AddScoped<IRequestHandler<Tick, int>, TickHandler>()
            .AddRecurringCommand<Tick, int>("Ticks", schedule ?? RecurringSchedule.Every(Interval));

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

    // A command that an endpoint would also send: AddRecurringCommand takes any command without parameter.
    public sealed record Ping : ICommand<int>;

    // Counts the commands its handlers receive, and makes the cycles numbered failing throw.
    private sealed class Cycles(params int[] failing)
    {
        private readonly Channel<int> _completed = Channel.CreateUnbounded<int>();

        public ConcurrentQueue<object> Commands { get; } = new();

        // The scope of each command that reached the handler.
        public ConcurrentQueue<ScopeMarker> Scopes { get; } = new();

        public int Sent => Commands.Count;

        public int Record(object command)
        {
            Commands.Enqueue(command);
            var cycle = Commands.Count;
            _completed.Writer.TryWrite(cycle);
            if (failing.Contains(cycle))
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

    // One per scope of the container: tells the scope of a condition from the one of the command.
    private sealed class ScopeMarker;

    // What the condition of Tick answers: Reason, or ReasonAtStart for the cycle at start, null to send the command, or an
    // exception while Throws. It records the cycles and the scopes it decides in.
    private sealed class Gate
    {
        public string? Reason { get; set; }

        public string? ReasonAtStart { get; set; }

        public bool Throws { get; set; }

        public int Checks => Cycles.Count;

        public ConcurrentQueue<RecurringCycle> Cycles { get; } = new();

        public ConcurrentQueue<ScopeMarker> Scopes { get; } = new();

        public string? Check(RecurringCycle cycle, ScopeMarker scope)
        {
            Scopes.Enqueue(scope);
            Cycles.Enqueue(cycle);
            if (Throws)
                throw new InvalidOperationException("The condition fails.");
            return cycle.AtStart ? ReasonAtStart ?? Reason : Reason;
        }
    }

    private sealed class TickCondition(Gate gate, ScopeMarker scope) : IRecurringCommandCondition<Tick>
    {
        public Task<string?> CheckAsync(RecurringCycle cycle, CancellationToken cancellationToken) => Task.FromResult(gate.Check(cycle, scope));
    }

    // The instants that the service waits for, in order, and the reasons of the cycles that a condition held back.
    private sealed class Waits : IRecurringCommandMonitor<Tick, int>
    {
        private readonly Channel<DateTimeOffset> _waits = Channel.CreateUnbounded<DateTimeOffset>();
        private readonly Channel<string> _held = Channel.CreateUnbounded<string>();

        public void Waiting(DateTimeOffset due) => _waits.Writer.TryWrite(due);

        public void Completed(int response)
        {
        }

        public void Failed(Exception exception)
        {
        }

        public void Held(string reason) => _held.Writer.TryWrite(reason);

        // The next instant the service waits for, within 10 seconds.
        public Task<DateTimeOffset> NextAsync() => ReadAsync(_waits.Reader);

        // The reason of the next cycle that a condition held back, within 10 seconds.
        public Task<string> HeldAsync() => ReadAsync(_held.Reader);

        private static async Task<T> ReadAsync<T>(ChannelReader<T> reader)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            return await reader.ReadAsync(timeout.Token);
        }
    }

    private sealed class TickHandler(Cycles cycles, ScopeMarker scope) : IRequestHandler<Tick, int>
    {
        public Task<int> Handle(Tick command, CancellationToken cancellationToken)
        {
            cycles.Scopes.Enqueue(scope);
            return Task.FromResult(cycles.Record(command));
        }
    }

    private sealed class PingHandler(Cycles cycles) : IRequestHandler<Ping, int>
    {
        public Task<int> Handle(Ping command, CancellationToken cancellationToken) => Task.FromResult(cycles.Record(command));
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = new();

        // The messages that start with prefix, in order.
        public string[] Messages(string prefix) => [.. Entries.Select(entry => entry.Message).Where(message => message.StartsWith(prefix, StringComparison.Ordinal))];

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
