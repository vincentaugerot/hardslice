using FluentValidation.TestHelper;
using MyApp.Features.ModuleName.CommandName;

namespace MyApp.Tests.Features.ModuleName.CommandName;

// One test per rule of the validator: change them with the fields of the command.
public sealed class CommandNameValidatorTests
{
    private readonly CommandNameValidator _validator = new();

    [Fact]
    public void Accepts_a_complete_command()
    {
#if (ParametersIsList)
        _validator.TestValidate(new CommandNameCommand(ParameterValues))
#else
        _validator.TestValidate(new CommandNameCommand(Guid.NewGuid()))
#endif
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Names_every_missing_field()
    {
#if (ParametersIsList)
        var result = _validator.TestValidate(new CommandNameCommand(ParameterNulls));

        ParameterMissingAssertions
#else
        var result = _validator.TestValidate(new CommandNameCommand(Guid.Empty));

        result.ShouldHaveValidationErrorFor(command => command.Id);
#endif
    }
}
