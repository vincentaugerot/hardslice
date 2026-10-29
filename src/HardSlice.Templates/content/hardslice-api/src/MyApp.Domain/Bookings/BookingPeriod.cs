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

    // Half-open [Start, End): a period ending when another starts does not overlap it.
    public bool Overlaps(BookingPeriod other) => Start < other.End && other.Start < End;

    public override string ToString() => $"[{Start:O}, {End:O})";
}
