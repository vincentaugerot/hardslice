using FluentValidation.TestHelper;
using Shop.Features.Catalog.RegisterProduct;

namespace Shop.Tests.Features.Catalog.RegisterProduct;

// One test per rule of the validator: the price rule belongs to the aggregate, tested in ProductTests.
public sealed class RegisterProductValidatorTests
{
    private readonly RegisterProductValidator _validator = new();

    [Fact]
    public void Accepts_a_complete_command()
    {
        _validator.TestValidate(new RegisterProductCommand("Coffee mug", 12.50m))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Accepts_a_name_of_100_characters()
    {
        _validator.TestValidate(new RegisterProductCommand(new string('a', 100), 12.50m))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Refuses_a_missing_name(string? name)
    {
        _validator.TestValidate(new RegisterProductCommand(name!, 12.50m))
            .ShouldHaveValidationErrorFor(command => command.Name);
    }

    [Fact]
    public void Refuses_a_name_longer_than_100_characters()
    {
        _validator.TestValidate(new RegisterProductCommand(new string('a', 101), 12.50m))
            .ShouldHaveValidationErrorFor(command => command.Name);
    }
}
