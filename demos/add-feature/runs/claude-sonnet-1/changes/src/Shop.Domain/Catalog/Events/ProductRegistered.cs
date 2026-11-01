namespace Shop.Domain.Catalog.Events;

// A product was registered in the catalog.
public sealed record ProductRegistered(ProductId ProductId) : IDomainEvent;
