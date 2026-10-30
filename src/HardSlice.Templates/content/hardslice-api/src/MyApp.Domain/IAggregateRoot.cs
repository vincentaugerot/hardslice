namespace MyApp.Domain;

// The entity through which its aggregate is loaded, changed and saved as a whole. Other aggregates refer to it by its id.
public interface IAggregateRoot : IEntity;
