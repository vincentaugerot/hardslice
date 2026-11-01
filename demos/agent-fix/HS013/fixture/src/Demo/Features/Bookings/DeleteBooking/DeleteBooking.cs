using Microsoft.AspNetCore.Http.HttpResults;
using Demo.Domain.Bookings;
using Demo.Shared.Messaging;
using Demo.Shared.Persistence;

namespace Demo.Features.Bookings.DeleteBooking;

public sealed record DeleteBookingCommand(Guid BookingId) : ICommand<bool>;

// Removes a cancelled booking. Changes the context only: TransactionBehavior saves and commits it.
internal sealed class DeleteBookingHandler(AppDbContext db) : IRequestHandler<DeleteBookingCommand, bool>
{
    public async Task<bool> Handle(DeleteBookingCommand command, CancellationToken cancellationToken)
    {
        var booking = await db.Set<Booking>().FindAsync([new BookingId(command.BookingId)], cancellationToken);
        if (booking is null)
            return false;

        booking.EnsureCanBeDeleted();
        db.Set<Booking>().Remove(booking);
        return true;
    }
}

internal static class DeleteBookingEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapDelete("/{id:guid}", async Task<Results<NoContent, NotFound>> (Guid id, IMediator mediator, CancellationToken cancellationToken) =>
            await mediator.Send(new DeleteBookingCommand(id), cancellationToken) ? TypedResults.NoContent() : TypedResults.NotFound());
}
