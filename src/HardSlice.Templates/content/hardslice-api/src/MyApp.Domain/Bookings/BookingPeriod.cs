namespace MyApp.Domain.Bookings;

public sealed record BookingPeriod
{
    public BookingPeriod(DateTimeOffset start, DateTimeOffset end)
    {
        // Bookings use UTC instants at microsecond precision, including before persistence.
        // Validate the stored bounds: truncation must never turn a valid period into an empty one.
        start = Normalize(start);
        end = Normalize(end);
        if (end <= start)
            throw new DomainException("A booking period must end after it starts.");

        Start = start;
        End = end;
    }

    public DateTimeOffset Start { get; }

    public DateTimeOffset End { get; }

    // Half-open [Start, End): a period ending when another starts does not overlap it.
    public bool Overlaps(BookingPeriod other) => Start < other.End && other.Start < End;

    private static DateTimeOffset Normalize(DateTimeOffset value) =>
        new(value.UtcTicks - value.UtcTicks % TimeSpan.TicksPerMicrosecond, TimeSpan.Zero);

    public override string ToString() => $"[{Start:O}, {End:O})";
}
