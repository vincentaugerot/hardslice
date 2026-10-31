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

    [Theory]
    [InlineData(1)]
    [InlineData(-7)]
    public void Normalizes_offsets_to_UTC_without_changing_the_instants(int offsetHours)
    {
        var offset = TimeSpan.FromHours(offsetHours);
        var period = new BookingPeriod(Start.ToOffset(offset), Start.AddHours(1).ToOffset(offset));

        period.Start.ShouldBe(Start);
        period.End.ShouldBe(Start.AddHours(1));
        period.Start.Offset.ShouldBe(TimeSpan.Zero);
        period.End.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Theory]
    [InlineData(1999)]
    [InlineData(2026)]
    public void Truncates_bounds_to_microseconds_before_comparing_periods(int year)
    {
        var start = new DateTimeOffset(year, 11, 2, 9, 0, 0, TimeSpan.Zero);
        var period = new BookingPeriod(start.AddTicks(1), start.AddTicks(19));

        period.Start.ShouldBe(start);
        period.End.ShouldBe(start.AddTicks(10));
        period.ShouldBe(new BookingPeriod(start, start.AddTicks(10)));
        period.Overlaps(new BookingPeriod(start.AddTicks(10), start.AddTicks(20))).ShouldBeFalse();
    }

    [Fact]
    public void Rejects_a_period_that_collapses_at_microsecond_precision()
    {
        Should.Throw<DomainException>(() => new BookingPeriod(Start.AddTicks(1), Start.AddTicks(2)));
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

    [Theory]
    [InlineData(0, 60, 30, 90)]
    [InlineData(30, 90, 0, 60)]
    [InlineData(0, 120, 30, 60)]
    [InlineData(30, 60, 0, 120)]
    [InlineData(0, 60, 0, 60)]
    public void Overlaps_a_period_sharing_part_of_its_time(int startMinute, int endMinute, int otherStartMinute, int otherEndMinute)
    {
        Period(startMinute, endMinute).Overlaps(Period(otherStartMinute, otherEndMinute)).ShouldBeTrue();
    }

    [Theory]
    [InlineData(0, 60, 60, 120)]
    [InlineData(60, 120, 0, 60)]
    [InlineData(0, 60, 90, 120)]
    public void Does_not_overlap_an_adjacent_or_disjoint_period(int startMinute, int endMinute, int otherStartMinute, int otherEndMinute)
    {
        Period(startMinute, endMinute).Overlaps(Period(otherStartMinute, otherEndMinute)).ShouldBeFalse();
    }

    private static BookingPeriod Period(int startMinute, int endMinute) =>
        new(Start.AddMinutes(startMinute), Start.AddMinutes(endMinute));
}
