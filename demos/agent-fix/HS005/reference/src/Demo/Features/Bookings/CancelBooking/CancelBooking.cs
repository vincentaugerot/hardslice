using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Demo.Domain.Bookings;
using Demo.Shared.Messaging;
using Demo.Shared.Persistence;

namespace Demo.Features.Bookings.CancelBooking;

public sealed record CancelBookingCommand(Guid BookingId) : ICommand<CancelledBooking?>;

public sealed record CancelledBooking(Guid Id, string Status);

internal sealed class CancelBookingValidator : AbstractValidator<CancelBookingCommand>
{
    public CancelBookingValidator() =>
        RuleFor(command => command.BookingId).NotEmpty();
}

// Changes the aggregate only: TransactionBehavior saves and commits it. Returns the new state of the booking as values,
// so that callers read it without loading the booking again; null when it does not exist.
internal sealed class CancelBookingHandler(AppDbContext db) : IRequestHandler<CancelBookingCommand, CancelledBooking?>
{
    public async Task<CancelledBooking?> Handle(CancelBookingCommand command, CancellationToken cancellationToken)
    {
        var booking = await db.Set<Booking>().FindAsync([new BookingId(command.BookingId)], cancellationToken);
        if (booking is null)
            return null;

        booking.Cancel();
        return new CancelledBooking(booking.Id.Value, booking.Status.ToString());
    }
}

internal static class CancelBookingEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/{id:guid}/cancel", async Task<Results<NoContent, NotFound>> (Guid id, IMediator mediator, CancellationToken cancellationToken) =>
            await mediator.Send(new CancelBookingCommand(id), cancellationToken) is not null ? TypedResults.NoContent() : TypedResults.NotFound());
}
