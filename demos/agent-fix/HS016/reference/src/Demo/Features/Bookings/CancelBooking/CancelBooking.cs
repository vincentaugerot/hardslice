using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Demo.Domain.Bookings;
using Demo.Shared.Messaging;
using Demo.Shared.Persistence;

namespace Demo.Features.Bookings.CancelBooking;

public sealed record CancelBookingCommand(Guid BookingId) : ICommand<bool>;

internal sealed class CancelBookingValidator : AbstractValidator<CancelBookingCommand>
{
    public CancelBookingValidator() =>
        RuleFor(command => command.BookingId).NotEmpty();
}

// Changes the aggregate only: TransactionBehavior saves and commits it.
internal sealed class CancelBookingHandler(AppDbContext db, TimeProvider clock) : IRequestHandler<CancelBookingCommand, bool>
{
    public async Task<bool> Handle(CancelBookingCommand command, CancellationToken cancellationToken)
    {
        var booking = await db.Set<Booking>().FindAsync([new BookingId(command.BookingId)], cancellationToken);
        if (booking is null)
            return false;

        booking.Cancel(clock.GetUtcNow());
        return true;
    }
}

internal static class CancelBookingEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/{id:guid}/cancel", async Task<Results<NoContent, NotFound>> (Guid id, IMediator mediator, CancellationToken cancellationToken) =>
            await mediator.Send(new CancelBookingCommand(id), cancellationToken) ? TypedResults.NoContent() : TypedResults.NotFound());
}
