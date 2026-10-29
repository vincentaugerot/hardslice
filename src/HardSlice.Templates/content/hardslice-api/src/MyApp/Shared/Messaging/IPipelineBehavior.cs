namespace MyApp.Shared.Messaging;

public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

// Wraps the handler of every request whose type satisfies the behavior's generic constraints.
public interface IPipelineBehavior<in TRequest, TResponse> where TRequest : notnull
{
    Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}
