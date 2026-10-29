using System.Diagnostics;
using MyApp.Shared.Messaging;

namespace MyApp.Shared.Behaviors;

// Logs the request name, its duration and its outcome, never its content: requests may carry personal data.
internal sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var start = Stopwatch.GetTimestamp();
        try
        {
            var response = await next();
            logger.LogInformation("{RequestName} succeeded in {ElapsedMilliseconds} ms",
                requestName, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            return response;
        }
        catch (Exception exception)
        {
            // The exception type only: its message may quote the request's values.
            logger.LogWarning("{RequestName} failed with {ExceptionType} in {ElapsedMilliseconds} ms",
                requestName, exception.GetType().Name, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            throw;
        }
    }
}
