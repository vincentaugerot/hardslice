namespace MyApp.Domain.ModuleName.Events;

// A fact that an aggregate of module ModuleName records, named in the past tense (HS019) and immutable (HS009). Replace Id with
// the values of the fact, such as the identifier of the aggregate that raised it: its handlers read nothing else.
public sealed record EventName(Guid Id) : IDomainEvent;
