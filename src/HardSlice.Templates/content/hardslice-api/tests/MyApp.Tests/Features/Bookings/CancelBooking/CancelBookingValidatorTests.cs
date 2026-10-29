using FluentValidation.TestHelper;
using MyApp.Features.Bookings.CancelBooking;

namespace MyApp.Tests.Features.Bookings.CancelBooking;

public sealed class CancelBookingValidatorTests
{
    private readonly CancelBookingValidator _validator = new();

    [Fact]
    public void Accepts_a_booking_id()
    {
        _validator.TestValidate(new CancelBookingCommand(Guid.NewGuid())).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Rejects_an_empty_booking_id()
    {
        _validator.TestValidate(new CancelBookingCommand(Guid.Empty)).ShouldHaveValidationErrorFor(command => command.BookingId);
    }
}
