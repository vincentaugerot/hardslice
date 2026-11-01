using Shop.Domain.Catalog;
using Shop.Domain.Catalog.Events;
using Shop.Shared.Messaging;
using Shop.Shared.Persistence;

namespace Shop.Features.Catalog.OpenStockOnProductRegistered;

internal sealed class OpenStockOnProductRegisteredHandler(AppDbContext db) : IDomainEventHandler<ProductRegistered>
{
    public Task Handle(ProductRegistered domainEvent, CancellationToken cancellationToken)
    {
        var stock = Stock.Open(domainEvent.ProductId);
        db.Set<Stock>().Add(stock);
        return Task.CompletedTask;
    }
}
