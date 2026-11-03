namespace Demo.Domain;

// The entity through which its aggregate is loaded, changed and saved as a whole. Other aggregates refer to it by its id.
// It records the domain events it raises in a private list; the command pipeline takes them to publish them before it commits.
public interface IAggregateRoot : IEntity
{
    // Returns the events raised since the last call, in the order they were raised, and empties the list.
    IReadOnlyList<IDomainEvent> DequeueDomainEvents();
}
