using MyApp.Domain;

namespace MyApp.Shared.Messaging;

// Reacts to a domain event inside the transaction of the command whose aggregates raised it, before that transaction commits.
// It changes aggregates through AppDbContext: its changes are saved and committed with the command, and its exception
// rolls the whole command back. It never sends a request through IMediator, which would open a transaction inside the
// command's one, and never has an external effect, such as an e-mail or an HTTP call, which would happen even if the
// transaction is rolled back afterwards. Registered by its module, by this contract. Only the handlers of the exact type of the
// event are called: a handler of an interface or a base type of events never is.
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task Handle(TEvent domainEvent, CancellationToken cancellationToken);
}
