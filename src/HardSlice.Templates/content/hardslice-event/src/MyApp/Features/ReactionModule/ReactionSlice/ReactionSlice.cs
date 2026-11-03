using MyApp.Domain.ModuleName.Events;
using MyApp.Shared.Messaging;

namespace MyApp.Features.ReactionModule.ReactionSlice;

// Reacts to EventName inside the transaction of the command whose aggregate raised it, before that transaction commits: its
// changes are saved and committed with the command, and its exception rolls the whole command back.
internal sealed class ReactionSliceHandler : IDomainEventHandler<EventName>
{
    public Task Handle(EventName domainEvent, CancellationToken cancellationToken) =>
        throw new NotImplementedException(
            "TODO(hardslice): implement ReactionSliceHandler through AppDbContext: inject it, load the aggregate to change with "
            + "Set<TAggregate>(), and call the method of the aggregate that holds the business rule. Never call SaveChangesAsync, "
            + "never send a request through IMediator, and never have an external effect, such as an e-mail or an HTTP call.");
}
