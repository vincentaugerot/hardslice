namespace MyApp.Domain.Bookings;

public sealed class Booking
{
    private BookingStatus _status;

    private Booking(BookingId id, Guid roomId, BookingPeriod period)
    {
        Id = id;
        RoomId = roomId;
        Period = period;
        _status = BookingStatus.Confirmed;
    }

    // Rehydration only: persistence restores the state of a stored booking. Business code calls Confirm.
    private Booking()
    {
        Period = null!;
    }

    public BookingId Id { get; }

    public Guid RoomId { get; }

    public BookingPeriod Period { get; }

    public BookingStatus Status => _status;

    // A room cannot hold two confirmed bookings whose periods overlap. existingBookings must include every
    // confirmed booking of the room that may overlap the period; the database enforces the same rule under concurrency.
    public static Booking Confirm(Guid roomId, BookingPeriod period, IEnumerable<Booking> existingBookings)
    {
        if (existingBookings.Any(other => other.RoomId == roomId && other.Status == BookingStatus.Confirmed && other.Period.Overlaps(period)))
            throw new BookingConflictException(roomId, period);

        return new(BookingId.New(), roomId, period);
    }

    public void Cancel()
    {
        if (_status == BookingStatus.Cancelled)
            throw new DomainException($"Booking '{Id}' is already cancelled.");

        _status = BookingStatus.Cancelled;
    }
}
