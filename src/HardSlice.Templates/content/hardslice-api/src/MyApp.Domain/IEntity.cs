namespace MyApp.Domain;

// Something with an identity whose state changes only through its own methods: an aggregate root or a child entity.
// HardSlice recognizes entities by this contract only, as hardslice.json declares it, never by their name or an Id property.
public interface IEntity;
