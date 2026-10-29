namespace MyApp.Domain.Bookings;

public sealed class Booking
{
    private readonly object _stateLock = new();
    private BookingStatus _status;

    private Booking(BookingId id, Guid roomId, BookingPeriod period)
    {
        Id = id;
        RoomId = roomId;
        Period = period;
        _status = BookingStatus.Confirmed;
    }

    public BookingId Id { get; }

    public Guid RoomId { get; }

    public BookingPeriod Period { get; }

    public BookingStatus Status
    {
        get
        {
            lock (_stateLock)
                return _status;
        }
    }

    public static Booking Confirm(Guid roomId, BookingPeriod period) => new(BookingId.New(), roomId, period);

    public void Cancel()
    {
        lock (_stateLock)
        {
            if (_status == BookingStatus.Cancelled)
                throw new DomainException($"Booking '{Id}' is already cancelled.");

            _status = BookingStatus.Cancelled;
        }
    }
}
