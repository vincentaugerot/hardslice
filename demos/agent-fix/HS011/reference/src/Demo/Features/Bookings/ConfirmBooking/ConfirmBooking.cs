using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Demo.Domain.Bookings;
using Demo.Shared.Messaging;
using Demo.Shared.Persistence;
using Npgsql;

namespace Demo.Features.Bookings.ConfirmBooking;

// The request of a slice speaks in values: identifiers are Guid, converted to the typed identifiers of the domain by the handler.
public sealed record ConfirmBookingCommand(Guid RoomId, DateTimeOffset Start, DateTimeOffset End) : ICommand<Guid>;

// Checks the shape of the input only: business rules, such as a period that ends after its start, stay in the domain.
internal sealed class ConfirmBookingValidator : AbstractValidator<ConfirmBookingCommand>
{
    public ConfirmBookingValidator()
    {
        RuleFor(command => command.RoomId).NotEmpty();
        RuleFor(command => command.Start).NotEmpty();
        RuleFor(command => command.End).NotEmpty();
    }
}

internal sealed class ConfirmBookingHandler(AppDbContext db) : IRequestHandler<ConfirmBookingCommand, Guid>
{
    // Created by the AddBookingNoOverlapConstraint migration.
    private const string NoOverlapConstraint = "EX_Bookings_RoomId_Period";

    public async Task<Guid> Handle(ConfirmBookingCommand command, CancellationToken cancellationToken)
    {
        var period = new BookingPeriod(command.Start, command.End);
        var booking = Booking.Confirm(command.RoomId, period, await LoadOverlappingBookingsAsync(command.RoomId, period, cancellationToken));
        db.Set<Booking>().Add(booking);
        try
        {
            // Saved early, inside the command transaction, only to translate the database's rejection.
            // TransactionBehavior commits; handlers that translate no database error never save.
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (IsOverlapRejectedByDatabase(exception))
        {
            throw new BookingConflictException(command.RoomId, period);
        }

        return booking.Id.Value;
    }

    // The confirmed bookings of the room whose period overlaps the requested one.
    private Task<List<Booking>> LoadOverlappingBookingsAsync(Guid roomId, BookingPeriod period, CancellationToken cancellationToken) =>
        db.Set<Booking>()
            .Where(booking => booking.RoomId == roomId
                && booking.Status == BookingStatus.Confirmed
                && booking.Period.Start < period.End
                && booking.Period.End > period.Start)
            .ToListAsync(cancellationToken);

    // An overlapping confirmation reached the database after the bookings above were loaded. PostgreSQL rejects this
    // insert with an exclusion violation once the other one is committed, or with a deadlock while both are in progress:
    // this single INSERT waits on nothing but the exclusion check, and at least one of the deadlocked inserts goes through.
    // EF Core wraps the deadlock, a transient error, in an InvalidOperationException, hence the innermost exception.
    private static bool IsOverlapRejectedByDatabase(Exception exception) =>
        exception.GetBaseException() is PostgresException { SqlState: PostgresErrorCodes.DeadlockDetected }
            or PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation, ConstraintName: NoOverlapConstraint };
}

internal static class ConfirmBookingEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/", async (ConfirmBookingCommand command, IMediator mediator, CancellationToken cancellationToken) =>
            TypedResults.Created($"/bookings/{await mediator.Send(command, cancellationToken)}"));
}
