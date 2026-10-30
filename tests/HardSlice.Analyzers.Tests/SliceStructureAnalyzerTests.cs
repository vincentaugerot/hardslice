using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using static HardSlice.Analyzers.Tests.AnalyzerHarness;

namespace HardSlice.Analyzers.Tests;

// HS004: a slice holds one request named after it, its handler and its endpoint, in the folder of its namespace.
public sealed class SliceStructureAnalyzerTests
{
    private const string StructureRule = "A slice holds one request named after it, its handler and its endpoint.";

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
            "requestHandler": "MyApp.Shared.Messaging.IRequestHandler`2, MyApp"
          },
          "shared": {
            "allowedFolders": ["Messaging", "Persistence", "Behaviors"]
          }
        }
        """;

    private static readonly (string Path, string Source) Messaging =
        ("Shared/Messaging/Requests.cs",
            """
            namespace MyApp.Shared.Messaging;

            public interface IRequest<TResponse>;

            public interface ICommand<TResponse> : IRequest<TResponse>;

            public interface IQuery<TResponse> : IRequest<TResponse>;

            public interface IRequestHandler<in TRequest, TResponse> where TRequest : IRequest<TResponse>
            {
                Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
            }
            """);

    // ArchiveBooking, a complete command slice: each test replaces one part of it.
    private const string ArchiveBookingCommand = "public sealed record ArchiveBookingCommand(Guid BookingId) : ICommand<bool>;";

    private const string ArchiveBookingHandler =
        """
        internal sealed class ArchiveBookingHandler : IRequestHandler<ArchiveBookingCommand, bool>
        {
            public Task<bool> Handle(ArchiveBookingCommand command, CancellationToken cancellationToken) => Task.FromResult(true);
        }
        """;

    private const string ArchiveBookingValidator =
        """
        internal sealed class ArchiveBookingValidator : AbstractValidator<ArchiveBookingCommand>
        {
            public ArchiveBookingValidator() => RuleFor(command => command.BookingId).NotEmpty();
        }
        """;

    private const string ArchiveBookingEndpoint =
        """
        internal static class ArchiveBookingEndpoint
        {
            public static void Map(RouteGroupBuilder group) => group.MapPost("/{id:guid}/archive", (Guid id) => TypedResults.NoContent());
        }
        """;

    private static readonly MetadataReference[] References = [.. PackageReferences("FluentValidation")];

    private static Task<ImmutableArray<Diagnostic>> VerifyAsync(params (string Path, string MarkedSource)[] files) =>
        VerifyAsync(Configuration, [], files);

    private static Task<ImmutableArray<Diagnostic>> VerifyAsync(
        string? configuration,
        MetadataReference[] references,
        params (string Path, string MarkedSource)[] files) =>
        AnalyzerHarness.VerifyAsync(
            new SliceStructureAnalyzer(),
            SliceStructureAnalyzer.DiagnosticId,
            "MyApp",
            configuration,
            [.. References, .. references],
            OutputKind.DynamicallyLinkedLibrary,
            [.. files, Messaging]);

    // The ArchiveBooking slice in its own file, each part given or left out.
    private static (string Path, string Source) ArchiveBooking(
        string command = ArchiveBookingCommand,
        string handler = ArchiveBookingHandler,
        string validator = ArchiveBookingValidator,
        string endpoint = ArchiveBookingEndpoint,
        string path = "Features/Bookings/ArchiveBooking/ArchiveBooking.cs") =>
        (path,
            $$"""
            using FluentValidation;
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Routing;
            using Microsoft.AspNetCore.Builder;
            using MyApp.Shared.Messaging;

            namespace MyApp.Features.Bookings.ArchiveBooking;

            {{command}}

            {{validator}}

            {{handler}}

            {{endpoint}}
            """);

    private static IEnumerable<string> Subjects(ImmutableArray<Diagnostic> diagnostics) =>
        diagnostics.Select(diagnostic => diagnostic.GetMessage().Split(". ")[0]);

    private static IEnumerable<string> Fixes(ImmutableArray<Diagnostic> diagnostics) =>
        diagnostics.Select(diagnostic => diagnostic.GetMessage().Split(" Fix: ")[1].Split(". Doc: ")[0]);

    [Fact]
    public async Task Reports_nothing_on_complete_slices_and_their_module()
    {
        await VerifyAsync(
            ArchiveBooking(),
            ("Features/Bookings/GetBooking/GetBooking.cs",
                """
                using Microsoft.AspNetCore.Http;
                using Microsoft.AspNetCore.Routing;
                using Microsoft.AspNetCore.Builder;
                using MyApp.Shared.Messaging;

                namespace MyApp.Features.Bookings.GetBooking;

                public sealed record GetBookingQuery(Guid BookingId) : IQuery<BookingDto?>;

                public sealed record BookingDto(Guid Id, string Status);

                internal sealed class GetBookingHandler : IRequestHandler<GetBookingQuery, BookingDto?>
                {
                    public Task<BookingDto?> Handle(GetBookingQuery query, CancellationToken cancellationToken) => Task.FromResult<BookingDto?>(null);
                }

                internal static class GetBookingEndpoint
                {
                    public static void Map(RouteGroupBuilder group) => group.MapGet("/{id:guid}", (Guid id) => TypedResults.Ok());
                }
                """),
            ("Features/Bookings/GetBooking/Dtos/BookingLine.cs", "namespace MyApp.Features.Bookings.GetBooking.Dtos; public sealed record BookingLine(Guid Id);"),
            ("Features/Bookings/BookingsModule.cs",
                """
                using Microsoft.AspNetCore.Builder;
                using Microsoft.AspNetCore.Routing;

                namespace MyApp.Features.Bookings;

                public static class BookingsModule
                {
                    public static void MapBookingsModule(IEndpointRouteBuilder endpoints)
                    {
                        var group = endpoints.MapGroup("/bookings");
                        MyApp.Features.Bookings.ArchiveBooking.ArchiveBookingEndpoint.Map(group);
                        MyApp.Features.Bookings.GetBooking.GetBookingEndpoint.Map(group);
                    }
                }
                """),
            ("Features/Bookings/BookingSummary.cs", "namespace MyApp.Features.Bookings; public sealed record BookingSummary(Guid Id);"),
            ("Shared/Behaviors/LoggingBehavior.cs", "namespace MyApp.Shared.Behaviors; internal sealed class LoggingBehavior;"),
            ("Program.cs", "namespace MyApp; public static class Program;"));
    }

    // The blank template: Shared and Program, no slice.
    [Fact]
    public async Task Reports_nothing_on_an_application_without_slices()
    {
        await VerifyAsync(("Program.cs", "namespace MyApp; public static class Program;"));
    }

    [Fact]
    public async Task Reports_a_request_that_is_not_named_after_its_slice()
    {
        var diagnostic = (await VerifyAsync(ArchiveBooking(
                command: "public sealed record [|ArchiveCommand|](Guid BookingId) : ICommand<bool>;",
                handler: ArchiveBookingHandler.Replace("ArchiveBookingCommand", "ArchiveCommand"),
                validator: ArchiveBookingValidator.Replace("<ArchiveBookingCommand>", "<ArchiveCommand>"))))
            .ShouldHaveSingleItem();

        diagnostic.GetMessage().ShouldBe(
            "slice 'Bookings/ArchiveBooking' declares 'ArchiveCommand'. "
            + "A slice holds one request named after it, its handler and its endpoint. "
            + "Fix: rename the command to 'ArchiveBookingCommand'. Doc: 'docs/rules/HS004.md'.");
    }

    [Fact]
    public async Task Reports_a_query_that_is_not_named_after_its_slice()
    {
        var diagnostics = await VerifyAsync(ArchiveBooking(
            command: "public sealed record [|ArchivedBookingsQuery|](Guid BookingId) : IQuery<bool>;",
            handler: ArchiveBookingHandler.Replace("ArchiveBookingCommand", "ArchivedBookingsQuery"),
            validator: ""));

        Fixes(diagnostics).ShouldBe(["rename the query to 'ArchiveBookingQuery'"]);
    }

    [Fact]
    public async Task Reports_a_slice_without_request_on_its_namespace()
    {
        var diagnostics = await VerifyAsync(
            ("Features/Bookings/ArchiveBooking/ArchiveBooking.cs",
                """
                namespace MyApp.Features.Bookings.[|ArchiveBooking|];

                internal static class ArchiveBookingEndpoint;
                """));

        diagnostics.ShouldHaveSingleItem().GetMessage().ShouldBe(
            "slice 'Bookings/ArchiveBooking' declares no request. "
            + StructureRule + " "
            + "Fix: declare the command 'ArchiveBookingCommand', which implements 'ICommand<TResponse>', "
            + "or the query 'ArchiveBookingQuery', which implements 'IQuery<TResponse>'; "
            + "types the slices of the module share belong to namespace 'MyApp.Features.Bookings'. Doc: 'docs/rules/HS004.md'.");
    }

    [Fact]
    public async Task Names_the_contract_a_lookalike_request_misses()
    {
        var diagnostics = await VerifyAsync(
            ("Features/Bookings/ArchiveBooking/ArchiveBooking.cs",
                """
                namespace MyApp.Features.Bookings.[|ArchiveBooking|];

                public sealed record ArchiveBookingCommand(Guid BookingId);

                internal static class ArchiveBookingEndpoint;
                """));

        Fixes(diagnostics).ShouldBe(["implement 'ICommand<TResponse>' on 'ArchiveBookingCommand'"]);
    }

    [Fact]
    public async Task Reports_a_second_request_in_a_slice()
    {
        var diagnostics = await VerifyAsync(ArchiveBooking(
            command: ArchiveBookingCommand + "\n\npublic sealed record [|RestoreBookingCommand|](Guid BookingId) : ICommand<bool>;\n\n"
                + "public sealed record [|ArchiveBookingQuery|](Guid BookingId) : IQuery<bool>;",
            handler: ArchiveBookingHandler
                + "\n\ninternal sealed class RestoreBookingHandler : IRequestHandler<RestoreBookingCommand, bool>\n"
                + "{\n    public Task<bool> Handle(RestoreBookingCommand command, CancellationToken cancellationToken) => Task.FromResult(true);\n}\n"
                + "\ninternal sealed class ArchiveBookingQueryHandler : IRequestHandler<ArchiveBookingQuery, bool>\n"
                + "{\n    public Task<bool> Handle(ArchiveBookingQuery query, CancellationToken cancellationToken) => Task.FromResult(true);\n}"));

        Subjects(diagnostics).ShouldBe(
        [
            "slice 'Bookings/ArchiveBooking' declares a second request, 'RestoreBookingCommand', besides 'ArchiveBookingCommand'",
            "slice 'Bookings/ArchiveBooking' declares a second request, 'ArchiveBookingQuery', besides 'ArchiveBookingCommand'"
        ]);
        Fixes(diagnostics).ShouldBe(
        [
            "move 'RestoreBookingCommand' into its own slice 'Features/Bookings/RestoreBooking/' with its handler and its endpoint",
            "move 'ArchiveBookingQuery' into its own slice 'Features/Bookings/<Slice>/', named after its use case, with its handler and its endpoint"
        ]);
    }

    [Fact]
    public async Task Reports_a_handler_that_is_not_named_after_its_slice()
    {
        var diagnostics = await VerifyAsync(ArchiveBooking(handler: ArchiveBookingHandler.Replace(
            "internal sealed class ArchiveBookingHandler",
            "internal sealed class [|ArchiveHandler|]")));

        diagnostics.ShouldHaveSingleItem().GetMessage().ShouldBe(
            "slice 'Bookings/ArchiveBooking' declares 'ArchiveHandler'. " + StructureRule + " "
            + "Fix: rename the handler to 'ArchiveBookingHandler'. Doc: 'docs/rules/HS004.md'.");
    }

    [Fact]
    public async Task Reports_a_slice_whose_request_has_no_handler()
    {
        var diagnostics = await VerifyAsync(ArchiveBooking(
            command: "public sealed record [|ArchiveBookingCommand|](Guid BookingId) : ICommand<bool>;",
            handler: ""));

        diagnostics.ShouldHaveSingleItem().GetMessage().ShouldBe(
            "slice 'Bookings/ArchiveBooking' declares no handler for 'ArchiveBookingCommand'. " + StructureRule + " "
            + "Fix: declare 'internal sealed class ArchiveBookingHandler : IRequestHandler<ArchiveBookingCommand, bool>' in the slice. "
            + "Doc: 'docs/rules/HS004.md'.");
    }

    [Fact]
    public async Task Reports_a_slice_without_endpoint()
    {
        var diagnostics = await VerifyAsync(ArchiveBooking(
            command: "public sealed record [|ArchiveBookingCommand|](Guid BookingId) : ICommand<bool>;",
            endpoint: ""));

        diagnostics.ShouldHaveSingleItem().GetMessage().ShouldBe(
            "slice 'Bookings/ArchiveBooking' declares no endpoint 'ArchiveBookingEndpoint'. " + StructureRule + " "
            + "Fix: declare the internal static class 'ArchiveBookingEndpoint' in the slice: its Map(RouteGroupBuilder group) method maps "
            + "the endpoint, and 'BookingsModule' calls it. Doc: 'docs/rules/HS004.md'.");
    }

    [Fact]
    public async Task Reports_an_endpoint_that_is_misnamed_or_not_static()
    {
        var misnamed = await VerifyAsync(ArchiveBooking(
            command: "public sealed record [|ArchiveBookingCommand|](Guid BookingId) : ICommand<bool>;",
            endpoint: "internal sealed class ArchiveEndpoint;"));
        var notStatic = await VerifyAsync(ArchiveBooking(endpoint: "internal sealed class [|ArchiveBookingEndpoint|];"));

        Fixes(misnamed).ShouldBe(["rename 'ArchiveEndpoint' to 'ArchiveBookingEndpoint' and declare it static"]);
        notStatic.ShouldHaveSingleItem().GetMessage().ShouldBe(
            "endpoint 'ArchiveBookingEndpoint' of slice 'Bookings/ArchiveBooking' is not static. " + StructureRule + " "
            + "Fix: declare 'ArchiveBookingEndpoint' as a static class: its Map(RouteGroupBuilder group) method maps the endpoint, "
            + "and 'BookingsModule' calls it. Doc: 'docs/rules/HS004.md'.");
    }

    [Fact]
    public async Task Reports_a_validator_that_is_not_named_after_its_slice()
    {
        var diagnostics = await VerifyAsync(ArchiveBooking(validator: ArchiveBookingValidator
            .Replace("internal sealed class ArchiveBookingValidator", "internal sealed class [|ArchiveRules|]")
            .Replace("public ArchiveBookingValidator()", "public ArchiveRules()")));

        diagnostics.ShouldHaveSingleItem().GetMessage().ShouldBe(
            "slice 'Bookings/ArchiveBooking' declares 'ArchiveRules'. " + StructureRule + " "
            + "Fix: rename the validator to 'ArchiveBookingValidator'. Doc: 'docs/rules/HS004.md'.");
    }

    // A validator of a type that is no request, such as a DTO of the command, is not the validator of the slice.
    [Fact]
    public async Task Does_not_name_a_validator_of_a_type_that_is_no_request()
    {
        await VerifyAsync(ArchiveBooking(validator: ArchiveBookingValidator
            + "\n\npublic sealed record ArchiveReason(string Text);\n\n"
            + "internal sealed class ArchiveReasonValidator : AbstractValidator<ArchiveReason>;"));
    }

    [Fact]
    public async Task Reports_a_validator_at_the_root_of_its_module()
    {
        var diagnostics = await VerifyAsync(
            ArchiveBooking(validator: ""),
            ("Features/Bookings/ArchiveBookingValidator.cs",
                """
                using FluentValidation;
                using MyApp.Features.Bookings.ArchiveBooking;

                namespace MyApp.Features.Bookings;

                internal sealed class [|ArchiveBookingValidator|] : AbstractValidator<ArchiveBookingCommand>;
                """));

        diagnostics.ShouldHaveSingleItem().GetMessage().ShouldBe(
            "validator 'ArchiveBookingValidator' of 'ArchiveBookingCommand' is declared in module 'Bookings', outside any slice. "
            + "A request, its handler and its validator belong to the slice of their use case. "
            + "Fix: move 'ArchiveBookingValidator' into slice 'Bookings/ArchiveBooking': folder 'Features/Bookings/ArchiveBooking/', "
            + "namespace 'MyApp.Features.Bookings.ArchiveBooking'. Doc: 'docs/rules/HS004.md'.");
    }

    [Fact]
    public async Task Reports_requests_and_handlers_outside_any_slice()
    {
        var diagnostics = await VerifyAsync(
            ("Features/Bookings/ArchiveBookingCommand.cs",
                """
                using MyApp.Shared.Messaging;

                namespace MyApp.Features.Bookings;

                public sealed record [|ArchiveBookingCommand|](Guid BookingId) : ICommand<bool>;

                internal sealed class [|ArchiveHandler|] : IRequestHandler<ArchiveBookingCommand, bool>
                {
                    public Task<bool> Handle(ArchiveBookingCommand command, CancellationToken cancellationToken) => Task.FromResult(true);
                }
                """),
            ("Shared/Messaging/Ping.cs",
                """
                namespace MyApp.Shared.Messaging;

                public sealed record [|Ping|] : IQuery<string>;

                internal sealed class [|PingHandler|] : IRequestHandler<Ping, string>
                {
                    public Task<string> Handle(Ping query, CancellationToken cancellationToken) => Task.FromResult("pong");
                }
                """),
            ("Features/ListModules.cs",
                """
                namespace MyApp.Features;

                public sealed record [|ListModulesQuery|] : MyApp.Shared.Messaging.IQuery<string[]>;
                """),
            ("Health.cs", "public sealed record [|HealthQuery|] : MyApp.Shared.Messaging.IQuery<bool>;"));

        Subjects(diagnostics).ShouldBe(
        [
            "command 'ArchiveBookingCommand' is declared in module 'Bookings', outside any slice",
            "handler 'ArchiveHandler' of 'ArchiveBookingCommand' is declared in module 'Bookings', outside any slice",
            "query 'ListModulesQuery' is declared in namespace 'MyApp.Features', outside any slice",
            "query 'HealthQuery' is declared in the global namespace, outside any slice",
            "query 'Ping' is declared in namespace 'MyApp.Shared.Messaging', outside any slice",
            "handler 'PingHandler' of 'Ping' is declared in namespace 'MyApp.Shared.Messaging', outside any slice"
        ]);
        Fixes(diagnostics).ShouldBe(
        [
            "move 'ArchiveBookingCommand' into slice 'Bookings/ArchiveBooking' with its handler and its endpoint: "
                + "folder 'Features/Bookings/ArchiveBooking/', namespace 'MyApp.Features.Bookings.ArchiveBooking'",
            "move 'ArchiveHandler' into slice 'Bookings/ArchiveBooking', renamed 'ArchiveBookingHandler': "
                + "folder 'Features/Bookings/ArchiveBooking/', namespace 'MyApp.Features.Bookings.ArchiveBooking'",
            "move 'ListModulesQuery' into slice '<Module>/ListModules' with its handler and its endpoint: "
                + "folder 'Features/<Module>/ListModules/', namespace 'MyApp.Features.<Module>.ListModules'",
            "move 'HealthQuery' into slice '<Module>/Health' with its handler and its endpoint: "
                + "folder 'Features/<Module>/Health/', namespace 'MyApp.Features.<Module>.Health'",
            "move 'Ping' into slice '<Module>/<Slice>', renamed '<Slice>Query', with its handler and its endpoint: "
                + "folder 'Features/<Module>/<Slice>/', namespace 'MyApp.Features.<Module>.<Slice>'",
            "move 'PingHandler' into slice '<Module>/<Slice>', renamed '<Slice>Handler': "
                + "folder 'Features/<Module>/<Slice>/', namespace 'MyApp.Features.<Module>.<Slice>'"
        ]);
    }

    // Moved with HS016: a handler of another slice is reported there, and its request has a handler.
    [Fact]
    public async Task Leaves_a_handler_in_another_slice_to_HS016()
    {
        await VerifyAsync(
            ArchiveBooking(handler: ""),
            ("Features/Bookings/RestoreBooking/RestoreBooking.cs",
                """
                using MyApp.Features.Bookings.ArchiveBooking;
                using MyApp.Shared.Messaging;

                namespace MyApp.Features.Bookings.RestoreBooking;

                public sealed record RestoreBookingCommand(Guid BookingId) : ICommand<bool>;

                internal sealed class RestoreBookingHandler : IRequestHandler<RestoreBookingCommand, bool>
                {
                    public Task<bool> Handle(RestoreBookingCommand command, CancellationToken cancellationToken) => Task.FromResult(true);
                }

                internal sealed class ArchiveHandler : IRequestHandler<ArchiveBookingCommand, bool>
                {
                    public Task<bool> Handle(ArchiveBookingCommand command, CancellationToken cancellationToken) => Task.FromResult(true);
                }

                internal static class RestoreBookingEndpoint;
                """));
    }

    [Fact]
    public async Task Reports_a_file_of_a_slice_outside_its_folder()
    {
        var diagnostics = await VerifyAsync(ArchiveBooking(
            command: "public sealed record [|ArchiveBookingCommand|](Guid BookingId) : ICommand<bool>;",
            handler: ArchiveBookingHandler.Replace("internal sealed class ArchiveBookingHandler", "internal sealed class [|ArchiveBookingHandler|]"),
            validator: ArchiveBookingValidator.Replace("internal sealed class ArchiveBookingValidator", "internal sealed class [|ArchiveBookingValidator|]"),
            endpoint: ArchiveBookingEndpoint.Replace("internal static class ArchiveBookingEndpoint", "internal static class [|ArchiveBookingEndpoint|]"),
            path: "Features/Bookings/ArchiveBooking.cs"));

        Subjects(diagnostics).First().ShouldBe(
            "'ArchiveBookingCommand' of slice 'Bookings/ArchiveBooking' is declared in 'Features/Bookings/ArchiveBooking.cs', outside the folder of its slice");
        diagnostics[0].GetMessage().ShouldBe(
            "'ArchiveBookingCommand' of slice 'Bookings/ArchiveBooking' is declared in 'Features/Bookings/ArchiveBooking.cs', outside the folder of its slice. "
            + "The folder and the namespace of a slice hold the same types. "
            + "Fix: move the declaration into a file under 'Features/Bookings/ArchiveBooking/'. Doc: 'docs/rules/HS004.md'.");
    }

    [Fact]
    public async Task Reports_a_type_of_the_folder_of_a_slice_declared_in_another_namespace()
    {
        var diagnostics = await VerifyAsync(
            ArchiveBooking(),
            ("Features/Bookings/ArchiveBooking/ArchivePolicy.cs", "namespace MyApp.Features.Bookings; internal static class [|ArchivePolicy|];"),
            ("Features/Bookings/ArchiveBooking/Clock.cs", "namespace MyApp.Shared; internal static class [|Clock|];"),
            ("Features/Bookings/ArchiveBooking/Global.cs", "internal static class [|ArchiveGlobals|];"));

        diagnostics.Select(diagnostic => diagnostic.GetMessage()).ShouldBe(
        [
            "'ArchivePolicy' is declared in 'Features/Bookings/ArchiveBooking/ArchivePolicy.cs', in the folder of slice 'Bookings/ArchiveBooking', "
                + "but in namespace 'MyApp.Features.Bookings'. The folder and the namespace of a slice hold the same types. "
                + "Fix: declare it in namespace 'MyApp.Features.Bookings.ArchiveBooking' if it belongs to the slice, "
                + "or move its file to 'Features/Bookings/'. Doc: 'docs/rules/HS004.md'.",
            "'Clock' is declared in 'Features/Bookings/ArchiveBooking/Clock.cs', in the folder of slice 'Bookings/ArchiveBooking', "
                + "but in namespace 'MyApp.Shared'. The folder and the namespace of a slice hold the same types. "
                + "Fix: declare it in namespace 'MyApp.Features.Bookings.ArchiveBooking' if it belongs to the slice, "
                + "or move its file to 'Shared/'. Doc: 'docs/rules/HS004.md'.",
            "'ArchiveGlobals' is declared in 'Features/Bookings/ArchiveBooking/Global.cs', in the folder of slice 'Bookings/ArchiveBooking', "
                + "but in the global namespace. The folder and the namespace of a slice hold the same types. "
                + "Fix: declare it in namespace 'MyApp.Features.Bookings.ArchiveBooking' if it belongs to the slice, "
                + "or move its file out of the folder of the slice. Doc: 'docs/rules/HS004.md'."
        ], ignoreOrder: true);
    }

    // A type of a slice declared in the folder of another slice is reported once, as a type outside the folder of its own
    // slice. RestoreBooking, which holds nothing else, also misses its request and its endpoint.
    [Fact]
    public async Task Reports_a_type_declared_in_the_folder_of_another_slice_once()
    {
        var diagnostics = await VerifyAsync(
            ArchiveBooking(),
            ("Features/Bookings/ArchiveBooking/Restore.cs",
                "namespace MyApp.Features.Bookings.[|[|RestoreBooking|]|]; internal static class [|RestorePolicy|];"));

        Subjects(diagnostics).ShouldBe(
        [
            "slice 'Bookings/RestoreBooking' declares no request",
            "slice 'Bookings/RestoreBooking' declares no endpoint 'RestoreBookingEndpoint'",
            "'RestorePolicy' of slice 'Bookings/RestoreBooking' is declared in 'Features/Bookings/ArchiveBooking/Restore.cs', outside the folder of its slice"
        ], ignoreOrder: true);
    }

    [Fact]
    public async Task Checks_partial_types_declaration_by_declaration()
    {
        var diagnostics = await VerifyAsync(
            ArchiveBooking(handler: ArchiveBookingHandler.Replace("internal sealed class", "internal sealed partial class")),
            ("Features/Bookings/ArchiveBookingHandler.Logging.cs",
                """
                namespace MyApp.Features.Bookings.ArchiveBooking;

                internal sealed partial class [|ArchiveBookingHandler|]
                {
                    private int _archived;
                }
                """));

        Subjects(diagnostics).ShouldBe(
            ["'ArchiveBookingHandler' of slice 'Bookings/ArchiveBooking' is declared in 'Features/Bookings/ArchiveBookingHandler.Logging.cs', outside the folder of its slice"]);
    }

    // Code generated by a source generator or marked auto-generated is neither checked nor counted against its slice.
    [Fact]
    public async Task Does_not_check_generated_code()
    {
        await VerifyAsync(
            ArchiveBooking(handler: ArchiveBookingHandler.Replace("internal sealed class", "internal sealed partial class")),
            ("obj/Debug/net10.0/generated/ArchiveBookingHandler.Logging.g.cs",
                """
                namespace MyApp.Features.Bookings.ArchiveBooking;

                internal sealed partial class ArchiveBookingHandler;
                """),
            ("Generated/Requests.cs",
                """
                // <auto-generated/>
                namespace MyApp.Features.Bookings
                {
                    public sealed record GeneratedQuery : MyApp.Shared.Messaging.IQuery<int>;
                }

                namespace MyApp.Features.Bookings.ArchiveBooking
                {
                    public sealed record ArchiveCommand : MyApp.Shared.Messaging.ICommand<int>;
                }
                """));
    }

    // A slice declared in several files is checked once.
    [Fact]
    public async Task Reports_a_slice_declared_in_several_files_once()
    {
        var diagnostics = await VerifyAsync(
            ("Features/Bookings/ArchiveBooking/ArchiveBookingCommand.cs",
                "namespace MyApp.Features.Bookings.ArchiveBooking; public sealed record [|ArchiveBookingCommand|](Guid BookingId) : MyApp.Shared.Messaging.ICommand<bool>;"),
            ("Features/Bookings/ArchiveBooking/ArchiveBookingHandler.cs",
                """
                namespace MyApp.Features.Bookings.ArchiveBooking;

                internal sealed class ArchiveBookingHandler : MyApp.Shared.Messaging.IRequestHandler<ArchiveBookingCommand, bool>
                {
                    public Task<bool> Handle(ArchiveBookingCommand command, CancellationToken cancellationToken) => Task.FromResult(true);
                }
                """),
            ("Features/Bookings/ArchiveBooking/Policies/ArchivePolicy.cs", "namespace MyApp.Features.Bookings.ArchiveBooking.Policies; internal static class ArchivePolicy;"));

        Subjects(diagnostics).ShouldBe(["slice 'Bookings/ArchiveBooking' declares no endpoint 'ArchiveBookingEndpoint'"]);
    }

    [Fact]
    public async Task Names_a_generic_request_without_its_arity_and_recognizes_a_handler_with_a_generic_base()
    {
        await VerifyAsync(
            ("Features/Bookings/ArchiveBooking/ArchiveBooking.cs",
                """
                using FluentValidation;
                using MyApp.Shared.Messaging;

                namespace MyApp.Features.Bookings.ArchiveBooking;

                public sealed record ArchiveBookingCommand<TReason>(Guid BookingId, TReason Reason) : ICommand<bool>;

                internal sealed class ArchiveBookingValidator<TReason> : AbstractValidator<ArchiveBookingCommand<TReason>>;

                internal sealed class ArchiveBookingHandler<TReason> : CommandHandler<ArchiveBookingCommand<TReason>, bool>
                {
                    public override Task<bool> Handle(ArchiveBookingCommand<TReason> command, CancellationToken cancellationToken) => Task.FromResult(true);
                }

                internal static class ArchiveBookingEndpoint;
                """),
            ("Shared/Messaging/CommandHandler.cs",
                """
                namespace MyApp.Shared.Messaging;

                public abstract class CommandHandler<TCommand, TResponse> : IRequestHandler<TCommand, TResponse> where TCommand : ICommand<TResponse>
                {
                    public abstract Task<TResponse> Handle(TCommand command, CancellationToken cancellationToken);
                }
                """));

        var diagnostics = await VerifyAsync(
            ("Features/Bookings/ArchiveBooking/ArchiveBooking.cs",
                """
                using MyApp.Shared.Messaging;

                namespace MyApp.Features.Bookings.ArchiveBooking;

                public sealed record [|ArchiveCommand|]<TReason>(Guid BookingId, TReason Reason) : ICommand<bool>;

                internal sealed class ArchiveBookingHandler<TReason> : IRequestHandler<ArchiveCommand<TReason>, bool>
                {
                    public Task<bool> Handle(ArchiveCommand<TReason> command, CancellationToken cancellationToken) => Task.FromResult(true);
                }

                internal static class ArchiveBookingEndpoint;
                """));
        Fixes(diagnostics).ShouldBe(["rename the command to 'ArchiveBookingCommand'"]);
    }

    // Contracts declared by a referenced assembly, compiled on its own and read as metadata.
    [Fact]
    public async Task Resolves_the_messaging_contracts_in_metadata()
    {
        var messaging = Compile("MyApp.Messaging", [(Messaging.Path, Messaging.Source.Replace("namespace MyApp.Shared.Messaging;", "namespace MyApp.Messaging;")), ("Usings.cs", ImplicitUsings)]);
        var configuration = Configuration
            .Replace("MyApp.Shared.Messaging.ICommand`1, MyApp\"", "MyApp.Messaging.ICommand`1, MyApp.Messaging\"")
            .Replace("MyApp.Shared.Messaging.IQuery`1, MyApp\"", "MyApp.Messaging.IQuery`1, MyApp.Messaging\"")
            .Replace("MyApp.Shared.Messaging.IRequestHandler`2, MyApp\"", "MyApp.Messaging.IRequestHandler`2, MyApp.Messaging\"");

        var diagnostics = await AnalyzerHarness.VerifyAsync(
            new SliceStructureAnalyzer(),
            SliceStructureAnalyzer.DiagnosticId,
            "MyApp",
            configuration,
            [.. References, Reference(messaging, asMetadata: true)],
            OutputKind.DynamicallyLinkedLibrary,
            ("Features/Bookings/ArchiveBooking/ArchiveBooking.cs",
                """
                using MyApp.Messaging;

                namespace MyApp.Features.Bookings.ArchiveBooking;

                public sealed record ArchiveBookingCommand(Guid BookingId) : ICommand<bool>;

                internal sealed class [|ArchiveHandler|] : IRequestHandler<ArchiveBookingCommand, bool>
                {
                    public Task<bool> Handle(ArchiveBookingCommand command, CancellationToken cancellationToken) => Task.FromResult(true);
                }

                internal static class ArchiveBookingEndpoint;
                """));

        Fixes(diagnostics).ShouldBe(["rename the handler to 'ArchiveBookingHandler'"]);
    }

    // Without hardslice.json, requests and handlers cannot be recognized: HS900 reports it, and only the endpoint and
    // the folders are checked.
    [Fact]
    public async Task Checks_the_endpoint_and_the_folders_only_without_the_messaging_contracts()
    {
        var diagnostics = await VerifyAsync(
            configuration: null,
            [],
            ArchiveBooking(
                command: "public sealed record [|ArchiveCommand|](Guid BookingId) : ICommand<bool>;",
                handler: "",
                validator: "",
                endpoint: "internal sealed class [|[|ArchiveBookingEndpoint|]|];",
                path: "Features/Bookings/ArchiveBooking.cs"));

        Subjects(diagnostics).ShouldBe(
        [
            "'ArchiveCommand' of slice 'Bookings/ArchiveBooking' is declared in 'Features/Bookings/ArchiveBooking.cs', outside the folder of its slice",
            "endpoint 'ArchiveBookingEndpoint' of slice 'Bookings/ArchiveBooking' is not static",
            "'ArchiveBookingEndpoint' of slice 'Bookings/ArchiveBooking' is declared in 'Features/Bookings/ArchiveBooking.cs', outside the folder of its slice"
        ], ignoreOrder: true);
    }

    [Fact]
    public async Task Does_not_apply_to_test_projects()
    {
        var compilation = Compile(
            "MyApp.Tests",
            [
                ("Features/Bookings/ArchiveBooking/ArchiveBookingTests.cs",
                    "namespace MyApp.Tests.Features.Bookings.ArchiveBooking; public sealed class ArchiveBookingTests;"),
                ("Usings.cs", ImplicitUsings)
            ]);

        var diagnostics = await compilation
            .WithAnalyzers([new SliceStructureAnalyzer()], Options(Configuration, isTestProject: true))
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        diagnostics.ShouldBeEmpty();
    }
}
