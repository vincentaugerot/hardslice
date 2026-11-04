namespace MyApp.Shared.Messaging;

// A request is either a command, which changes state inside one transaction, or a query, which only reads.
// Never implement IRequest directly: the pipeline behaviors and the architecture tests tell them apart.
// The response type is exact: widening it would select a different handler and pipeline contract.
public interface IRequest<TResponse>;

public interface ICommand<TResponse> : IRequest<TResponse>;

// A command that only the application sends, such as a job or a hosted service: it has no endpoint, and a type of its module
// sends it (HS004). It goes through the pipeline of every command, validation and transaction included.
public interface ISystemCommand<TResponse> : ICommand<TResponse>;

public interface IQuery<TResponse> : IRequest<TResponse>;

public interface IRequestHandler<in TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
