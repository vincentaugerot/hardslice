#if (!NoSample)
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
#endif
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Domain;
#if (!NoSample)
using MyApp.Domain.Bookings;
using MyApp.Domain.Bookings.Events;
using MyApp.Features.Bookings.ConfirmBooking;
using MyApp.Features.Bookings.GetBooking;
using MyApp.Shared.Behaviors;
#endif
using MyApp.Shared.Messaging;
using MyApp.Shared.Persistence;

namespace MyApp.IntegrationTests.Shared.Behaviors;

// Domain events published to handlers registered for these tests only: those of Probe, an aggregate that only these tests map,
#if (!NoSample)
// and BookingConfirmed, raised when ConfirmBooking confirms a booking.
#endif
public sealed class DomainEventPublisherTests(PostgresDatabase database)
{
    // Without any module: the command handler tracks an aggregate that raised two events, which are published in order to their
    // handler, inside the transaction of the command.
    [Fact]
    public async Task Publishes_the_events_of_a_tracked_aggregate_in_order_inside_the_command_transaction()
    {
        var id = Guid.NewGuid();
        var observations = new List<ProbeObservation>();
        await using var app = App(services => services
            .ConfigureDbContext<AppDbContext>(options => options.ReplaceService<IModelCustomizer, ProbeModelCustomizer>())
            .AddSingleton(observations)
            .AddScoped<IRequestHandler<RaiseProbeEvents, bool>, RaiseProbeEventsHandler>()
            .AddScoped<IDomainEventHandler<ProbeRaised>, ObservingProbeHandler>());
        await using var scope = app.Services.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new RaiseProbeEvents(id, 2), TestContext.Current.CancellationToken);

        observations.ShouldBe([new ProbeObservation(id, 1, InTransaction: true), new ProbeObservation(id, 2, InTransaction: true)]);
    }

    public sealed record RaiseProbeEvents(Guid Id, int Count) : ICommand<bool>;

    public sealed record ProbeRaised(Guid ProbeId, int Order) : IDomainEvent;

    public sealed record ProbeObservation(Guid ProbeId, int Order, bool InTransaction);

    // An aggregate of these tests: attached unchanged, it is never saved, so it needs no table.
    public sealed class Probe : IAggregateRoot
    {
        private readonly List<IDomainEvent> _domainEvents = [];

        private Probe(Guid id) => Id = id;

        public Guid Id { get; }

        public static Probe Raise(Guid id, int count)
        {
            var probe = new Probe(id);
            for (var order = 1; order <= count; order++)
                probe._domainEvents.Add(new ProbeRaised(id, order));
            return probe;
        }

        public IReadOnlyList<IDomainEvent> DequeueDomainEvents()
        {
            var domainEvents = _domainEvents.ToArray();
            _domainEvents.Clear();
            return domainEvents;
        }
    }

    // Adds Probe to the model of AppDbContext for these tests, beside the mappings of the modules.
    private sealed class ProbeModelCustomizer(ModelCustomizerDependencies dependencies) : RelationalModelCustomizer(dependencies)
    {
        public override void Customize(ModelBuilder modelBuilder, DbContext context)
        {
            base.Customize(modelBuilder, context);
            modelBuilder.Entity<Probe>().HasKey(probe => probe.Id);
        }
    }

    private sealed class RaiseProbeEventsHandler(AppDbContext db) : IRequestHandler<RaiseProbeEvents, bool>
    {
        public Task<bool> Handle(RaiseProbeEvents command, CancellationToken cancellationToken)
        {
            db.Attach(Probe.Raise(command.Id, command.Count));
            return Task.FromResult(true);
        }
    }

    private sealed class ObservingProbeHandler(AppDbContext db, List<ProbeObservation> observations) : IDomainEventHandler<ProbeRaised>
    {
        public Task Handle(ProbeRaised domainEvent, CancellationToken cancellationToken)
        {
            observations.Add(new ProbeObservation(domainEvent.ProbeId, domainEvent.Order, db.Database.CurrentTransaction is not null));
            return Task.CompletedTask;
        }
    }
#if (!NoSample)

    private static readonly DateTimeOffset Start = TestInstants.Start;

    [Fact]
    public async Task Publishes_the_event_once_inside_the_transaction_before_anything_is_committed()
    {
        var roomId = Guid.NewGuid();
        var observations = new List<Observation>();
        await using var app = App(services => services
            .AddSingleton(observations)
            .AddSingleton(database)
            .AddScoped<IDomainEventHandler<BookingConfirmed>, ObservingHandler>());

        var response = await app.CreateClient().PostAsJsonAsync("/bookings", Confirmation(roomId), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        observations.ShouldHaveSingleItem().ShouldBe(new Observation(roomId, InTransaction: true, SeenInTransaction: true, SeenByAnotherConnection: false));
        (await StoredCountAsync(roomId)).ShouldBe(1);
    }

    [Theory]
    [InlineData(false, HttpStatusCode.BadRequest)]
    [InlineData(true, HttpStatusCode.Conflict)]
    public async Task A_failing_handler_saves_nothing_drops_the_pending_events_and_answers_with_its_exception(bool conflict, HttpStatusCode status)
    {
        var roomId = Guid.NewGuid();
        var confirmedByHandler = new List<Booking>();
        await using var app = App(services => services
            .AddSingleton(confirmedByHandler)
            .AddSingleton(new Failure(conflict))
            .AddScoped<IDomainEventHandler<BookingConfirmed>, FailingHandler>());

        var response = await app.CreateClient().PostAsJsonAsync("/bookings", Confirmation(roomId), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(status);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!.Detail.ShouldBe(FailingHandler.Message);
        var other = confirmedByHandler.ShouldHaveSingleItem();
        other.DomainEvents.ShouldBeEmpty();
        (await StoredCountAsync(roomId)).ShouldBe(0);
        (await StoredCountAsync(other.RoomId)).ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Calls_every_handler_of_the_event_in_registration_order(bool secondFirst)
    {
        var calls = new List<string>();
        await using var app = App(services =>
        {
            services.AddSingleton(calls);
            if (secondFirst)
                services.AddScoped<IDomainEventHandler<BookingConfirmed>, SecondHandler>().AddScoped<IDomainEventHandler<BookingConfirmed>, FirstHandler>();
            else
                services.AddScoped<IDomainEventHandler<BookingConfirmed>, FirstHandler>().AddScoped<IDomainEventHandler<BookingConfirmed>, SecondHandler>();
        });

        var response = await app.CreateClient().PostAsJsonAsync("/bookings", Confirmation(Guid.NewGuid()), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        calls.ShouldBe(secondFirst ? ["second", "first"] : ["first", "second"]);
    }

    // Each call confirms a booking of another room, which raises BookingConfirmed again.
    [Fact]
    public async Task Fails_with_the_name_of_the_event_when_handlers_raise_events_beyond_the_maximum_depth()
    {
        var roomId = Guid.NewGuid();
        var confirmedByHandler = new List<Booking>();
        await using var app = App(services => services
            .AddSingleton(confirmedByHandler)
            .AddScoped<IDomainEventHandler<BookingConfirmed>, LoopingHandler>());

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => SendAsync(app, Confirmation(roomId)));

        exception.Message.ShouldStartWith(
            $"Domain event 'BookingConfirmed' was raised at publication depth {DomainEventPublisher.MaxDepth + 1}, beyond the limit of {DomainEventPublisher.MaxDepth}: ");
        confirmedByHandler.Count.ShouldBe(DomainEventPublisher.MaxDepth);
        (await StoredCountAsync(roomId)).ShouldBe(0);
        (await StoredCountAsync(confirmedByHandler[0].RoomId)).ShouldBe(0);
    }

    [Fact]
    public async Task Fails_with_the_name_of_the_handler_when_it_sends_a_request_through_the_mediator()
    {
        var roomId = Guid.NewGuid();
        await using var app = App(services => services.AddScoped<IDomainEventHandler<BookingConfirmed>, SendingHandler>());

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => SendAsync(app, Confirmation(roomId)));

        exception.Message.ShouldStartWith("Domain event handler 'SendingHandler' sent 'GetBookingQuery' through IMediator while handling 'BookingConfirmed'. ");
        (await StoredCountAsync(roomId)).ShouldBe(0);
    }
#endif

    private WebApplicationFactory<Program> App(Action<IServiceCollection> handlers) =>
        new AppFactory(database).WithWebHostBuilder(builder => builder.ConfigureTestServices(handlers));
#if (!NoSample)

    private static async Task SendAsync(WebApplicationFactory<Program> app, ConfirmBookingCommand command)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command, TestContext.Current.CancellationToken);
    }

    private static ConfirmBookingCommand Confirmation(Guid roomId) => new(roomId, Start, Start.AddHours(1));

    private async Task<int> StoredCountAsync(Guid roomId)
    {
        await using var db = database.CreateDbContext();
        return await db.Set<Booking>().CountAsync(booking => booking.RoomId == roomId, TestContext.Current.CancellationToken);
    }

    public sealed record Observation(Guid RoomId, bool InTransaction, bool SeenInTransaction, bool SeenByAnotherConnection);

    public sealed record Failure(bool Conflict);

    // Reads the booking through the command's context, then through a connection of its own, outside the transaction.
    private sealed class ObservingHandler(AppDbContext db, PostgresDatabase database, List<Observation> observations)
        : IDomainEventHandler<BookingConfirmed>
    {
        public async Task Handle(BookingConfirmed domainEvent, CancellationToken cancellationToken)
        {
            var seenInTransaction = await db.Set<Booking>().AsNoTracking().AnyAsync(booking => booking.Id == domainEvent.BookingId, cancellationToken);
            await using var other = database.CreateDbContext();
            var seenByAnotherConnection = await other.Set<Booking>().AnyAsync(booking => booking.Id == domainEvent.BookingId, cancellationToken);
            observations.Add(new Observation(domainEvent.RoomId, db.Database.CurrentTransaction is not null, seenInTransaction, seenByAnotherConnection));
        }
    }

    // Confirms a booking of another room, whose BookingConfirmed is still pending, then throws.
    private sealed class FailingHandler(AppDbContext db, List<Booking> confirmed, Failure failure) : IDomainEventHandler<BookingConfirmed>
    {
        public const string Message = "The handler rejected the confirmation.";

        public Task Handle(BookingConfirmed domainEvent, CancellationToken cancellationToken)
        {
            var other = Booking.Confirm(Guid.NewGuid(), domainEvent.Period, []);
            db.Set<Booking>().Add(other);
            confirmed.Add(other);
            throw failure.Conflict ? new ConflictException(Message) : new DomainException(Message);
        }
    }

    private sealed class FirstHandler(List<string> calls) : IDomainEventHandler<BookingConfirmed>
    {
        public Task Handle(BookingConfirmed domainEvent, CancellationToken cancellationToken)
        {
            calls.Add("first");
            return Task.CompletedTask;
        }
    }

    private sealed class SecondHandler(List<string> calls) : IDomainEventHandler<BookingConfirmed>
    {
        public Task Handle(BookingConfirmed domainEvent, CancellationToken cancellationToken)
        {
            calls.Add("second");
            return Task.CompletedTask;
        }
    }

    private sealed class LoopingHandler(AppDbContext db, List<Booking> confirmed) : IDomainEventHandler<BookingConfirmed>
    {
        public Task Handle(BookingConfirmed domainEvent, CancellationToken cancellationToken)
        {
            var next = Booking.Confirm(Guid.NewGuid(), domainEvent.Period, []);
            db.Set<Booking>().Add(next);
            confirmed.Add(next);
            return Task.CompletedTask;
        }
    }

    private sealed class SendingHandler(IMediator mediator) : IDomainEventHandler<BookingConfirmed>
    {
        public Task Handle(BookingConfirmed domainEvent, CancellationToken cancellationToken) =>
            mediator.Send(new GetBookingQuery(domainEvent.BookingId.Value), cancellationToken);
    }
#endif
}
