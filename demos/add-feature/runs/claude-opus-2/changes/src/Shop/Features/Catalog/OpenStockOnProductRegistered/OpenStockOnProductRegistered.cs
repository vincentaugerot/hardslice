using Shop.Domain.Catalog;
using Shop.Domain.Catalog.Events;
using Shop.Shared.Messaging;
using Shop.Shared.Persistence;

namespace Shop.Features.Catalog.OpenStockOnProductRegistered;

// Opens the stock of a newly registered product, at a quantity of zero. Runs inside the transaction of RegisterProduct, before anything is
// saved: the stock is saved and committed with the product, and its exception rolls the whole registration back.
internal sealed class OpenStockOnProductRegisteredHandler(AppDbContext db) : IDomainEventHandler<ProductRegistered>
{
    public Task Handle(ProductRegistered domainEvent, CancellationToken cancellationToken)
    {
        db.Set<Stock>().Add(Stock.Open(domainEvent.ProductId));
        return Task.CompletedTask;
    }
}
