using Microsoft.Extensions.DependencyInjection;
using MyApp.Domain.ModuleName.Events;
using MyApp.Features.ReactionModule.ReactionSlice;
using MyApp.Shared.Messaging;
using MyApp.Shared.Persistence;

namespace MyApp.IntegrationTests.Features.ReactionModule.ReactionSlice;

// The pipeline calls ReactionSliceHandler with the EventName that an aggregate raised during a command, then saves the command.
// This test does the same with an event it builds. Once an aggregate raises EventName, also send the command that raises it
// through its endpoint: the reaction then runs inside its transaction.
public sealed class ReactionSliceTests(PostgresDatabase database)
{
    [Fact]
    public async Task Reacts_to_EventName()
    {
        await using var app = new AppFactory(database);
        await using var scope = app.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetServices<IDomainEventHandler<EventName>>().OfType<ReactionSliceHandler>().SingleOrDefault();
        handler.ShouldNotBeNull("ReactionModuleModule does not register ReactionSliceHandler as IDomainEventHandler<EventName>.");
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var domainEvent = new EventName(Guid.NewGuid());

        await handler.Handle(domainEvent, TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Fail("TODO(hardslice): assert what ReactionSliceHandler changed for domainEvent, such as the state stored in PostgreSQL, read with database.CreateDbContext().");
    }
}
