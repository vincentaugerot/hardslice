using MyApp.Domain;
using MyApp.Domain.Bookings;

namespace MyApp.Tests.Domain.Bookings;

public sealed class BookingPeriodTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Accepts_an_end_after_the_start()
    {
        var period = new BookingPeriod(Start, Start.AddHours(1));

        period.Start.ShouldBe(Start);
        period.End.ShouldBe(Start.AddHours(1));
    }

    [Fact]
    public void Rejects_an_end_equal_to_the_start()
    {
        Should.Throw<DomainException>(() => new BookingPeriod(Start, Start));
    }

    [Fact]
    public void Rejects_an_end_before_the_start()
    {
        Should.Throw<DomainException>(() => new BookingPeriod(Start, Start.AddMinutes(-1)));
    }
}
