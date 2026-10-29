using Microsoft.Extensions.Logging;
using MyApp.Shared.Behaviors;
using MyApp.Shared.Messaging;

namespace MyApp.Tests.Shared.Behaviors;

public sealed class LoggingBehaviorTests
{
    private const string Secret = "alice@example.com";

    [Fact]
    public async Task Logs_the_name_duration_and_success_of_a_request_without_its_content()
    {
        var logger = new RecordingLogger();

        await new LoggingBehavior<SampleCommand, int>(logger)
            .Handle(new SampleCommand(Secret), () => Task.FromResult(1), TestContext.Current.CancellationToken);

        var entry = logger.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Information);
        entry.Message.ShouldStartWith("SampleCommand succeeded in ");
        entry.Message.ShouldEndWith(" ms");
        entry.Message.ShouldNotContain(Secret);
    }

    [Fact]
    public async Task Logs_the_exception_type_of_a_failed_request_without_its_message_and_rethrows()
    {
        var logger = new RecordingLogger();

        await Should.ThrowAsync<InvalidOperationException>(() => new LoggingBehavior<SampleCommand, int>(logger).Handle(
            new SampleCommand(Secret), () => throw new InvalidOperationException($"Unknown customer {Secret}."), TestContext.Current.CancellationToken));

        var entry = logger.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Message.ShouldStartWith("SampleCommand failed with InvalidOperationException in ");
        entry.Message.ShouldNotContain(Secret);
    }

    public sealed record SampleCommand(string Email) : ICommand<int>;

    private sealed class RecordingLogger : ILogger<LoggingBehavior<SampleCommand, int>>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));

        public bool IsEnabled(LogLevel logLevel) => true;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    }
}
