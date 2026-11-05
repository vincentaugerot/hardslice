using MyApp.Shared.Messaging;

namespace MyApp.Features.ModuleName.CommandName;

// Decides, before each cycle of AddRecurringCommand, whether CommandNameCommand is sent, such as a key of the service that the
// command reads that is not set, or an import at start that a version already stored makes useless. ModuleNameModule registers it
// after the handler of the command. It receives what it reads by its constructor, such as IOptions<T>, IConfiguration or
// AppDbContext, and never writes: what the cycle does is the work of the command. A query that loads an entity starts with
// AsNoTracking(), and a read that loads none, such as AnyAsync, CountAsync or a projection to values, needs none (HS006).
internal sealed class CommandNameCondition : IRecurringCommandCondition<CommandNameCommand>
{
    // Null sends the command at this cycle, and a reason holds it back: the cycle sends nothing, is not retried, and the reason is
    // logged once until it changes. cycle.AtStart tells the cycle at start of AlsoAtStart, and cycle.Retry its retries.
    public Task<string?> CheckAsync(RecurringCycle cycle, CancellationToken cancellationToken) =>
        throw new NotImplementedException(
            "TODO(hardslice): implement CommandNameCondition: receive what it reads by its constructor, such as IOptions<T> or "
            + "AppDbContext, then return null to send CommandNameCommand at this cycle, or the reason it is not sent, such as "
            + "\"ModuleName:ApiKey is not set\". Never write.");
}
