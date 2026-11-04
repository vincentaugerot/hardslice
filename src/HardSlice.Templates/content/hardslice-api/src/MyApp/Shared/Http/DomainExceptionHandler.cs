using Microsoft.AspNetCore.Diagnostics;
using MyApp.Domain;

namespace MyApp.Shared.Http;

internal sealed class DomainExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DomainException)
            return false;

        var (status, title) = exception is ConflictException
            ? (StatusCodes.Status409Conflict, "The request conflicts with the current state.")
            : (StatusCodes.Status400BadRequest, "A business rule was violated.");

        httpContext.Response.StatusCode = status;
        // The status is the answer: a client whose Accept header refuses problem details still gets it, without a body.
        await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = { Title = title, Detail = exception.Message }
        });
        return true;
    }
}
