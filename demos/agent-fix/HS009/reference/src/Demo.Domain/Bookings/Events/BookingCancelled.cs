namespace Demo.Domain.Bookings.Events;

public sealed record BookingCancelled(BookingId BookingId, Guid RoomId) : IDomainEvent;
