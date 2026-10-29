using System.Collections.Concurrent;

namespace MyApp.Shared.Messaging;

// Resolves the request's handler from the current scope and runs it through the pipeline behaviors,
// in registration order: the first registered behavior is the outermost.
internal sealed class Mediator(IServiceProvider services) : IMediator
{
    private static readonly ConcurrentDictionary<(Type Request, Type Response), object> Dispatchers = new();

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var dispatcher = (RequestDispatcher<TResponse>)Dispatchers.GetOrAdd((request.GetType(), typeof(TResponse)), static contract =>
            Activator.CreateInstance(typeof(RequestDispatcher<,>).MakeGenericType(contract.Request, contract.Response))!);
        return dispatcher.Dispatch(request, services, cancellationToken);
    }

    private abstract class RequestDispatcher<TResponse>
    {
        public abstract Task<TResponse> Dispatch(object request, IServiceProvider services, CancellationToken cancellationToken);
    }

    private sealed class RequestDispatcher<TRequest, TResponse> : RequestDispatcher<TResponse>
        where TRequest : IRequest<TResponse>
    {
        public override Task<TResponse> Dispatch(object request, IServiceProvider services, CancellationToken cancellationToken)
        {
            var typed = (TRequest)request;
            var handler = services.GetRequiredService<IRequestHandler<TRequest, TResponse>>();

            RequestHandlerDelegate<TResponse> next = () => handler.Handle(typed, cancellationToken);
            foreach (var behavior in services.GetServices<IPipelineBehavior<TRequest, TResponse>>().Reverse())
            {
                var inner = next;
                next = () => behavior.Handle(typed, inner, cancellationToken);
            }

            return next();
        }
    }
}
