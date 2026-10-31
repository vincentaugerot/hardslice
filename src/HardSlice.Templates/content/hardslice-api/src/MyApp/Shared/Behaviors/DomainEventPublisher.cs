using System.Collections.Concurrent;
using MyApp.Domain;
using MyApp.Shared.Messaging;
using MyApp.Shared.Persistence;

namespace MyApp.Shared.Behaviors;

// Publishes the domain events of every aggregate AppDbContext tracks to their IDomainEventHandler<TEvent>, inside the
// command transaction and before saving: TransactionBehavior calls it once the command handler has returned.
// Each aggregate's events go in the order it raised them, aggregates in the order the context started tracking them, and
// each event to every handler in registration order. Events raised by a handler are published in turn, round after round,
// up to MaxDepth rounds: beyond that, the handlers raise events in a loop and the command fails.
internal sealed class DomainEventPublisher
{
    public const int MaxDepth = 8;

    private static readonly ConcurrentDictionary<Type, EventDispatcher> Dispatchers = new();

    private readonly AppDbContext _db;
    private readonly IServiceProvider _services;
    private readonly Dictionary<object, int> _trackingOrder = new(ReferenceEqualityComparer.Instance);

    public DomainEventPublisher(AppDbContext db, IServiceProvider services)
    {
        _db = db;
        _services = services;
        _db.ChangeTracker.Tracked += (_, tracked) => _trackingOrder.TryAdd(tracked.Entry.Entity, _trackingOrder.Count);
    }

    public async Task PublishAsync(CancellationToken cancellationToken)
    {
        for (var depth = 1; ; depth++)
        {
            var domainEvents = DequeueAll();
            if (domainEvents.Count == 0)
                return;

            if (depth > MaxDepth)
            {
                throw new InvalidOperationException(
                    $"Domain event '{domainEvents[0].GetType().Name}' was raised at publication depth {depth}, beyond the limit of {MaxDepth}: "
                    + "the domain event handlers raise events in a loop. "
                    + "Fix: make the handlers of these events stop raising the event that triggers them.");
            }

            foreach (var domainEvent in domainEvents)
            {
                var dispatcher = Dispatchers.GetOrAdd(domainEvent.GetType(), static type =>
                    (EventDispatcher)Activator.CreateInstance(typeof(EventDispatcher<>).MakeGenericType(type))!);
                await dispatcher.Dispatch(domainEvent, _services, cancellationToken);
            }
        }
    }

    // The events of a failed command are never published: emptied with the rest of its changes.
    public void Discard() => DequeueAll();

    private List<IDomainEvent> DequeueAll() =>
        _db.ChangeTracker.Entries<IAggregateRoot>()
            .Select(entry => entry.Entity)
            .OrderBy(aggregate => _trackingOrder.TryGetValue(aggregate, out var order) ? order : int.MaxValue)
            .SelectMany(aggregate => aggregate.DequeueDomainEvents())
            .ToList();

    private abstract class EventDispatcher
    {
        public abstract Task Dispatch(IDomainEvent domainEvent, IServiceProvider services, CancellationToken cancellationToken);
    }

    private sealed class EventDispatcher<TEvent> : EventDispatcher where TEvent : IDomainEvent
    {
        public override async Task Dispatch(IDomainEvent domainEvent, IServiceProvider services, CancellationToken cancellationToken)
        {
            foreach (var handler in services.GetServices<IDomainEventHandler<TEvent>>())
            {
                using (DomainEventHandlerScope.Enter(handler.GetType(), typeof(TEvent)))
                    await handler.Handle((TEvent)domainEvent, cancellationToken);
            }
        }
    }
}
