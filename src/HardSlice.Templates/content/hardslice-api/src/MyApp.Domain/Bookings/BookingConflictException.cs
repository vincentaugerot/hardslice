namespace MyApp.Domain.Bookings;

public sealed class BookingConflictException(Guid roomId, BookingPeriod period)
    : ConflictException($"Room '{roomId}' already has a confirmed booking overlapping {period}.");
