using FluentValidation.TestHelper;
using MyApp.Features.Bookings.ConfirmBooking;

namespace MyApp.Tests.Features.Bookings.ConfirmBooking;

public sealed class ConfirmBookingValidatorTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    private readonly ConfirmBookingValidator _validator = new();

    [Fact]
    public void Accepts_a_complete_command()
    {
        _validator.TestValidate(new ConfirmBookingCommand(Guid.NewGuid(), Start, Start.AddHours(1)))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Names_every_missing_field()
    {
        var result = _validator.TestValidate(new ConfirmBookingCommand(Guid.Empty, default, default));

        result.ShouldHaveValidationErrorFor(command => command.RoomId);
        result.ShouldHaveValidationErrorFor(command => command.Start);
        result.ShouldHaveValidationErrorFor(command => command.End);
    }

    // A period that does not end after its start is a business rule: BookingPeriod rejects it, not the validator.
    [Fact]
    public void Leaves_the_period_rule_to_the_domain()
    {
        _validator.TestValidate(new ConfirmBookingCommand(Guid.NewGuid(), Start, Start.AddHours(-1)))
            .ShouldNotHaveAnyValidationErrors();
    }
}
