using FluentValidation;
using MyApp.Shared.Behaviors;
using MyApp.Shared.Messaging;

namespace MyApp.Tests.Shared.Behaviors;

public sealed class ValidationBehaviorTests
{
    [Fact]
    public async Task Rejects_an_invalid_request_with_the_failures_of_every_validator_before_its_handler()
    {
        var nameValidator = new InlineValidator<SampleCommand>();
        nameValidator.RuleFor(command => command.Name).NotEmpty();
        var quantityValidator = new InlineValidator<SampleCommand>();
        quantityValidator.RuleFor(command => command.Quantity).GreaterThan(0);
        var behavior = new ValidationBehavior<SampleCommand, int>([nameValidator, quantityValidator]);
        var handlerReached = false;

        var exception = await Should.ThrowAsync<ValidationException>(() => behavior.Handle(
            new SampleCommand("", 0), () => { handlerReached = true; return Task.FromResult(1); }, TestContext.Current.CancellationToken));

        exception.Errors.Select(failure => failure.PropertyName).ShouldBe(["Name", "Quantity"]);
        handlerReached.ShouldBeFalse();
    }

    [Fact]
    public async Task Passes_a_valid_request_to_its_handler()
    {
        var validator = new InlineValidator<SampleCommand>();
        validator.RuleFor(command => command.Name).NotEmpty();
        var behavior = new ValidationBehavior<SampleCommand, int>([validator]);

        var response = await behavior.Handle(new SampleCommand("lamp", 1), () => Task.FromResult(42), TestContext.Current.CancellationToken);

        response.ShouldBe(42);
    }

    public sealed record SampleCommand(string Name, int Quantity) : ICommand<int>;
}
