namespace Demo.Domain.Bookings;

// The bookings waiting for their room to free up, in the order they joined.
public sealed class Waitlist : IAggregateRoot
{
    private readonly List<WaitlistEntry> _entries = [];

    public Waitlist(Guid roomId) => RoomId = roomId;

    public Guid RoomId { get; }

    public IReadOnlyList<WaitlistEntry> Entries => _entries.AsReadOnly();

    public void Join(Booking booking)
    {
        if (booking.RoomId != RoomId)
            throw new DomainException($"Booking '{booking.Id}' is for another room.");
        if (Contains(booking.Id))
            throw new DomainException($"Booking '{booking.Id}' is already on the waitlist.");

        _entries.Add(new WaitlistEntry(booking.Id, _entries.Count + 1));
    }

    public bool Contains(BookingId bookingId) => _entries.Any(entry => entry.BookingId == bookingId);

    public IReadOnlyList<IDomainEvent> DequeueDomainEvents() => [];
}

public sealed class WaitlistEntry : IEntity
{
    internal WaitlistEntry(BookingId bookingId, int position)
    {
        BookingId = bookingId;
        Position = position;
    }

    public BookingId BookingId { get; }

    public int Position { get; }
}
