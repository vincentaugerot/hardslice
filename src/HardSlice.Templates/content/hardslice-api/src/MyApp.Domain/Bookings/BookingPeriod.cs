namespace MyApp.Domain.Bookings;

public sealed record BookingPeriod
{
    public BookingPeriod(DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start)
            throw new DomainException("A booking period must end after it starts.");

        Start = start;
        End = end;
    }

    public DateTimeOffset Start { get; }

    public DateTimeOffset End { get; }
}
