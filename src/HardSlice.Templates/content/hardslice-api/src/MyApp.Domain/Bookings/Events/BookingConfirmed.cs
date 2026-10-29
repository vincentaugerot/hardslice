namespace MyApp.Domain.Bookings.Events;

public sealed record BookingConfirmed(BookingId BookingId, Guid RoomId, BookingPeriod Period) : IDomainEvent;
