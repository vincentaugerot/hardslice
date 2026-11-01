using FluentValidation.TestHelper;
using Demo.Features.Bookings.DeleteBooking;

namespace Demo.Tests.Features.Bookings.DeleteBooking;

public sealed class DeleteBookingValidatorTests
{
    private readonly DeleteBookingValidator _validator = new();

    [Fact]
    public void Accepts_a_booking_id()
    {
        _validator.TestValidate(new DeleteBookingCommand(Guid.NewGuid())).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Rejects_an_empty_booking_id()
    {
        _validator.TestValidate(new DeleteBookingCommand(Guid.Empty)).ShouldHaveValidationErrorFor(command => command.BookingId);
    }
}
