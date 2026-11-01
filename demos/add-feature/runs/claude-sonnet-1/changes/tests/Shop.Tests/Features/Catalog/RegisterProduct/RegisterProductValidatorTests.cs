using FluentValidation.TestHelper;
using Shop.Features.Catalog.RegisterProduct;

namespace Shop.Tests.Features.Catalog.RegisterProduct;

public sealed class RegisterProductValidatorTests
{
    private readonly RegisterProductValidator _validator = new();

    [Fact]
    public void Accepts_a_complete_command()
    {
        _validator.TestValidate(new RegisterProductCommand("Keyboard", 49.90m))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Accepts_a_name_of_100_characters()
    {
        _validator.TestValidate(new RegisterProductCommand(new string('a', 100), 1m))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Refuses_a_missing_name(string? name)
    {
        _validator.TestValidate(new RegisterProductCommand(name!, 1m))
            .ShouldHaveValidationErrorFor(command => command.Name);
    }

    [Fact]
    public void Refuses_a_name_over_100_characters()
    {
        _validator.TestValidate(new RegisterProductCommand(new string('a', 101), 1m))
            .ShouldHaveValidationErrorFor(command => command.Name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Refuses_a_price_that_is_not_greater_than_zero(int price)
    {
        _validator.TestValidate(new RegisterProductCommand("Keyboard", price))
            .ShouldHaveValidationErrorFor(command => command.Price);
    }
}
