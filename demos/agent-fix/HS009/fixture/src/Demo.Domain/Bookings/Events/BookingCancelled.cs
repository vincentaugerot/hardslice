namespace Demo.Domain.Bookings.Events;

public sealed class BookingCancelled(BookingId bookingId, Guid roomId) : IDomainEvent
{
    public BookingId BookingId { get; } = bookingId;

    public Guid RoomId { get; } = roomId;
}
