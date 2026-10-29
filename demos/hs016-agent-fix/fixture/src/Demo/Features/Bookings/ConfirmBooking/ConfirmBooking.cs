using Microsoft.EntityFrameworkCore;
using Demo.Domain;
using Demo.Domain.Bookings;
using Demo.Shared.Persistence;
using Npgsql;

namespace Demo.Features.Bookings.ConfirmBooking;

public sealed record ConfirmBookingCommand(Guid RoomId, DateTimeOffset Start, DateTimeOffset End);

internal sealed class ConfirmBookingHandler(AppDbContext db, TimeProvider clock)
{
    // Created by the AddBookingNoOverlapConstraint migration.
    private const string NoOverlapConstraint = "EX_Bookings_RoomId_Period";

    public async Task<BookingId> Handle(ConfirmBookingCommand command, CancellationToken cancellationToken)
    {
        var period = new BookingPeriod(command.Start, command.End);
        if (BookingSchedule.HasStarted(period, clock.GetUtcNow()))
            throw new DomainException("A booking cannot be confirmed for a period that has already started.");

        var existingBookings = await db.Set<Booking>()
            .Where(booking => booking.RoomId == command.RoomId
                && booking.Status == BookingStatus.Confirmed
                && booking.Period.Start < period.End
                && booking.Period.End > period.Start)
            .ToListAsync(cancellationToken);

        var booking = Booking.Confirm(command.RoomId, period, existingBookings);
        db.Set<Booking>().Add(booking);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (IsOverlapRejectedByDatabase(exception))
        {
            throw new BookingConflictException(command.RoomId, period);
        }

        return booking.Id;
    }

    // An overlapping confirmation reached the database after the bookings above were loaded. PostgreSQL rejects this
    // insert with an exclusion violation once the other one is committed, or with a deadlock while both are in progress:
    // this single INSERT waits on nothing but the exclusion check, and at least one of the deadlocked inserts goes through.
    // EF Core wraps the deadlock, a transient error, in an InvalidOperationException, hence the innermost exception.
    private static bool IsOverlapRejectedByDatabase(Exception exception) =>
        exception.GetBaseException() is PostgresException { SqlState: PostgresErrorCodes.DeadlockDetected }
            or PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation, ConstraintName: NoOverlapConstraint };
}

// A booking is frozen once its period has started: it can no longer be confirmed or cancelled.
internal static class BookingSchedule
{
    public static bool HasStarted(BookingPeriod period, DateTimeOffset now) => period.Start <= now;
}

internal static class ConfirmBookingEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/", async (ConfirmBookingCommand command, ConfirmBookingHandler handler, CancellationToken cancellationToken) =>
            TypedResults.Created($"/bookings/{await handler.Handle(command, cancellationToken)}"));
}
