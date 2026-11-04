using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MyApp.Domain;
using MyApp.Shared.Http;

namespace MyApp.Tests.Shared.Http;

// The answers of the HTTP pipeline to an unreadable request, a validation error and a domain error, as Program.cs registers them.
public sealed class ExceptionHandlersTests
{
    [Fact]
    public async Task Answers_an_unreadable_request_with_its_status_and_the_reason_it_could_not_be_read()
    {
        var unreadable = new BadHttpRequestException("Failed to read the body.", StatusCodes.Status400BadRequest, new JsonException("A date and time must be an ISO 8601 string with its offset."));

        var (handled, status, problem) = await Handle(services => new BadRequestExceptionHandler(services.GetRequiredService<IProblemDetailsService>()), unreadable);

        handled.ShouldBeTrue();
        status.ShouldBe(StatusCodes.Status400BadRequest);
        problem.GetProperty("title").GetString().ShouldBe("The request could not be read.");
        problem.GetProperty("detail").GetString().ShouldBe("A date and time must be an ISO 8601 string with its offset.");
    }

    [Fact]
    public async Task Answers_a_validation_error_with_400_and_the_failures_by_field_as_the_client_names_it()
    {
        var invalid = new ValidationException([new ValidationFailure("RoomId", "'Room Id' must not be empty."), new ValidationFailure("RoomId", "'Room Id' is unknown.")]);

        var (handled, status, problem) = await Handle(
            services => new ValidationExceptionHandler(services.GetRequiredService<IProblemDetailsService>(), services.GetRequiredService<IOptions<JsonOptions>>()),
            invalid);

        handled.ShouldBeTrue();
        status.ShouldBe(StatusCodes.Status400BadRequest);
        problem.GetProperty("errors").GetProperty("roomId").EnumerateArray().Select(error => error.GetString())
            .ShouldBe(["'Room Id' must not be empty.", "'Room Id' is unknown."]);
    }

    [Fact]
    public async Task Answers_a_broken_rule_with_400_and_a_conflict_with_409()
    {
        var (ruleHandled, ruleStatus, rule) = await Handle(DomainHandler, new DomainException("The period must end after it starts."));
        var (conflictHandled, conflictStatus, conflict) = await Handle(DomainHandler, new ConflictException("The room is already booked."));

        ruleHandled.ShouldBeTrue();
        ruleStatus.ShouldBe(StatusCodes.Status400BadRequest);
        rule.GetProperty("title").GetString().ShouldBe("A business rule was violated.");
        rule.GetProperty("detail").GetString().ShouldBe("The period must end after it starts.");
        conflictHandled.ShouldBeTrue();
        conflictStatus.ShouldBe(StatusCodes.Status409Conflict);
        conflict.GetProperty("title").GetString().ShouldBe("The request conflicts with the current state.");
        conflict.GetProperty("detail").GetString().ShouldBe("The room is already booked.");
    }

    [Fact]
    public async Task Each_handler_leaves_any_other_exception_to_the_next_one()
    {
        var other = new InvalidOperationException("Not an answer of the HTTP pipeline.");

        (await Handle(services => new BadRequestExceptionHandler(services.GetRequiredService<IProblemDetailsService>()), other)).Handled.ShouldBeFalse();
        (await Handle(
            services => new ValidationExceptionHandler(services.GetRequiredService<IProblemDetailsService>(), services.GetRequiredService<IOptions<JsonOptions>>()),
            other)).Handled.ShouldBeFalse();
        (await Handle(DomainHandler, other)).Handled.ShouldBeFalse();
    }

    private static DomainExceptionHandler DomainHandler(IServiceProvider services) => new(services.GetRequiredService<IProblemDetailsService>());

    private static async Task<(bool Handled, int Status, JsonElement Problem)> Handle(Func<IServiceProvider, IExceptionHandler> create, Exception exception)
    {
        await using var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        using var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = services, Response = { Body = body } };

        var handled = await create(services).TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        var problem = body.Length == 0 ? default : JsonDocument.Parse(body.ToArray()).RootElement.Clone();
        return (handled, context.Response.StatusCode, problem);
    }
}
