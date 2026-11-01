namespace Shop.Domain.Catalog.Events;

// A product was registered in the catalog. Named in the past tense (HS019) and immutable (HS009): its handlers read nothing else.
public sealed record ProductRegistered(ProductId ProductId) : IDomainEvent;
