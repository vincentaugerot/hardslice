namespace Demo.Domain.Bookings.Events;

public sealed record BookingCancelled(BookingId BookingId, DateTimeOffset CancelledAt) : IDomainEvent;
