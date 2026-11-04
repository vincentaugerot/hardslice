namespace MyApp.Domain.ModuleName;

#if (LinkIsId)
// A child entity of the aggregate AggregateName that holds the identifier of its root instead of being held by it: a collection
// too large to load with its root, such as millions of rows, written and read apart from it. AggregateName holds nothing of it,
// so HS008 knows no owner for it and asserts nothing. Its state changes only through its methods (HS007).
#else
// A child entity of the aggregate AggregateName, held in its collection EntityCollection: loaded and saved with its root, and
// added, changed and removed only through the methods of AggregateName, which check the rules of the whole aggregate. Its state
// changes only through its methods (HS007).
#endif
public sealed class EntityName : IEntity
{
#if (LinkIsId)
    private EntityName(EntityNameId id, AggregateNameId aggregateNameId)
    {
        Id = id;
        AggregateNameId = aggregateNameId;
    }
#else
    private EntityName(EntityNameId id)
    {
        Id = id;
    }
#endif

    // Rehydration only: persistence restores the state of the stored entity. Business code calls Create.
    private EntityName()
    {
    }

    public EntityNameId Id { get; }
#if (LinkIsId)

    // The identifier of its root, never the root itself (HS008).
    public AggregateNameId AggregateNameId { get; }

    // Creates the child of a root once its rules hold. Name it after the business fact, take its state as parameters, and throw
    // a DomainException for each rule they break.
    public static EntityName Create(AggregateNameId aggregateNameId) => new(EntityNameId.New(), aggregateNameId);
#else

    // Creates the child once its rules hold, for the method of AggregateName that adds it to its collection. Name it after the
    // business fact, take its state as parameters, and throw a DomainException for each rule they break.
    public static EntityName Create() => new(EntityNameId.New());
#endif
}
