using Demo.Domain;
using Demo.Domain.Bookings;
using Demo.Features.Bookings;
using Demo.Features.Bookings.CancelBooking;

namespace Demo.Tests.Features.Bookings.CancelBooking;

public sealed class CancelBookingTests
{
    private static readonly BookingPeriod Period = new(
        new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 11, 2, 10, 0, 0, TimeSpan.Zero));

    private readonly InMemoryBookingStore _store = new();

    private readonly TimeProvider _beforeStart = new FixedClock(Period.Start.AddDays(-1));

    [Fact]
    public async Task Cancels_an_existing_booking()
    {
        var booking = Booking.Confirm(Guid.NewGuid(), Period);
        await _store.AddAsync(booking, TestContext.Current.CancellationToken);

        var cancelled = await new CancelBookingHandler(_store, _beforeStart).Handle(
            new CancelBookingCommand(booking.Id.Value), TestContext.Current.CancellationToken);

        cancelled.ShouldBeTrue();
        var stored = await _store.FindAsync(booking.Id, TestContext.Current.CancellationToken);
        stored.ShouldNotBeNull();
        stored.Status.ShouldBe(BookingStatus.Cancelled);
    }

    [Fact]
    public async Task Reports_an_unknown_booking()
    {
        var cancelled = await new CancelBookingHandler(_store, _beforeStart).Handle(
            new CancelBookingCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        cancelled.ShouldBeFalse();
    }

    [Fact]
    public async Task Allows_only_one_of_concurrent_cancellations()
    {
        const int contenderCount = 32;
        var booking = Booking.Confirm(Guid.NewGuid(), Period);
        await _store.AddAsync(booking, TestContext.Current.CancellationToken);
        var handler = new CancelBookingHandler(_store, _beforeStart);
        using var ready = new CountdownEvent(contenderCount);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var attempts = Enumerable.Range(0, contenderCount)
            .Select(_ => Task.Run(async () =>
            {
                ready.Signal();
                await start.Task;

                try
                {
                    return (Succeeded: await handler.Handle(
                        new CancelBookingCommand(booking.Id.Value), TestContext.Current.CancellationToken), Exception: (DomainException?)null);
                }
                catch (DomainException exception)
                {
                    return (Succeeded: false, Exception: exception);
                }
            }, TestContext.Current.CancellationToken))
            .ToArray();

        ready.Wait(TestContext.Current.CancellationToken);
        start.SetResult();
        var outcomes = await Task.WhenAll(attempts);

        outcomes.Count(outcome => outcome.Succeeded).ShouldBe(1);
        outcomes.Count(outcome => outcome.Exception is not null).ShouldBe(contenderCount - 1);
        booking.Status.ShouldBe(BookingStatus.Cancelled);
    }

    [Fact]
    public async Task Rejects_a_booking_that_has_started()
    {
        var booking = Booking.Confirm(Guid.NewGuid(), Period);
        await _store.AddAsync(booking, TestContext.Current.CancellationToken);

        await Should.ThrowAsync<DomainException>(() => new CancelBookingHandler(_store, new FixedClock(Period.Start)).Handle(
            new CancelBookingCommand(booking.Id.Value), TestContext.Current.CancellationToken));

        booking.Status.ShouldBe(BookingStatus.Confirmed);
    }
}
