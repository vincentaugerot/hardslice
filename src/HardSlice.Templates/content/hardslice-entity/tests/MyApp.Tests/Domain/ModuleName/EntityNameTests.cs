using MyApp.Domain;
using MyApp.Domain.ModuleName;

namespace MyApp.Tests.Domain.ModuleName;

public sealed class EntityNameTests
{
    [Fact]
    public void Create_gives_the_new_EntityName_its_state()
    {
#if (LinkIsId)
        var aggregateNameId = AggregateNameId.New();

        var entity = EntityName.Create(aggregateNameId);

        entity.Id.Value.ShouldNotBe(Guid.Empty);
        entity.AggregateNameId.ShouldBe(aggregateNameId);
        Assert.Fail("TODO(hardslice): pass the state of the new EntityName to its factory method, assert it here, "
            + "then test each rule that the factory refuses with Should.Throw<DomainException>.");
#else
        var entity = EntityName.Create();

        entity.Id.Value.ShouldNotBe(Guid.Empty);
        Assert.Fail("TODO(hardslice): pass the state of the new EntityName to its factory method, assert it here, "
            + "then test each rule that the factory refuses with Should.Throw<DomainException>. Test the methods of AggregateName "
            + "that change its collection EntityCollection in the tests of AggregateName, with the rules of the whole aggregate.");
#endif
    }

    [Fact]
    public void Create_gives_each_EntityName_its_own_identifier()
    {
#if (LinkIsId)
        var aggregateNameId = AggregateNameId.New();

        EntityName.Create(aggregateNameId).Id.ShouldNotBe(EntityName.Create(aggregateNameId).Id);
#else
        EntityName.Create().Id.ShouldNotBe(EntityName.Create().Id);
#endif
    }
}
