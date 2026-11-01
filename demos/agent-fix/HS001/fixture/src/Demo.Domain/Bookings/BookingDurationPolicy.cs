using Microsoft.Extensions.Configuration;

namespace Demo.Domain.Bookings;

// A booking lasts at most the maximum duration of the application.
public sealed class BookingDurationPolicy(IConfiguration configuration)
{
    public TimeSpan Maximum { get; } = TimeSpan.FromHours(configuration.GetValue("Bookings:MaximumHours", 24));

    public void Check(BookingPeriod period)
    {
        if (period.End - period.Start > Maximum)
            throw new DomainException($"A booking lasts at most {Maximum.TotalHours} hours.");
    }
}
