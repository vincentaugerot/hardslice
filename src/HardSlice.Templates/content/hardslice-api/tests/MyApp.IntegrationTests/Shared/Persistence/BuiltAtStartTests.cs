using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyApp.Shared.Persistence;

namespace MyApp.IntegrationTests.Shared.Persistence;

// A service registered with AddSingletonBuiltAtStart is built when the application starts, under AppFactory too, which starts no
// hosted service of the application: a factory that throws fails the start, before any request.
public sealed class BuiltAtStartTests(PostgresDatabase database)
{
    [Fact]
    public void Builds_the_service_when_the_application_starts_before_any_request()
    {
        var builds = 0;
        using var app = new AppFactory(database).WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingletonBuiltAtStart(_ =>
            {
                builds++;
                return new ReferenceData("2026");
            })));

        _ = app.Services;

        builds.ShouldBe(1);
        app.Services.GetRequiredService<ReferenceData>().Edition.ShouldBe("2026");
        builds.ShouldBe(1);
    }

    [Fact]
    public void Fails_the_start_of_the_application_when_its_factory_throws()
    {
        var logs = new ErrorLogs();
        using var app = new AppFactory(database).WithWebHostBuilder(builder => builder
            .ConfigureLogging(logging => logging.AddProvider(logs))
            .ConfigureTestServices(services =>
                services.AddSingletonBuiltAtStart<ReferenceData>(_ => throw new InvalidDataException("data/2026/rates.json: $.rate is missing."))));

        // Program runs the host on its own thread and disposes it once its start failed. WebApplicationFactory throws the exception of
        // the factory, or ObjectDisposedException when Program disposed the host first. The host logs the exception before either.
        var thrown = Should.Throw<Exception>(() => app.CreateClient());
        if (thrown is not ObjectDisposedException)
            thrown.ShouldBeOfType<InvalidDataException>().Message.ShouldBe("data/2026/rates.json: $.rate is missing.");
        logs.Exceptions.ShouldContain(exception =>
            exception is InvalidDataException && exception.Message == "data/2026/rates.json: $.rate is missing.");
    }

    private sealed record ReferenceData(string Edition);

    // The exceptions that the application logs as errors.
    private sealed class ErrorLogs : ILoggerProvider
    {
        public ConcurrentQueue<Exception> Exceptions { get; } = new();

        public ILogger CreateLogger(string categoryName) => new Logger(Exceptions);

        public void Dispose()
        {
        }

        private sealed class Logger(ConcurrentQueue<Exception> exceptions) : ILogger
        {
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (logLevel >= LogLevel.Error && exception is not null)
                    exceptions.Enqueue(exception);
            }

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        }
    }
}
