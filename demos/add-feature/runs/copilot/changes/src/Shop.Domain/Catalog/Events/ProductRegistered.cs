namespace Shop.Domain.Catalog.Events;

public sealed record ProductRegistered(ProductId ProductId) : IDomainEvent;
