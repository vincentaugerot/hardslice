namespace Demo.Domain.Bookings.Events;

// An entity too, so that the outbox we are about to add can store it with an identity. Its state has no public setter,
// as an entity requires.
public sealed record BookingConfirmed : IDomainEvent, IEntity
{
    public BookingConfirmed(BookingId bookingId, Guid roomId, BookingPeriod period)
    {
        BookingId = bookingId;
        RoomId = roomId;
        Period = period;
    }

    public BookingId BookingId { get; }

    public Guid RoomId { get; }

    public BookingPeriod Period { get; }
}
