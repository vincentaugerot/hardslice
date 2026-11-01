using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Demo.Domain.Bookings;
using Demo.Shared.Messaging;
using Demo.Shared.Persistence;

namespace Demo.Features.Bookings.CancelBooking;

public sealed record CancelBookingCommand(Guid BookingId) : ICommand<Booking?>;

internal sealed class CancelBookingValidator : AbstractValidator<CancelBookingCommand>
{
    public CancelBookingValidator() =>
        RuleFor(command => command.BookingId).NotEmpty();
}

// Changes the aggregate only: TransactionBehavior saves and commits it. Returns the cancelled booking, so that callers
// read its new state without loading it again; null when it does not exist.
internal sealed class CancelBookingHandler(AppDbContext db) : IRequestHandler<CancelBookingCommand, Booking?>
{
    public async Task<Booking?> Handle(CancelBookingCommand command, CancellationToken cancellationToken)
    {
        var booking = await db.Set<Booking>().FindAsync([new BookingId(command.BookingId)], cancellationToken);
        booking?.Cancel();
        return booking;
    }
}

internal static class CancelBookingEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/{id:guid}/cancel", async Task<Results<NoContent, NotFound>> (Guid id, IMediator mediator, CancellationToken cancellationToken) =>
            await mediator.Send(new CancelBookingCommand(id), cancellationToken) is not null ? TypedResults.NoContent() : TypedResults.NotFound());
}
