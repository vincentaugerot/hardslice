using Microsoft.Extensions.Configuration;
using Demo.Domain;
using Demo.Domain.Bookings;

namespace Demo.Tests.Domain.Bookings;

public sealed class BookingDurationPolicyTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    private readonly BookingDurationPolicy _policy = new(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Bookings:MaximumHours"] = "4" })
        .Build());

    [Fact]
    public void Accepts_a_booking_up_to_the_maximum_duration()
    {
        Should.NotThrow(() => _policy.Check(new BookingPeriod(Start, Start.AddHours(4))));
    }

    [Fact]
    public void Rejects_a_booking_longer_than_the_maximum_duration()
    {
        Should.Throw<DomainException>(() => _policy.Check(new BookingPeriod(Start, Start.AddHours(5))));
    }
}
