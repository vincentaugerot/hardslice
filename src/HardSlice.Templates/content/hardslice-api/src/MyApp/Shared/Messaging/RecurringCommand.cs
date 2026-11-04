using System.Globalization;
using Microsoft.Extensions.Options;

namespace MyApp.Shared.Messaging;

public static class RecurringCommandServiceCollectionExtensions
{
    // The longest delay a timer accepts, about 49 days.
    internal static readonly TimeSpan LongestDelay = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

    // Sends a new TCommand through IMediator every interval, the first one an interval after the application starts, for a
    // periodic task such as collecting a feed or purging old rows. The module of the command registers it, beside its handler.
    // The configuration key <section>:Interval, such as Realtime:CollectRealtimeFeeds:Interval, replaces the interval passed
    // here: an invalid one stops the start of the application.
    public static IServiceCollection AddRecurringCommand<TCommand, TResponse>(this IServiceCollection services, string section, TimeSpan interval)
        where TCommand : ICommand<TResponse>, new() =>
        services.AddRecurringCommand<TCommand, TResponse>(section, RecurringSchedule.Every(interval));

    // Sends a new TCommand through IMediator at each instant of the schedule, such as every night at 04:30 in America/Toronto
    // with RecurringSchedule.DailyAt(new TimeOnly(4, 30), "America/Toronto"), and sends it again after a failed cycle with
    // WithRetries. Any command without parameter: a system command that only the application sends, or a command that its
    // endpoint also sends. The keys Interval, DailyAt, TimeZone, RetryDelay and Retries of the configuration section replace the
    // values passed here, such as Schedule:ImportNightlySchedule:DailyAt: an invalid one stops the start of the application.
    public static IServiceCollection AddRecurringCommand<TCommand, TResponse>(this IServiceCollection services, string section, RecurringSchedule schedule)
        where TCommand : ICommand<TResponse>, new()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(section);
        ArgumentNullException.ThrowIfNull(schedule);
        if (services.Any(service => service.ServiceType == typeof(IHostedService)
            && service.ImplementationType == typeof(RecurringCommandService<TCommand, TResponse>)))
        {
            throw new InvalidOperationException(
                $"AddRecurringCommand: {typeof(TCommand).Name} is already sent at regular intervals. Register it once, with one schedule.");
        }

        var command = typeof(TCommand).Name;
        services.AddOptions<RecurringCommandOptions<TCommand>>()
            .Configure(options =>
            {
                options.Interval = schedule.Interval;
                options.DailyAt = schedule.TimeOfDay;
                options.TimeZone = schedule.TimeZone;
                options.RetryDelay = schedule.RetryDelay;
                options.Retries = schedule.Retries;
            })
            .BindConfiguration(section)
            .Validate(
                options => (options.Interval is null) != (options.DailyAt is null),
                $"{section}: {command} is sent either every {section}:Interval or every day at {section}:DailyAt, so exactly one of them is set.")
            .Validate(
                options => options.Interval is null || (options.Interval > TimeSpan.Zero && options.Interval <= LongestDelay),
                $"{section}:Interval, the interval of {command}, must be longer than zero and at most {LongestDelay}.")
            .Validate(
                options => options.DailyAt is null ? options.TimeZone is null : RecurringSchedule.FindTimeZone(options.TimeZone) is not null,
                $"{section}:TimeZone, the time zone of {command}, must be the IANA id of a time zone of this machine, such as America/Toronto or UTC, "
                + $"and is set with {section}:DailyAt only.")
            .Validate(
                options => options.Retries >= 0 && (options.Retries == 0 || (options.RetryDelay > TimeSpan.Zero && options.RetryDelay <= LongestDelay)),
                $"{section}:Retries, the number of times {command} is sent again after a failed cycle, must be zero or more, and {section}:RetryDelay, "
                + $"the delay between two of them, longer than zero and at most {LongestDelay} when there are any.")
            .ValidateOnStart();
        return services.AddHostedService<RecurringCommandService<TCommand, TResponse>>();
    }
}

// When AddRecurringCommand sends its command: every interval from the start of the application, or every day at a local time of
// a time zone, and how many times it sends it again after a failed cycle. A local time that a change of the clocks skips moves
// forward by the length of the change, such as 02:30 to 03:30, and one that the clocks repeat is sent once, at its first instant.
public sealed record RecurringSchedule
{
    private RecurringSchedule()
    {
    }

    public TimeSpan? Interval { get; private init; }

    // The local time of a daily schedule, in TimeZone.
    public TimeOnly? TimeOfDay { get; private init; }

    // The IANA id of the time zone of TimeOfDay, such as America/Toronto, or UTC.
    public string? TimeZone { get; private init; }

    public TimeSpan? RetryDelay { get; private init; }

    public int Retries { get; private init; }

    public static RecurringSchedule Every(TimeSpan interval) => new() { Interval = interval };

    public static RecurringSchedule DailyAt(TimeOnly time, string timeZone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZone);
        return new() { TimeOfDay = time, TimeZone = timeZone };
    }

    // After a failed cycle, sends the command again at most retries times, delay apart, then waits for the next instant of the
    // schedule after the last one. A retry that succeeds ends the retries.
    public RecurringSchedule WithRetries(TimeSpan delay, int retries) => this with { RetryDelay = delay, Retries = retries };

    public override string ToString() =>
        (Interval is { } interval ? $"every {interval}" : "every day at " + TimeOfDay?.ToString("HH':'mm", CultureInfo.InvariantCulture) + " in " + TimeZone)
        + (Retries > 0 ? $", sent again at most {Retries} times {RetryDelay} apart after a failed cycle" : "");

    // The first instant of the schedule after after: on the grid of Interval from start, or the next TimeOfDay of the time zone.
    internal DateTimeOffset NextAfter(DateTimeOffset start, DateTimeOffset after)
    {
        if (Interval is { } interval)
            return after < start ? start + interval : start + TimeSpan.FromTicks(interval.Ticks * ((after - start).Ticks / interval.Ticks + 1));

        var zone = FindTimeZone(TimeZone) ?? throw new InvalidOperationException($"No time zone {TimeZone} on this machine.");
        var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(after, zone).DateTime);
        for (var days = 0; days <= 2; days++)
        {
            var instant = InstantOf(day.AddDays(days), TimeOfDay!.Value, zone);
            if (instant > after)
                return instant;
        }

        throw new InvalidOperationException($"No instant of {this} within two days of {after:O}.");
    }

    internal static TimeZoneInfo? FindTimeZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return null;
        }
    }

    private static DateTimeOffset InstantOf(DateOnly day, TimeOnly time, TimeZoneInfo zone)
    {
        var local = day.ToDateTime(time, DateTimeKind.Unspecified);
        // A skipped time is read with the offset before the change, the standard one: 02:30 becomes 03:30 when the clocks go
        // forward an hour. A repeated time takes its larger offset, the first of its two instants.
        var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }
}

// The schedule of one recurring command, bound from its configuration section.
internal sealed class RecurringCommandOptions<TCommand>
{
    public TimeSpan? Interval { get; set; }

    public TimeOnly? DailyAt { get; set; }

    public string? TimeZone { get; set; }

    public TimeSpan? RetryDelay { get; set; }

    public int Retries { get; set; }

    public RecurringSchedule ToSchedule()
    {
        var schedule = Interval is { } interval ? RecurringSchedule.Every(interval) : RecurringSchedule.DailyAt(DailyAt!.Value, TimeZone!);
        return Retries > 0 ? schedule.WithRetries(RetryDelay!.Value, Retries) : schedule;
    }
}

// What RecurringCommandService tells the integration tests that watch it, through RecurringCommandCycles: the application
// registers none.
internal interface IRecurringCommandMonitor<TCommand, TResponse>
{
    // The instant of the next cycle, before the service waits for it.
    void Waiting(DateTimeOffset due);

    void Completed(TResponse response);

    void Failed(Exception exception);
}

// Sends a new TCommand at each instant of its schedule, on the TimeProvider of the container, each time in a scope of its own,
// as an HTTP request would: the command goes through the whole pipeline, its transaction included. A failed cycle is logged and
// sent again as many times as the schedule retries it, then the next instant of the schedule comes: the application keeps
// running. Cycles never overlap: a cycle that ends after the next instant is followed at once by one cycle, and the schedule goes
// on from there. The integration tests start it with AppFactory.KeepHostedService only.
internal sealed class RecurringCommandService<TCommand, TResponse>(
    IServiceScopeFactory scopes,
    TimeProvider timeProvider,
    IOptions<RecurringCommandOptions<TCommand>> options,
    IEnumerable<IRecurringCommandMonitor<TCommand, TResponse>> monitors,
    ILogger<RecurringCommandService<TCommand, TResponse>> logger) : BackgroundService
    where TCommand : ICommand<TResponse>, new()
{
    private DateTimeOffset _start;

    // The instants are fixed from the start of the host, before ExecuteAsync runs in the background: a clock advanced right
    // after the start never misses the first one.
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _start = timeProvider.GetUtcNow();
        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var schedule = options.Value.ToSchedule();
        var command = typeof(TCommand).Name;
        logger.LogInformation("{Command} is sent {Schedule}", command, schedule);
        var due = schedule.NextAfter(_start, _start);
        var retry = 0;
        try
        {
            while (true)
            {
                due = await WaitAsync(due, stoppingToken);
                var failure = await SendAsync(stoppingToken);
                if (failure is null)
                {
                    if (retry > 0)
                        logger.LogInformation("{Command} succeeded at retry {Retry} of {Retries}", command, retry, schedule.Retries);
                    retry = 0;
                    due = schedule.NextAfter(_start, due);
                }
                else if (retry < schedule.Retries)
                {
                    retry++;
                    due += schedule.RetryDelay!.Value;
                    logger.LogError(failure, "{Command} failed: retry {Retry} of {Retries} is sent in {Delay}", command, retry, schedule.Retries, DelayUntil(due));
                }
                else
                {
                    retry = 0;
                    due = schedule.NextAfter(_start, due);
                    logger.LogError(failure, "{Command} failed: the next one is sent in {Delay}", command, DelayUntil(due));
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The application stops.
        }
    }

    // Waits for due, or not at all when it has passed: that late cycle is due now, and the schedule goes on from it. The timer
    // starts before the monitors hear of due, so a test that moves its clock to due once it heard of it always wakes the service.
    private async Task<DateTimeOffset> WaitAsync(DateTimeOffset due, CancellationToken stoppingToken)
    {
        var now = timeProvider.GetUtcNow();
        if (due <= now)
            due = now;
        var delay = due > now ? Task.Delay(due - now, timeProvider, stoppingToken) : Task.CompletedTask;
        foreach (var monitor in monitors)
            monitor.Waiting(due);
        await delay;
        return due;
    }

    // The exception that failed the cycle, or null.
    private async Task<Exception?> SendAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var response = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new TCommand(), stoppingToken);
            foreach (var monitor in monitors)
                monitor.Completed(response);
            return null;
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            // Since .NET 8, an exception that leaves ExecuteAsync stops the host. The command has no parameter, so its
            // exception quotes no value of a request.
            foreach (var monitor in monitors)
                monitor.Failed(exception);
            return exception;
        }
    }

    // The delay until due, to the second, for the logs.
    private TimeSpan DelayUntil(DateTimeOffset due) =>
        TimeSpan.FromSeconds(Math.Max(0, Math.Round((due - timeProvider.GetUtcNow()).TotalSeconds)));
}
