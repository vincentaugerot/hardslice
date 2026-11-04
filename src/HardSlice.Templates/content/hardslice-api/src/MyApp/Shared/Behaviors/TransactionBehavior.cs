using Microsoft.EntityFrameworkCore;
using MyApp.Domain;
using MyApp.Shared.Messaging;
using MyApp.Shared.Persistence;
using Npgsql;

namespace MyApp.Shared.Behaviors;

// One transaction per command: the handler changes aggregates, the handlers of the domain events they raised react,
// then this behavior saves and commits everything. Any exception, from a handler or from saving, rolls the whole command
// back. Queries never go through it, and publish no event.
internal sealed class TransactionBehavior<TCommand, TResponse>(AppDbContext db, DomainEventPublisher domainEvents)
    : IPipelineBehavior<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<TResponse> Handle(TCommand command, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var response = await next();
            await domainEvents.PublishAsync(cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return response;
        }
        catch (Exception exception)
        {
            // Rollback does not undo tracked mutations, even after an early save.
            // Later commands in this scope must reload the database state.
            domainEvents.Discard();
            db.ChangeTracker.Clear();
            if (exception is DbUpdateConcurrencyException)
            {
                // An aggregate with a concurrency token, such as xmin, was changed by another request since this one loaded it.
                throw new ConflictException(
                    "The data this request changes was changed by another request in the meantime: read it again, then retry.");
            }

            if (IsUniqueViolation(exception))
            {
                // A unique index refused a row, most often written by a concurrent request since this one read, such as two
                // commands that create the same aggregate. The message never repeats the values of the key, which PostgreSQL puts
                // in its own. A handler that catches the violation to throw an exception of its domain keeps that exception.
                throw new ConflictException(
                    "The data this request writes already exists, possibly written by another request in the meantime: read it again, then retry.");
            }

            throw;
        }
    }

    // Raised by SaveChangesAsync inside a DbUpdateException, or as is by SQL that a handler runs itself.
    private static bool IsUniqueViolation(Exception exception) =>
        exception is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }
            or DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } };
}
