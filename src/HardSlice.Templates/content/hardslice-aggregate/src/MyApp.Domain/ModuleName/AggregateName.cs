namespace MyApp.Domain.ModuleName;

// The root of the AggregateName aggregate: its state changes only through its methods, which check its rules (HS007).
public sealed class AggregateName : IAggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = [];

    private AggregateName(AggregateNameId id)
    {
        Id = id;
    }

    // Rehydration only: persistence restores the state of the stored aggregate. Business code calls Create.
    private AggregateName()
    {
    }

    public AggregateNameId Id { get; }

    // Events raised and not yet published. Not persisted: a reloaded aggregate has none.
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    // Creates the aggregate once its rules hold. Name it after the business fact, such as Order.Place, take the state of
    // the new aggregate as parameters, and throw a DomainException, or a ConflictException, for each rule they break.
    public static AggregateName Create() => new(AggregateNameId.New());

    public IReadOnlyList<IDomainEvent> DequeueDomainEvents()
    {
        var domainEvents = _domainEvents.ToArray();
        _domainEvents.Clear();
        return domainEvents;
    }
}
