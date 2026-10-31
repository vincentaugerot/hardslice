using FluentValidation;
#if (ResponseIsNone)
using Microsoft.AspNetCore.Http.HttpResults;
#endif
using MyApp.Shared.Messaging;

namespace MyApp.Features.ModuleName.CommandName;

#if (ResponseIsId)
// Returns the identifier of the aggregate it changes or creates. Replace Id with the fields of the use case.
public sealed record CommandNameCommand(Guid Id) : ICommand<Guid>;
#elif (ResponseIsNone)
// Returns false when the aggregate it changes does not exist, answered with 404. Replace Id with the fields of the use case.
public sealed record CommandNameCommand(Guid Id) : ICommand<bool>;
#else
// Replace Id with the fields of the use case.
public sealed record CommandNameCommand(Guid Id) : ICommand<ResponseTypeName>;

// Values only: never an entity, not even in a property (HS005).
public sealed record ResponseTypeName(Guid Id);
#endif

// Checks the shape of the input only: business rules stay in the aggregate.
internal sealed class CommandNameValidator : AbstractValidator<CommandNameCommand>
{
    public CommandNameValidator() =>
        RuleFor(command => command.Id).NotEmpty();
}

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

internal static class CommandNameEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
#if (ResponseIsNone)
        group.MapPost("/command-route", async Task<Results<NoContent, NotFound>> (CommandNameCommand command, IMediator mediator, CancellationToken cancellationToken) =>
            await mediator.Send(command, cancellationToken) ? TypedResults.NoContent() : TypedResults.NotFound());
#else
        group.MapPost("/command-route", async (CommandNameCommand command, IMediator mediator, CancellationToken cancellationToken) =>
            TypedResults.Ok(await mediator.Send(command, cancellationToken)));
#endif
}
