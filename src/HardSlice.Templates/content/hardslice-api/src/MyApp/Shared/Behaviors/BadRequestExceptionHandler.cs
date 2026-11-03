using Microsoft.AspNetCore.Diagnostics;

namespace MyApp.Shared.Behaviors;

// A request that ASP.NET Core cannot read, such as a malformed JSON body or a date without its offset, answered with its status,
// 400 in general. In Development, minimal APIs throw it instead of answering: without this handler, it would become a 500.
internal sealed class BadRequestExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException badRequest)
            return false;

        httpContext.Response.StatusCode = badRequest.StatusCode;
        // The status is the answer: a client whose Accept header refuses problem details still gets it, without a body.
        await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = { Title = "The request could not be read.", Detail = badRequest.InnerException?.Message ?? badRequest.Message }
        });
        return true;
    }
}
