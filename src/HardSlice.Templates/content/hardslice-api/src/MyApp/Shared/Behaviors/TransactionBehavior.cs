using MyApp.Shared.Messaging;
using MyApp.Shared.Persistence;

namespace MyApp.Shared.Behaviors;

// One transaction per command: the handler changes aggregates, this behavior saves and commits them.
// Any exception, from the handler or from saving, rolls the whole command back. Queries never go through it.
internal sealed class TransactionBehavior<TCommand, TResponse>(AppDbContext db) : IPipelineBehavior<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<TResponse> Handle(TCommand command, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var response = await next();
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return response;
        }
        catch
        {
            // Rollback does not undo tracked mutations, even after an early save.
            // Later commands in this scope must reload the database state.
            db.ChangeTracker.Clear();
            throw;
        }
    }
}
