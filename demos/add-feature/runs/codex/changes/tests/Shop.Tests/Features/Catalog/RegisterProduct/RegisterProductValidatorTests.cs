using FluentValidation.TestHelper;
using Shop.Features.Catalog.RegisterProduct;

namespace Shop.Tests.Features.Catalog.RegisterProduct;

// One test per rule of the validator: change them with the fields of the command.
public sealed class RegisterProductValidatorTests
{
    private readonly RegisterProductValidator _validator = new();

    [Fact]
    public void Accepts_a_complete_command()
    {
        _validator.TestValidate(new RegisterProductCommand("Desk lamp", 29.95m))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    public void Rejects_a_missing_name(string? name)
    {
        var result = _validator.TestValidate(new RegisterProductCommand(name!, 29.95m));

        result.ShouldHaveValidationErrorFor(command => command.Name);
    }

    [Fact]
    public void Rejects_a_name_longer_than_100_characters()
    {
        _validator.TestValidate(new RegisterProductCommand(new string('a', 101), 1m))
            .ShouldHaveValidationErrorFor(command => command.Name);
    }

    [Fact]
    public void Accepts_a_name_of_100_characters()
    {
        _validator.TestValidate(new RegisterProductCommand(new string('a', 100), 1m))
            .ShouldNotHaveAnyValidationErrors();
    }
}
