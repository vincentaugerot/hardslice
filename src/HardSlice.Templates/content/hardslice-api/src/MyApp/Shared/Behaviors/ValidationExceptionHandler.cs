using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace MyApp.Shared.Behaviors;

internal sealed class ValidationExceptionHandler(IProblemDetailsService problemDetails, IOptions<JsonOptions> json) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ValidationException validation)
            return ValueTask.FromResult(false);

        // Fields named as the client writes them in JSON.
        var naming = json.Value.SerializerOptions.PropertyNamingPolicy;
        var errors = validation.Errors
            .GroupBy(failure => naming?.ConvertName(failure.PropertyName) ?? failure.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(failure => failure.ErrorMessage).ToArray());

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        return problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new HttpValidationProblemDetails(errors)
        });
    }
}
