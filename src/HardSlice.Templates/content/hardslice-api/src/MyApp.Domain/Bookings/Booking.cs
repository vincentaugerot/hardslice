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

    public static Booking Confirm(Guid roomId, BookingPeriod period) => new(BookingId.New(), roomId, period);

    public void Cancel()
    {
        if (_status == BookingStatus.Cancelled)
            throw new DomainException($"Booking '{Id}' is already cancelled.");

        _status = BookingStatus.Cancelled;
    }
}
