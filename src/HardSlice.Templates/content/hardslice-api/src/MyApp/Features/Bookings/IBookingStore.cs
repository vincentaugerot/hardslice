using MyApp.Domain.Bookings;

namespace MyApp.Features.Bookings;

// Module contract shared by the Bookings slices; persistence stays outside the domain.
internal interface IBookingStore
{
    Task AddAsync(Booking booking, CancellationToken cancellationToken);

    Task<Booking?> FindAsync(BookingId id, CancellationToken cancellationToken);

    Task UpdateAsync(Booking booking, CancellationToken cancellationToken);
}
