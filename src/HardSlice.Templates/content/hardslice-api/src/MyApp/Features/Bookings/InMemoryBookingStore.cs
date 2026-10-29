using System.Collections.Concurrent;
using MyApp.Domain.Bookings;

namespace MyApp.Features.Bookings;

internal sealed class InMemoryBookingStore : IBookingStore
{
    private readonly ConcurrentDictionary<BookingId, Booking> _bookings = new();

    public Task AddAsync(Booking booking, CancellationToken cancellationToken)
    {
        if (!_bookings.TryAdd(booking.Id, booking))
            throw new InvalidOperationException($"Booking '{booking.Id}' is already stored.");

        return Task.CompletedTask;
    }

    public Task<Booking?> FindAsync(BookingId id, CancellationToken cancellationToken) =>
        Task.FromResult(_bookings.GetValueOrDefault(id));

    public Task UpdateAsync(Booking booking, CancellationToken cancellationToken)
    {
        _bookings[booking.Id] = booking;
        return Task.CompletedTask;
    }
}
