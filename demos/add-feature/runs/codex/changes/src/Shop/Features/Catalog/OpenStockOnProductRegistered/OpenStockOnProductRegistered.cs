using Shop.Domain.Catalog;
using Shop.Domain.Catalog.Events;
using Shop.Shared.Messaging;
using Shop.Shared.Persistence;

namespace Shop.Features.Catalog.OpenStockOnProductRegistered;

// Reacts to ProductRegistered inside the transaction of the command whose aggregate raised it, before anything is saved: its changes are
// saved and committed with the command, and its exception rolls the whole command back.
internal sealed class OpenStockOnProductRegisteredHandler(AppDbContext db) : IDomainEventHandler<ProductRegistered>
{
    public Task Handle(ProductRegistered domainEvent, CancellationToken cancellationToken)
    {
        db.Set<Stock>().Add(Stock.Open(domainEvent.ProductId));
        return Task.CompletedTask;
    }
}
