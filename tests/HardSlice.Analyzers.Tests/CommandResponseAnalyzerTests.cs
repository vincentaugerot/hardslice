using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using static HardSlice.Analyzers.Tests.AnalyzerHarness;

namespace HardSlice.Analyzers.Tests;

// HS005: a command handler never returns an entity, directly, in an envelope or in a DTO.
public sealed class CommandResponseAnalyzerTests
{
    private const string Rule = "Command handlers return an identifier, a DTO of values or nothing.";

    private const string Configuration =
        """
        {
          "style": "slices",
          "contracts": {
            "entity": "MyApp.Domain.IEntity, MyApp.Domain",
            "aggregateRoot": "MyApp.Domain.IAggregateRoot, MyApp.Domain",
            "domainEvent": "MyApp.Domain.IDomainEvent, MyApp.Domain",
            "command": "MyApp.Shared.Messaging.ICommand`1, MyApp",
            "query": "MyApp.Shared.Messaging.IQuery`1, MyApp",
            "requestHandler": "MyApp.Shared.Messaging.IRequestHandler`2, MyApp",
            "domainEventHandler": "MyApp.Shared.Messaging.IDomainEventHandler`1, MyApp"
          },
          "shared": {
            "allowedFolders": ["Messaging", "Persistence", "Behaviors"]
          }
        }
        """;

    // The Booking aggregate, its child entity BookingLine and its value objects.
    private const string Domain =
        """
        namespace MyApp.Domain
        {
            public interface IEntity;

            public interface IAggregateRoot : IEntity;

            public interface IDomainEvent;
        }

        namespace MyApp.Domain.Bookings
        {
            public readonly record struct BookingId(Guid Value);

            public sealed record BookingPeriod(DateTimeOffset Start, DateTimeOffset End);

            public sealed record BookingConfirmed(BookingId BookingId) : IDomainEvent;

            public sealed class Booking : IAggregateRoot
            {
                private readonly List<BookingLine> _lines = [];

                public BookingId Id { get; } = new(Guid.NewGuid());

                public BookingPeriod Period { get; } = new(default, default);

                public IReadOnlyList<BookingLine> Lines => _lines;
            }

            public sealed class BookingLine : IEntity
            {
                public Guid Id { get; } = Guid.NewGuid();

                public string Label { get; } = "";
            }
        }
        """;

    private const string Messaging =
        """
        namespace MyApp.Shared.Messaging
        {
            public interface IRequest<TResponse>;

            public interface ICommand<TResponse> : IRequest<TResponse>;

            public interface IQuery<TResponse> : IRequest<TResponse>;

            public interface IRequestHandler<in TRequest, TResponse> where TRequest : IRequest<TResponse>
            {
                Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
            }

            public interface IDomainEventHandler<in TEvent>
            {
                Task Handle(TEvent domainEvent, CancellationToken cancellationToken);
            }

            public readonly record struct Unit;

            public sealed record Result<T>(T? Value, string? Error);
        }
        """;

    private static CSharpCompilation CompileDomain() =>
        Compile("MyApp.Domain", [("Domain.cs", Domain), ("Usings.cs", ImplicitUsings)]);

    // The application, with the messaging of the template, referencing the domain as a project.
    private static Task<ImmutableArray<Diagnostic>> VerifyAsync(params string[] markedSources) =>
        VerifyAsync([Reference(CompileDomain(), asMetadata: false)], markedSources);

    private static Task<ImmutableArray<Diagnostic>> VerifyAsync(MetadataReference[] references, params string[] markedSources) =>
        AnalyzerHarness.VerifyAsync(
            new CommandResponseAnalyzer(),
            CommandResponseAnalyzer.DiagnosticId,
            "MyApp",
            Configuration,
            references,
            OutputKind.DynamicallyLinkedLibrary,
            [.. markedSources, Messaging]);

    // The first sentence of each message, without its rule and its fix.
    private static IEnumerable<string> Subjects(ImmutableArray<Diagnostic> diagnostics) =>
        diagnostics.Select(diagnostic => diagnostic.GetMessage().Split(". ")[0]);

    private static IEnumerable<string> Fixes(ImmutableArray<Diagnostic> diagnostics) =>
        diagnostics.Select(diagnostic => diagnostic.GetMessage().Split(" Fix: ")[1].Split(". Skill: ")[0]);

    [Fact]
    public async Task Reports_a_command_handler_that_returns_the_aggregate()
    {
        var diagnostics = await VerifyAsync(
            """
            using MyApp.Domain.Bookings;
            using MyApp.Shared.Messaging;

            namespace MyApp.Features.Bookings.ConfirmBooking;

            public sealed record ConfirmBookingCommand(Guid RoomId) : ICommand<Booking>;

            internal sealed class [|ConfirmBookingHandler|] : IRequestHandler<ConfirmBookingCommand, Booking>
            {
                public Task<Booking> Handle(ConfirmBookingCommand command, CancellationToken cancellationToken) => Task.FromResult(new Booking());
            }
            """);

        diagnostics.ShouldHaveSingleItem().GetMessage().ShouldBe(
            "handler 'ConfirmBookingHandler' of command 'ConfirmBookingCommand' returns the entity 'Booking'. "
            + Rule + " Fix: return 'BookingId', or a DTO built from the entity's values. Skill: fix-architecture-violation. Doc: 'docs/rules/HS005.md'.");
    }

    [Fact]
    public async Task Names_the_property_of_a_DTO_that_holds_the_entity()
    {
        var diagnostics = await VerifyAsync(
            """
            using MyApp.Domain.Bookings;
            using MyApp.Shared.Messaging;

            namespace MyApp.Features.Bookings.ConfirmBooking;

            public sealed record ConfirmBookingCommand(Guid RoomId) : ICommand<ConfirmationDto>;

            public sealed record ConfirmationDto(Guid RoomId, Booking Booking);

            internal sealed class [|ConfirmBookingHandler|] : IRequestHandler<ConfirmBookingCommand, ConfirmationDto>
            {
                public Task<ConfirmationDto> Handle(ConfirmBookingCommand command, CancellationToken cancellationToken) =>
                    Task.FromResult(new ConfirmationDto(command.RoomId, new Booking()));
            }
            """);

        diagnostics.ShouldHaveSingleItem().GetMessage().ShouldBe(
            "handler 'ConfirmBookingHandler' of command 'ConfirmBookingCommand' returns 'ConfirmationDto', whose property 'Booking' holds the entity 'Booking'. "
            + Rule + " Fix: return 'BookingId', or a DTO built from the entity's values. Skill: fix-architecture-violation. Doc: 'docs/rules/HS005.md'.");
    }

    [Fact]
    public async Task Reports_an_entity_in_an_envelope_a_collection_an_array_a_tuple_or_a_DTO()
    {
        var diagnostics = await VerifyAsync(
            """
            using MyApp.Domain;
            using MyApp.Domain.Bookings;
            using MyApp.Shared.Messaging;

            namespace MyApp.Features.Bookings.Leaks;

            public sealed record ListCommand : ICommand<List<Booking>>;
            public sealed record ArrayCommand : ICommand<Booking[]>;
            public sealed record TupleCommand : ICommand<(BookingId, Booking)>;
            public sealed record ResultCommand : ICommand<Result<Booking>>;
            public sealed record LineCommand : ICommand<BookingLine>;
            public sealed record NullableCommand : ICommand<Booking?>;
            public sealed record DictionaryCommand : ICommand<Dictionary<Guid, IReadOnlyList<BookingLine>>>;
            public sealed record LinesDtoCommand : ICommand<LinesDto>;
            public sealed record NestedDtoCommand : ICommand<SummaryDto>;
            public sealed record DtoListCommand : ICommand<IReadOnlyList<LinesDto>>;
            public sealed record FieldCommand : ICommand<FieldDto>;
            public sealed record RootContractCommand : ICommand<IAggregateRoot>;
            public sealed record CollectionCommand : ICommand<BookingList>;

            public sealed record LinesDto(Guid BookingId, IReadOnlyList<BookingLine> Lines);
            public sealed record SummaryDto(string Title, DetailsDto Details);
            public sealed record DetailsDto(int Count, Booking? Booking);
            public sealed class FieldDto { public BookingLine? Line; }
            public sealed class BookingList : List<Booking>;

            internal sealed class [|ListHandler|] : IRequestHandler<ListCommand, List<Booking>> { public Task<List<Booking>> Handle(ListCommand c, CancellationToken t) => throw new NotImplementedException(); }
            internal sealed class [|ArrayHandler|] : IRequestHandler<ArrayCommand, Booking[]> { public Task<Booking[]> Handle(ArrayCommand c, CancellationToken t) => throw new NotImplementedException(); }
            internal sealed class [|TupleHandler|] : IRequestHandler<TupleCommand, (BookingId, Booking)> { public Task<(BookingId, Booking)> Handle(TupleCommand c, CancellationToken t) => throw new NotImplementedException(); }
            internal sealed class [|ResultHandler|] : IRequestHandler<ResultCommand, Result<Booking>> { public Task<Result<Booking>> Handle(ResultCommand c, CancellationToken t) => throw new NotImplementedException(); }
            internal sealed class [|LineHandler|] : IRequestHandler<LineCommand, BookingLine> { public Task<BookingLine> Handle(LineCommand c, CancellationToken t) => throw new NotImplementedException(); }
            internal sealed class [|NullableHandler|] : IRequestHandler<NullableCommand, Booking?> { public Task<Booking?> Handle(NullableCommand c, CancellationToken t) => throw new NotImplementedException(); }
            internal sealed class [|DictionaryHandler|] : IRequestHandler<DictionaryCommand, Dictionary<Guid, IReadOnlyList<BookingLine>>> { public Task<Dictionary<Guid, IReadOnlyList<BookingLine>>> Handle(DictionaryCommand c, CancellationToken t) => throw new NotImplementedException(); }
            internal sealed class [|LinesDtoHandler|] : IRequestHandler<LinesDtoCommand, LinesDto> { public Task<LinesDto> Handle(LinesDtoCommand c, CancellationToken t) => throw new NotImplementedException(); }
            internal sealed class [|NestedDtoHandler|] : IRequestHandler<NestedDtoCommand, SummaryDto> { public Task<SummaryDto> Handle(NestedDtoCommand c, CancellationToken t) => throw new NotImplementedException(); }
            internal sealed class [|DtoListHandler|] : IRequestHandler<DtoListCommand, IReadOnlyList<LinesDto>> { public Task<IReadOnlyList<LinesDto>> Handle(DtoListCommand c, CancellationToken t) => throw new NotImplementedException(); }
            internal sealed class [|FieldHandler|] : IRequestHandler<FieldCommand, FieldDto> { public Task<FieldDto> Handle(FieldCommand c, CancellationToken t) => throw new NotImplementedException(); }
            internal sealed class [|RootContractHandler|] : IRequestHandler<RootContractCommand, IAggregateRoot> { public Task<IAggregateRoot> Handle(RootContractCommand c, CancellationToken t) => throw new NotImplementedException(); }
            internal sealed class [|CollectionHandler|] : IRequestHandler<CollectionCommand, BookingList> { public Task<BookingList> Handle(CollectionCommand c, CancellationToken t) => throw new NotImplementedException(); }
            """);

        Subjects(diagnostics).ShouldBe(
        [
            "handler 'ListHandler' of command 'ListCommand' returns 'List<Booking>', which holds the entity 'Booking'",
            "handler 'ArrayHandler' of command 'ArrayCommand' returns 'Booking[]', which holds the entity 'Booking'",
            "handler 'TupleHandler' of command 'TupleCommand' returns '(BookingId, Booking)', which holds the entity 'Booking'",
            "handler 'ResultHandler' of command 'ResultCommand' returns 'Result<Booking>', which holds the entity 'Booking'",
            "handler 'LineHandler' of command 'LineCommand' returns the entity 'BookingLine'",
            "handler 'NullableHandler' of command 'NullableCommand' returns the entity 'Booking'",
            "handler 'DictionaryHandler' of command 'DictionaryCommand' returns 'Dictionary<Guid, IReadOnlyList<BookingLine>>', which holds the entity 'BookingLine'",
            "handler 'LinesDtoHandler' of command 'LinesDtoCommand' returns 'LinesDto', whose property 'Lines' holds the entity 'BookingLine'",
            "handler 'NestedDtoHandler' of command 'NestedDtoCommand' returns 'SummaryDto', whose property 'Details.Booking' holds the entity 'Booking'",
            "handler 'DtoListHandler' of command 'DtoListCommand' returns 'IReadOnlyList<LinesDto>', whose property 'LinesDto.Lines' holds the entity 'BookingLine'",
            "handler 'FieldHandler' of command 'FieldCommand' returns 'FieldDto', whose field 'Line' holds the entity 'BookingLine'",
            "handler 'RootContractHandler' of command 'RootContractCommand' returns the entity 'IAggregateRoot'",
            "handler 'CollectionHandler' of command 'CollectionCommand' returns 'BookingList', which holds the entity 'Booking'"
        ]);
        Fixes(diagnostics).Distinct().ShouldBe(
        [
            "return 'BookingId', or a DTO built from the entity's values",
            "return 'Guid', or a DTO built from the entity's values",
            "return the identifier of 'IAggregateRoot', or a DTO built from the entity's values"
        ]);
    }

    // BookingId and BookingPeriod are value objects of the domain: followed, they hold values only.
    [Fact]
    public async Task Accepts_an_identifier_a_value_object_a_DTO_of_values_and_nothing()
    {
        await VerifyAsync(
            """
            using MyApp.Domain.Bookings;
            using MyApp.Shared.Messaging;

            namespace MyApp.Features.Bookings.Conforming;

            public sealed record IdCommand : ICommand<BookingId>;
            public sealed record CancelCommand : ICommand<bool>;
            public sealed record ValuesCommand : ICommand<BookingDto>;
            public sealed record NothingCommand : ICommand<Unit>;
            public sealed record PeriodCommand : ICommand<Result<BookingPeriod>>;
            public sealed record EventsCommand : ICommand<IReadOnlyList<BookingConfirmed>>;
            public sealed record HiddenCommand : ICommand<HiddenDto>;
            public sealed record GetBookingQuery : IQuery<Booking>;

            public sealed record BookingDto(BookingId Id, Guid RoomId, DateTimeOffset Start, IReadOnlyList<string> Lines);

            // Not detected: a static member, a non-public one and an entity typed object.
            public sealed class HiddenDto
            {
                public static Booking? Last { get; set; }

                internal Booking? Booking { get; set; }

                private readonly Booking? _booking = null;

                public object? Entity { get; set; }
            }

            internal sealed class IdHandler : IRequestHandler<IdCommand, BookingId> { public Task<BookingId> Handle(IdCommand c, CancellationToken t) => throw new NotImplementedException(); }
            internal sealed class CancelHandler : IRequestHandler<CancelCommand, bool> { public Task<bool> Handle(CancelCommand c, CancellationToken t) => throw new NotImplementedException(); }
            internal sealed class ValuesHandler : IRequestHandler<ValuesCommand, BookingDto> { public Task<BookingDto> Handle(ValuesCommand c, CancellationToken t) => throw new NotImplementedException(); }
            internal sealed class NothingHandler : IRequestHandler<NothingCommand, Unit> { public Task<Unit> Handle(NothingCommand c, CancellationToken t) => throw new NotImplementedException(); }
            internal sealed class PeriodHandler : IRequestHandler<PeriodCommand, Result<BookingPeriod>> { public Task<Result<BookingPeriod>> Handle(PeriodCommand c, CancellationToken t) => throw new NotImplementedException(); }
            internal sealed class EventsHandler : IRequestHandler<EventsCommand, IReadOnlyList<BookingConfirmed>> { public Task<IReadOnlyList<BookingConfirmed>> Handle(EventsCommand c, CancellationToken t) => throw new NotImplementedException(); }
            internal sealed class HiddenHandler : IRequestHandler<HiddenCommand, HiddenDto> { public Task<HiddenDto> Handle(HiddenCommand c, CancellationToken t) => throw new NotImplementedException(); }

            // A query handler is out of the rule.
            internal sealed class GetBookingHandler : IRequestHandler<GetBookingQuery, Booking> { public Task<Booking> Handle(GetBookingQuery q, CancellationToken t) => throw new NotImplementedException(); }

            // Not a handler: the type that returns the entity implements no requestHandler contract.
            internal sealed class BookingFactory { public Booking Create() => new(); }
            """);
    }

    [Fact]
    public async Task Follows_DTOs_that_reference_each_other_without_looping()
    {
        var diagnostics = await VerifyAsync(
            """
            using MyApp.Domain.Bookings;
            using MyApp.Shared.Messaging;

            namespace MyApp.Features.Bookings.Tree;

            public sealed record TreeCommand : ICommand<NodeDto>;
            public sealed record CycleCommand : ICommand<LeftDto>;

            public sealed class NodeDto
            {
                public NodeDto? Parent { get; init; }

                public List<NodeDto> Children { get; } = [];

                public string Name { get; init; } = "";
            }

            public sealed record LeftDto(RightDto? Right, string Name);
            public sealed record RightDto(LeftDto? Left, LeafDto Leaf);
            public sealed record LeafDto(Booking Booking);

            internal sealed class TreeHandler : IRequestHandler<TreeCommand, NodeDto> { public Task<NodeDto> Handle(TreeCommand c, CancellationToken t) => throw new NotImplementedException(); }
            internal sealed class [|CycleHandler|] : IRequestHandler<CycleCommand, LeftDto> { public Task<LeftDto> Handle(CycleCommand c, CancellationToken t) => throw new NotImplementedException(); }
            """);

        Subjects(diagnostics).ShouldBe(["handler 'CycleHandler' of command 'CycleCommand' returns 'LeftDto', whose property 'Right.Leaf.Booking' holds the entity 'Booking'"]);
    }

    // A handler inherits its contract from a base class; a generic handler is analyzable when its type parameters are
    // constrained to an entity or a command.
    [Fact]
    public async Task Reports_inherited_and_generic_handlers()
    {
        var diagnostics = await VerifyAsync(
            """
            using MyApp.Domain;
            using MyApp.Domain.Bookings;
            using MyApp.Shared.Messaging;

            namespace MyApp.Features.Bookings.Generic;

            public sealed record RestoreCommand : ICommand<Booking>;
            public sealed record CreateCommand<TEntity> : ICommand<TEntity> where TEntity : IEntity;
            public sealed record EchoCommand<T> : ICommand<T>;
            public sealed record PageCommand : ICommand<PageDto<BookingLine>>;
            public sealed record PageOfValuesCommand : ICommand<PageDto<BookingId>>;

            public sealed record PageDto<T>(IReadOnlyList<T> Items, int Total);

            internal abstract class EntityHandler<TCommand, TEntity> : IRequestHandler<TCommand, TEntity> where TCommand : ICommand<TEntity>
            {
                public abstract Task<TEntity> Handle(TCommand command, CancellationToken cancellationToken);
            }

            internal sealed class [|RestoreHandler|] : EntityHandler<RestoreCommand, Booking>
            {
                public override Task<Booking> Handle(RestoreCommand command, CancellationToken cancellationToken) => throw new NotImplementedException();
            }

            internal sealed class [|CreateHandler|]<TEntity> : IRequestHandler<CreateCommand<TEntity>, TEntity> where TEntity : IEntity
            {
                public Task<TEntity> Handle(CreateCommand<TEntity> command, CancellationToken cancellationToken) => throw new NotImplementedException();
            }

            // Not analyzable: TResponse is an unconstrained type parameter.
            internal sealed class EchoHandler<T> : IRequestHandler<EchoCommand<T>, T>
            {
                public Task<T> Handle(EchoCommand<T> command, CancellationToken cancellationToken) => throw new NotImplementedException();
            }

            internal sealed class [|LoggingHandler|]<TCommand> : IRequestHandler<TCommand, Booking> where TCommand : ICommand<Booking>
            {
                public Task<Booking> Handle(TCommand command, CancellationToken cancellationToken) => throw new NotImplementedException();
            }

            internal sealed class [|PageHandler|] : IRequestHandler<PageCommand, PageDto<BookingLine>>, IRequestHandler<PageOfValuesCommand, PageDto<BookingId>>
            {
                public Task<PageDto<BookingLine>> Handle(PageCommand command, CancellationToken cancellationToken) => throw new NotImplementedException();

                public Task<PageDto<BookingId>> Handle(PageOfValuesCommand command, CancellationToken cancellationToken) => throw new NotImplementedException();
            }
            """);

        Subjects(diagnostics).ShouldBe(
        [
            "handler 'RestoreHandler' of command 'RestoreCommand' returns the entity 'Booking'",
            "handler 'CreateHandler<TEntity>' of command 'CreateCommand<TEntity>' returns the entity 'TEntity'",
            "handler 'LoggingHandler<TCommand>' of command 'TCommand' returns the entity 'Booking'",
            "handler 'PageHandler' of command 'PageCommand' returns 'PageDto<BookingLine>', which holds the entity 'BookingLine'"
        ]);
        Fixes(diagnostics).ElementAt(1).ShouldBe("return the identifier of 'TEntity', or a DTO built from the entity's values");
    }

    // The domain compiled on its own, a DTO declared in a project that references it, both read from metadata.
    [Fact]
    public async Task Reads_the_domain_and_the_DTOs_of_other_assemblies_from_metadata()
    {
        var domain = CompileDomain();
        var domainReference = Reference(domain, asMetadata: true);
        var contracts = Compile(
            "MyApp.Contracts",
            [
                ("Dtos.cs",
                    """
                    using MyApp.Domain.Bookings;

                    namespace MyApp.Contracts;

                    public sealed record ConfirmationDto(Guid RoomId, Booking Booking);

                    public sealed record ReceiptDto(BookingId Id, string Room);
                    """),
                ("Usings.cs", ImplicitUsings)
            ],
            [domainReference]);

        var diagnostics = await VerifyAsync(
            [domainReference, Reference(contracts, asMetadata: true)],
            """
            using MyApp.Contracts;
            using MyApp.Shared.Messaging;

            namespace MyApp.Features.Bookings.ConfirmBooking;

            public sealed record ConfirmBookingCommand : ICommand<Result<ConfirmationDto>>;
            public sealed record ReceiptCommand : ICommand<ReceiptDto>;

            internal sealed class [|ConfirmBookingHandler|] : IRequestHandler<ConfirmBookingCommand, Result<ConfirmationDto>>
            {
                public Task<Result<ConfirmationDto>> Handle(ConfirmBookingCommand command, CancellationToken cancellationToken) => throw new NotImplementedException();
            }

            internal sealed class ReceiptHandler : IRequestHandler<ReceiptCommand, ReceiptDto>
            {
                public Task<ReceiptDto> Handle(ReceiptCommand command, CancellationToken cancellationToken) => throw new NotImplementedException();
            }
            """);

        Subjects(diagnostics).ShouldBe(
            ["handler 'ConfirmBookingHandler' of command 'ConfirmBookingCommand' returns 'Result<ConfirmationDto>', whose property 'ConfirmationDto.Booking' holds the entity 'Booking'"]);
    }

    // The contracts are invisible from the domain, and from an application without hardslice.json: HS900 reports the latter.
    [Fact]
    public async Task Does_nothing_without_the_contracts()
    {
        var source =
            """
            using MyApp.Domain.Bookings;
            using MyApp.Shared.Messaging;

            namespace MyApp.Features.Bookings.ConfirmBooking;

            public sealed record ConfirmBookingCommand : ICommand<Booking>;

            internal sealed class ConfirmBookingHandler : IRequestHandler<ConfirmBookingCommand, Booking>
            {
                public Task<Booking> Handle(ConfirmBookingCommand command, CancellationToken cancellationToken) => throw new NotImplementedException();
            }
            """;
        var application = Compile("MyApp", [("Handler.cs", source), ("Messaging.cs", Messaging), ("Usings.cs", ImplicitUsings)], [Reference(CompileDomain(), asMetadata: true)]);

        (await application.WithAnalyzers([new CommandResponseAnalyzer()], Options(configuration: null))
                .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken))
            .ShouldBeEmpty();
        (await CompileDomain().WithAnalyzers([new CommandResponseAnalyzer()], Options(Configuration))
                .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken))
            .ShouldBeEmpty();
    }

    // An auto-generated header does not exempt a handler from the rule.
    [Fact]
    public async Task Reports_generated_code()
    {
        await VerifyAsync(
            """
            // <auto-generated/>
            using MyApp.Domain.Bookings;
            using MyApp.Shared.Messaging;

            namespace MyApp.Features.Bookings.ConfirmBooking;

            public sealed record ConfirmBookingCommand : ICommand<Booking>;

            internal sealed class [|ConfirmBookingHandler|] : IRequestHandler<ConfirmBookingCommand, Booking>
            {
                public Task<Booking> Handle(ConfirmBookingCommand command, CancellationToken cancellationToken) => throw new NotImplementedException();
            }
            """);
    }

    [Fact]
    public async Task Does_not_apply_to_test_projects()
    {
        var compilation = Compile(
            "MyApp",
            [
                ("Handler.cs",
                    """
                    using MyApp.Domain.Bookings;
                    using MyApp.Shared.Messaging;

                    namespace MyApp.Tests;

                    public sealed record FakeCommand : ICommand<Booking>;

                    internal sealed class FakeHandler : IRequestHandler<FakeCommand, Booking>
                    {
                        public Task<Booking> Handle(FakeCommand command, CancellationToken cancellationToken) => Task.FromResult(new Booking());
                    }
                    """),
                ("Messaging.cs", Messaging),
                ("Usings.cs", ImplicitUsings)
            ],
            [Reference(CompileDomain(), asMetadata: true)]);

        var diagnostics = await compilation
            .WithAnalyzers([new CommandResponseAnalyzer()], Options(Configuration, isTestProject: true))
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        diagnostics.ShouldBeEmpty();
    }
}
