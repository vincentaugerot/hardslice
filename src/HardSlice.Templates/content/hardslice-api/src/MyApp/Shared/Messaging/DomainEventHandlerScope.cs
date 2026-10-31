namespace MyApp.Shared.Messaging;

// The domain event handler running in the current asynchronous flow, if any. The mediator refuses every request sent
// while one runs: a request would open its own transaction inside the one of the command that raised the event.
internal static class DomainEventHandlerScope
{
    private static readonly AsyncLocal<(Type Handler, Type Event)?> Running = new();

    public static (Type Handler, Type Event)? Current => Running.Value;

    // Visible to the handler and to everything it awaits, until disposed.
    public static IDisposable Enter(Type handler, Type domainEvent)
    {
        var previous = Running.Value;
        Running.Value = (handler, domainEvent);
        return new Exit(previous);
    }

    private sealed class Exit((Type Handler, Type Event)? previous) : IDisposable
    {
        public void Dispose() => Running.Value = previous;
    }
}
