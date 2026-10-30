using FluentValidation;
using MyApp.Shared.Messaging;

namespace MyApp.ArchitectureTests;

public sealed class RequestTypesTests
{
    [Fact]
    public void A_command_is_a_concrete_ICommand_never_a_query()
    {
        RequestTypes.IsCommand(typeof(ProbeCommand)).ShouldBeTrue();
        RequestTypes.IsCommand(typeof(ProbeStructCommand)).ShouldBeTrue();
        RequestTypes.IsCommand(typeof(ProbeQuery)).ShouldBeFalse();
        RequestTypes.IsCommand(typeof(AbstractProbeCommand)).ShouldBeFalse();
    }

    [Fact]
    public void The_parameters_of_a_command_are_its_public_instance_properties_and_fields()
    {
        RequestTypes.ParametersOf(typeof(ProbeCommand)).ShouldBe(["BookingId", "Reason"], ignoreOrder: true);
        RequestTypes.ParametersOf(typeof(ProbeStructCommand)).ShouldBe(["Count"]);
        RequestTypes.ParametersOf(typeof(ParameterlessProbeCommand)).ShouldBeEmpty();
    }

    [Fact]
    public void A_handler_handles_a_command_only_when_its_request_is_one()
    {
        RequestTypes.CommandHandledBy(typeof(ProbeCommandHandler)).ShouldBe(typeof(ProbeCommand));
        RequestTypes.CommandHandledBy(typeof(GenericProbeCommandHandler<>)).ShouldNotBeNull().IsGenericParameter.ShouldBeTrue();
        RequestTypes.CommandHandledBy(typeof(ProbeQueryHandler)).ShouldBeNull();
        RequestTypes.CommandHandledBy(typeof(ProbeCommand)).ShouldBeNull();
    }

    [Fact]
    public void A_concrete_validator_validates_its_type_and_every_construction_of_a_generic_one()
    {
        var validated = RequestTypes.ValidatorsIn([typeof(ProbeCommandValidator), typeof(GenericProbeCommandValidator<>), typeof(AbstractProbeValidator)]);

        RequestTypes.IsValidated(typeof(ProbeCommand), validated).ShouldBeTrue();
        RequestTypes.IsValidated(typeof(GenericProbeCommand<>), validated).ShouldBeTrue();
        RequestTypes.IsValidated(typeof(GenericProbeCommand<string>), validated).ShouldBeTrue();
        RequestTypes.IsValidated(typeof(ParameterlessProbeCommand), validated).ShouldBeFalse();
        RequestTypes.IsValidated(typeof(ProbeStructCommand), validated).ShouldBeFalse();
    }

    [Fact]
    public void A_closed_validator_does_not_cover_other_constructions_or_the_open_command()
    {
        var validators = RequestTypes.ValidatorsIn([typeof(IntProbeCommandValidator)]);

        RequestTypes.IsValidated(typeof(GenericProbeCommand<int>), validators).ShouldBeTrue();
        RequestTypes.IsValidated(typeof(GenericProbeCommand<string>), validators).ShouldBeFalse();
        RequestTypes.IsValidated(typeof(GenericProbeCommand<>), validators).ShouldBeFalse();
    }

    [Fact]
    public void A_constrained_validator_must_accept_the_commands_arguments_and_open_parameters()
    {
        var validators = RequestTypes.ValidatorsIn([typeof(StructProbeCommandValidator<>)]);

        RequestTypes.IsValidated(typeof(GenericProbeCommand<int>), validators).ShouldBeTrue();
        RequestTypes.IsValidated(typeof(GenericProbeCommand<string>), validators).ShouldBeFalse();
        RequestTypes.IsValidated(typeof(GenericProbeCommand<>), validators).ShouldBeFalse();

        var constrained = RequestTypes.ValidatorsIn([typeof(ConstrainedProbeCommandValidator<>)]);
        RequestTypes.IsValidated(typeof(ConstrainedProbeCommand<>), constrained).ShouldBeTrue();
    }

    [Fact]
    public void Interface_and_constructor_constraints_are_checked_too()
    {
        var validators = RequestTypes.ValidatorsIn([typeof(InputProbeCommandValidator<>)]);

        RequestTypes.IsValidated(typeof(GenericProbeCommand<ProbeInput>), validators).ShouldBeTrue();
        RequestTypes.IsValidated(typeof(GenericProbeCommand<InputWithoutDefaultConstructor>), validators).ShouldBeFalse();
        RequestTypes.IsValidated(typeof(GenericProbeCommand<object>), validators).ShouldBeFalse();
        RequestTypes.IsValidated(typeof(GenericProbeCommand<>), validators).ShouldBeFalse();
    }

    [Fact]
    public void A_validator_generic_over_the_entire_command_matches_compatible_commands()
    {
        var validators = RequestTypes.ValidatorsIn([typeof(AnyProbeCommandValidator<>)]);

        RequestTypes.IsValidated(typeof(ProbeCommand), validators).ShouldBeTrue();
        RequestTypes.IsValidated(typeof(ProbeStructCommand), validators).ShouldBeFalse();
    }

    [Fact]
    public void Repeated_parameters_cannot_match_different_arguments()
    {
        var validators = RequestTypes.ValidatorsIn([typeof(SameArgumentsValidator<>)]);

        RequestTypes.IsValidated(typeof(PairCommand<int, int>), validators).ShouldBeTrue();
        RequestTypes.IsValidated(typeof(PairCommand<int, string>), validators).ShouldBeFalse();
        RequestTypes.IsValidated(typeof(PairCommand<,>), validators).ShouldBeFalse();
    }

    [Fact]
    public void Generic_parameters_are_bound_by_the_contract_not_by_position()
    {
        var validators = RequestTypes.ValidatorsIn([typeof(ReversedArgumentsValidator<,>)]);

        RequestTypes.IsValidated(typeof(PairCommand<int, string>), validators).ShouldBeTrue();
        RequestTypes.IsValidated(typeof(PairCommand<,>), validators).ShouldBeTrue();
    }

    [Fact]
    public void Nested_generic_arguments_do_not_cover_every_construction()
    {
        var validators = RequestTypes.ValidatorsIn([typeof(ListProbeCommandValidator<>)]);

        RequestTypes.IsValidated(typeof(GenericProbeCommand<List<int>>), validators).ShouldBeTrue();
        RequestTypes.IsValidated(typeof(GenericProbeCommand<int>), validators).ShouldBeFalse();
        RequestTypes.IsValidated(typeof(GenericProbeCommand<>), validators).ShouldBeFalse();
    }

    private sealed record ProbeCommand(Guid BookingId) : ICommand<bool>
    {
        public string? Reason = null;

        public static int Instances => 0;
    }

    private record struct ProbeStructCommand(int Count) : ICommand<int>;

    private sealed record ParameterlessProbeCommand : ICommand<int>;

    private abstract record AbstractProbeCommand(Guid Id) : ICommand<bool>;

    private sealed record GenericProbeCommand<T>(T Value) : ICommand<T>;

    private sealed record ProbeQuery(Guid Id) : IQuery<bool>;

    private sealed class ProbeCommandHandler : IRequestHandler<ProbeCommand, bool>
    {
        public Task<bool> Handle(ProbeCommand command, CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class GenericProbeCommandHandler<TCommand> : IRequestHandler<TCommand, bool> where TCommand : ICommand<bool>
    {
        public Task<bool> Handle(TCommand command, CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class ProbeQueryHandler : IRequestHandler<ProbeQuery, bool>
    {
        public Task<bool> Handle(ProbeQuery query, CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class ProbeCommandValidator : AbstractValidator<ProbeCommand>;

    private sealed class GenericProbeCommandValidator<T> : AbstractValidator<GenericProbeCommand<T>>;

    private sealed class IntProbeCommandValidator : AbstractValidator<GenericProbeCommand<int>>;

    private sealed class StructProbeCommandValidator<T> : AbstractValidator<GenericProbeCommand<T>> where T : struct;

    private sealed record ConstrainedProbeCommand<T>(T Value) : ICommand<T> where T : struct;

    private sealed class ConstrainedProbeCommandValidator<T> : AbstractValidator<ConstrainedProbeCommand<T>> where T : struct;

    private interface IProbeInput;

    private sealed class ProbeInput : IProbeInput;

    private sealed class InputWithoutDefaultConstructor : IProbeInput
    {
        public InputWithoutDefaultConstructor(int value) { }
    }

    private sealed class InputProbeCommandValidator<T> : AbstractValidator<GenericProbeCommand<T>> where T : IProbeInput, new();

    private sealed class AnyProbeCommandValidator<TCommand> : AbstractValidator<TCommand> where TCommand : ICommand<bool>;

    private sealed record PairCommand<TLeft, TRight>(TLeft Left, TRight Right) : ICommand<bool>;

    private sealed class SameArgumentsValidator<T> : AbstractValidator<PairCommand<T, T>>;

    private sealed class ReversedArgumentsValidator<TLeft, TRight> : AbstractValidator<PairCommand<TRight, TLeft>>;

    private sealed class ListProbeCommandValidator<T> : AbstractValidator<GenericProbeCommand<List<T>>>;

    private abstract class AbstractProbeValidator : AbstractValidator<ProbeStructCommand>;
}
