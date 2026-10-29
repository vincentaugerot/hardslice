using Microsoft.AspNetCore.Diagnostics;
using MyApp.Domain;

namespace MyApp.Shared.Behaviors;

internal sealed class DomainExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DomainException)
            return ValueTask.FromResult(false);

        var (status, title) = exception is ConflictException
            ? (StatusCodes.Status409Conflict, "The request conflicts with the current state.")
            : (StatusCodes.Status400BadRequest, "A business rule was violated.");

        httpContext.Response.StatusCode = status;
        return problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = { Title = title, Detail = exception.Message }
        });
    }
}
