namespace Demo.Domain.Bookings;

// A booking lasts at most the maximum duration of the application.
public sealed class BookingDurationPolicy(TimeSpan maximum)
{
    public TimeSpan Maximum { get; } = maximum;

    public void Check(BookingPeriod period)
    {
        if (period.End - period.Start > Maximum)
            throw new DomainException($"A booking lasts at most {Maximum.TotalHours} hours.");
    }
}
