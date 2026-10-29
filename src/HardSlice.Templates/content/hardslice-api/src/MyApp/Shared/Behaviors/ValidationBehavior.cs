using FluentValidation;
using FluentValidation.Results;
using MyApp.Shared.Messaging;

namespace MyApp.Shared.Behaviors;

// Runs every validator of the request before its handler. Any failure throws a ValidationException,
// answered with 400 by ValidationExceptionHandler: the handler is never reached.
internal sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var failures = new List<ValidationFailure>();
        foreach (var validator in validators)
            failures.AddRange((await validator.ValidateAsync(request, cancellationToken)).Errors);

        if (failures.Count > 0)
            throw new ValidationException(failures);

        return await next();
    }
}
