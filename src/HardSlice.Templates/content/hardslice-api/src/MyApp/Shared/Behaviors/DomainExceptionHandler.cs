using Microsoft.AspNetCore.Diagnostics;
using MyApp.Domain;

namespace MyApp.Shared.Behaviors;

internal sealed class DomainExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DomainException)
            return ValueTask.FromResult(false);

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        return problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = { Title = "A business rule was violated.", Detail = exception.Message }
        });
    }
}
