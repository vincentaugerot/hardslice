using FluentValidation.TestHelper;
using Shop.Features.Catalog.RegisterProduct;

namespace Shop.Tests.Features.Catalog.RegisterProduct;

public sealed class RegisterProductValidatorTests
{
    private readonly RegisterProductValidator _validator = new();

    [Fact]
    public void Accepts_a_complete_command()
    {
        _validator.TestValidate(new RegisterProductCommand("Coffee Mug", 12.50m))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Rejects_an_empty_name()
    {
        _validator.TestValidate(new RegisterProductCommand("", 12.50m))
            .ShouldHaveValidationErrorFor(command => command.Name);
    }

    [Fact]
    public void Rejects_a_name_longer_than_100_characters()
    {
        _validator.TestValidate(new RegisterProductCommand(new string('a', 101), 12.50m))
            .ShouldHaveValidationErrorFor(command => command.Name);
    }

    [Fact]
    public void Rejects_a_price_of_zero()
    {
        _validator.TestValidate(new RegisterProductCommand("Coffee Mug", 0m))
            .ShouldHaveValidationErrorFor(command => command.Price);
    }

    [Fact]
    public void Rejects_a_negative_price()
    {
        _validator.TestValidate(new RegisterProductCommand("Coffee Mug", -1m))
            .ShouldHaveValidationErrorFor(command => command.Price);
    }
}
