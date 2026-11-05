#if (!ParametersIsNone)
using FluentValidation;
#endif
#if (ResponseIsNone && !EndpointIsNone)
using Microsoft.AspNetCore.Http.HttpResults;
#endif
using MyApp.Shared.Messaging;

namespace MyApp.Features.ModuleName.CommandName;

#if (DailyAtIsGiven)
// A scheduled system command: AddRecurringCommand, in ModuleNameModule, sends a new one every day at RecurringDailyAt in
// RecurringTimeZone, or at ModuleName:CommandName:DailyAt in ModuleName:CommandName:TimeZone of the configuration, each time in a
// scope of its own. It has no endpoint (HS004), and goes through the pipeline of every command: a failed cycle rolls its
// transaction back and is logged.
#if (RetryIsGiven)
// After a failed cycle, it is sent again at most RecurringCountOfRetries times, RecurringDelayOfRetries apart, then the next day
// after the last one comes.
#endif
#elif (EveryIsGiven)
// A recurring system command: AddRecurringCommand, in ModuleNameModule, sends a new one every RecurringInterval, or every
// ModuleName:CommandName:Interval of the configuration, each time in a scope of its own. It has no endpoint (HS004), and goes
// through the pipeline of every command: a failed cycle rolls its transaction back, is logged, and the next one runs.
#if (RetryIsGiven)
// After a failed cycle, it is sent again at most RecurringCountOfRetries times, RecurringDelayOfRetries apart, then the next interval
// after the last one comes.
#endif
#elif (EndpointIsNone)
// A system command: only the application sends it, never an HTTP request. A type of module ModuleName creates it and sends it
// through IMediator in a scope of its own, such as a hosted service of this slice. It has no endpoint (HS004), and goes through
// the pipeline of every command: validation, then the transaction.
#endif
#if (AtStart)
// It is also sent once when the application starts, before the first instant of its schedule, unless
// ModuleName:CommandName:AtStart of the configuration is false.
#endif
#if (ParametersIsNone)
// No parameter: what the command needs comes from the database, the configuration or a service that its module registers,
// never from the request. It has no validator (HS013), and its tests cover its success or a failure of the domain.
#if (ResponseIsId)
// Returns the identifier of the aggregate it changes or creates.
public sealed record CommandNameCommand : ICommand<Guid>;
#else
public sealed record CommandNameCommand : ICommand<ResponseTypeName>;

// Values only: never an entity, not even in a property (HS005). Replace Id with what the command did.
public sealed record ResponseTypeName(Guid Id);
#endif
#elif (ReadsIdFromRoute)
// A DELETE has no body: its endpoint reads Id, the identifier of the aggregate, from its route.
#if (ResponseIsId)
public sealed record CommandNameCommand(Guid Id) : ICommand<Guid>;
#elif (ResponseIsNone)
// Returns false when the aggregate does not exist, answered with 404.
public sealed record CommandNameCommand(Guid Id) : ICommand<bool>;
#else
public sealed record CommandNameCommand(Guid Id) : ICommand<ResponseTypeName>;

// Values only: never an entity, not even in a property (HS005).
public sealed record ResponseTypeName(Guid Id);
#endif
#elif (ResponseIsId)
// Returns the identifier of the aggregate it changes or creates. Replace Id with the fields of the use case.
public sealed record CommandNameCommand(Guid Id) : ICommand<Guid>;
#elif (ResponseIsNone && EndpointIsNone)
// Returns false when the aggregate it changes does not exist. Replace Id with the fields of the use case.
public sealed record CommandNameCommand(Guid Id) : ICommand<bool>;
#elif (ResponseIsNone)
// Returns false when the aggregate it changes does not exist, answered with 404. Replace Id with the fields of the use case.
public sealed record CommandNameCommand(Guid Id) : ICommand<bool>;
#else
// Replace Id with the fields of the use case.
public sealed record CommandNameCommand(Guid Id) : ICommand<ResponseTypeName>;

// Values only: never an entity, not even in a property (HS005).
public sealed record ResponseTypeName(Guid Id);
#endif
#if (!ParametersIsNone)

// Checks the shape of the input only: business rules stay in the aggregate.
internal sealed class CommandNameValidator : AbstractValidator<CommandNameCommand>
{
    public CommandNameValidator() =>
        RuleFor(command => command.Id).NotEmpty();
}
#endif

// Changes the aggregate only: TransactionBehavior saves and commits it, and publishes its domain events before.
#if (ResponseIsId)
internal sealed class CommandNameHandler : IRequestHandler<CommandNameCommand, Guid>
{
    public Task<Guid> Handle(CommandNameCommand command, CancellationToken cancellationToken) =>
#elif (ResponseIsNone)
internal sealed class CommandNameHandler : IRequestHandler<CommandNameCommand, bool>
{
    public Task<bool> Handle(CommandNameCommand command, CancellationToken cancellationToken) =>
#else
internal sealed class CommandNameHandler : IRequestHandler<CommandNameCommand, ResponseTypeName>
{
    public Task<ResponseTypeName> Handle(CommandNameCommand command, CancellationToken cancellationToken) =>
#endif
        throw new NotImplementedException(
            "TODO(hardslice): implement CommandNameHandler through the aggregate of module ModuleName: inject AppDbContext, "
            + "load the aggregate with Set<TAggregate>(), call the method of the aggregate that holds the business rule, "
            + "and return the response. Never call SaveChangesAsync.");
}
#if (!EndpointIsNone)

internal static class CommandNameEndpoint
{
#if (RequireIsGiven)
    // The policy AuthorizationPolicyName answers 401 or 403 before the delegate runs: those responses are not in its signature (HS015),
    // and an integration test asserts the 401.
#endif
    public static void Map(RouteGroupBuilder group) =>
#if (RequireIsGiven && ParametersIsNone)
        group.MapPost("/command-route", async (IMediator mediator, CancellationToken cancellationToken) =>
                TypedResults.Ok(await mediator.Send(new CommandNameCommand(), cancellationToken)))
            .RequireAuthorization("AuthorizationPolicyName");
#elif (RequireIsGiven && ReadsIdFromRoute && ResponseIsNone)
        group.MapPost("/command-id-route", async Task<Results<NoContent, NotFound>> (Guid id, IMediator mediator, CancellationToken cancellationToken) =>
                await mediator.Send(new CommandNameCommand(id), cancellationToken) ? TypedResults.NoContent() : TypedResults.NotFound())
            .RequireAuthorization("AuthorizationPolicyName");
#elif (RequireIsGiven && ReadsIdFromRoute)
        group.MapPost("/command-id-route", async (Guid id, IMediator mediator, CancellationToken cancellationToken) =>
                TypedResults.Ok(await mediator.Send(new CommandNameCommand(id), cancellationToken)))
            .RequireAuthorization("AuthorizationPolicyName");
#elif (RequireIsGiven && ResponseIsNone)
        group.MapPost("/command-route", async Task<Results<NoContent, NotFound>> (CommandNameCommand command, IMediator mediator, CancellationToken cancellationToken) =>
                await mediator.Send(command, cancellationToken) ? TypedResults.NoContent() : TypedResults.NotFound())
            .RequireAuthorization("AuthorizationPolicyName");
#elif (RequireIsGiven)
        group.MapPost("/command-route", async (CommandNameCommand command, IMediator mediator, CancellationToken cancellationToken) =>
                TypedResults.Ok(await mediator.Send(command, cancellationToken)))
            .RequireAuthorization("AuthorizationPolicyName");
#elif (ParametersIsNone)
        group.MapPost("/command-route", async (IMediator mediator, CancellationToken cancellationToken) =>
            TypedResults.Ok(await mediator.Send(new CommandNameCommand(), cancellationToken)));
#elif (ReadsIdFromRoute && ResponseIsNone)
        group.MapPost("/command-id-route", async Task<Results<NoContent, NotFound>> (Guid id, IMediator mediator, CancellationToken cancellationToken) =>
            await mediator.Send(new CommandNameCommand(id), cancellationToken) ? TypedResults.NoContent() : TypedResults.NotFound());
#elif (ReadsIdFromRoute)
        group.MapPost("/command-id-route", async (Guid id, IMediator mediator, CancellationToken cancellationToken) =>
            TypedResults.Ok(await mediator.Send(new CommandNameCommand(id), cancellationToken)));
#elif (ResponseIsNone)
        group.MapPost("/command-route", async Task<Results<NoContent, NotFound>> (CommandNameCommand command, IMediator mediator, CancellationToken cancellationToken) =>
            await mediator.Send(command, cancellationToken) ? TypedResults.NoContent() : TypedResults.NotFound());
#else
        group.MapPost("/command-route", async (CommandNameCommand command, IMediator mediator, CancellationToken cancellationToken) =>
            TypedResults.Ok(await mediator.Send(command, cancellationToken)));
#endif
}
#endif
