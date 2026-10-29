namespace MyApp.Domain.Bookings;

public readonly record struct BookingId(Guid Value)
{
    public static BookingId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
