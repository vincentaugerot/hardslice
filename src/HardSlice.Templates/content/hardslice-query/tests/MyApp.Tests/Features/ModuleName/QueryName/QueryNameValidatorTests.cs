using FluentValidation.TestHelper;
using MyApp.Features.ModuleName.QueryName;

namespace MyApp.Tests.Features.ModuleName.QueryName;

// One test per rule of the validator: change them with the filters of the query.
public sealed class QueryNameValidatorTests
{
    private readonly QueryNameValidator _validator = new();

    [Fact]
    public void Accepts_a_complete_query()
    {
        _validator.TestValidate(new QueryNameQuery(ParameterValues))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Names_every_missing_filter()
    {
        var result = _validator.TestValidate(new QueryNameQuery(ParameterNulls));

        ParameterMissingAssertions
    }
}
