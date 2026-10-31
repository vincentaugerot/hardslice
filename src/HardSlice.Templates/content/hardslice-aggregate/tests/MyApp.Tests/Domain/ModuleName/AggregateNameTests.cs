using MyApp.Domain.ModuleName;

namespace MyApp.Tests.Domain.ModuleName;

public sealed class AggregateNameTests
{
    [Fact]
    public void Create_gives_the_new_AggregateName_its_state()
    {
        var aggregate = AggregateName.Create();

        aggregate.Id.Value.ShouldNotBe(Guid.Empty);
        Assert.Fail("TODO(hardslice): pass the state of the new AggregateName to its factory method, assert it here, "
            + "then test each rule that the factory refuses with Should.Throw<DomainException>.");
    }

    [Fact]
    public void Create_gives_each_AggregateName_its_own_identifier()
    {
        AggregateName.Create().Id.ShouldNotBe(AggregateName.Create().Id);
    }

    [Fact]
    public void DequeueDomainEvents_returns_the_raised_events_once_and_empties_the_list()
    {
        var aggregate = AggregateName.Create();
        var raised = aggregate.DomainEvents.ToArray();

        aggregate.DequeueDomainEvents().ShouldBe(raised);
        aggregate.DomainEvents.ShouldBeEmpty();
        aggregate.DequeueDomainEvents().ShouldBeEmpty();
    }
}
