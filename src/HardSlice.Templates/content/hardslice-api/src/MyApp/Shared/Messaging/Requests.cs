namespace MyApp.Shared.Messaging;

// A request is either a command, which changes state inside one transaction, or a query, which only reads.
// Never implement IRequest directly: the pipeline behaviors and the architecture tests tell them apart.
// The response type is exact: widening it would select a different handler and pipeline contract.
public interface IRequest<TResponse>;

public interface ICommand<TResponse> : IRequest<TResponse>;

public interface IQuery<TResponse> : IRequest<TResponse>;

public interface IRequestHandler<in TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
