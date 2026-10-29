using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MyApp.Domain.Bookings;
using MyApp.Shared.Messaging;
using MyApp.Shared.Persistence;
using Npgsql;

namespace MyApp.Features.Bookings.ConfirmBooking;

public sealed record ConfirmBookingCommand(Guid RoomId, DateTimeOffset Start, DateTimeOffset End) : ICommand<BookingId>;

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

internal sealed class ConfirmBookingHandler(AppDbContext db) : IRequestHandler<ConfirmBookingCommand, BookingId>
{
    // Created by the AddBookingNoOverlapConstraint migration.
    private const string NoOverlapConstraint = "EX_Bookings_RoomId_Period";

    public async Task<BookingId> Handle(ConfirmBookingCommand command, CancellationToken cancellationToken)
    {
        var period = new BookingPeriod(command.Start, command.End);
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
            // Saved early, inside the command transaction, only to translate the database's rejection.
            // TransactionBehavior commits; handlers that translate no database error never save.
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

internal static class ConfirmBookingEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/", async (ConfirmBookingCommand command, IMediator mediator, CancellationToken cancellationToken) =>
            TypedResults.Created($"/bookings/{await mediator.Send(command, cancellationToken)}"));
}
