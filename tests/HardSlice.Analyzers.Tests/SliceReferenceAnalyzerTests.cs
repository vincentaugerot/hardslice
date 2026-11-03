using Microsoft.CodeAnalysis;
using static HardSlice.Analyzers.Tests.AnalyzerHarness;

namespace HardSlice.Analyzers.Tests;

public sealed class SliceReferenceAnalyzerTests
{
    private const string ConfirmBookingSlice =
        """
        namespace MyApp.Features.Bookings.ConfirmBooking;

        public sealed record ConfirmBookingCommand(Guid RoomId);

        public sealed class ConfirmBookingHandler
        {
            public const string Name = "confirm";

            public static readonly int Order = 1;

            public static ConfirmBookingHandler Create() => new();

            public int Handle(ConfirmBookingCommand command) => 0;

            public sealed class Result;
        }

        public class ConfirmBookingBase;

        public interface IConfirmBookingRule;

        public sealed class ConfirmBookingResult<T>;

        public struct ConfirmBookingValue;

        [AttributeUsage(AttributeTargets.All)]
        public sealed class ConfirmBookingMarkerAttribute : Attribute;

        public static class ConfirmBookingExtensions
        {
            public static bool IsConfirmable(this Guid roomId) => true;
        }

        public static class ConfirmBookingEndpoint
        {
            public static void Map() { }
        }

        public sealed class ConfirmBookingCollection
        {
            public ConfirmBookingHandler this[int index] => ConfirmBookingHandler.Create();

            public static ConfirmBookingCollection operator +(ConfirmBookingCollection left, ConfirmBookingCollection right) => left;
        }

        public sealed class ConfirmBookingToken
        {
            public static implicit operator ConfirmBookingToken(Guid value) => new();
        }
        """;

    private const string BookingsModule =
        """
        namespace MyApp.Features.Bookings;

        public interface IBookingStore;

        public delegate void BookingConfirmedHandler(MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingCommand command);

        public sealed class BookingEnvelope
        {
            public void Deconstruct(
                out MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingHandler handler,
                out int count)
            {
                handler = MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingHandler.Create();
                count = 1;
            }
        }

        public sealed class StoreEnvelope
        {
            public void Deconstruct(out IBookingStore store, out int count)
            {
                store = null!;
                count = 0;
            }
        }

        public sealed class ContractBox(MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingHandler value);

        public sealed class StoreBox(IBookingStore value);

        public sealed class ContractIndex
        {
            public object this[MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingHandler key] => key;
        }

        public sealed class StoreIndex
        {
            public object this[IBookingStore key] => key;
        }

        public static class BookingsModule
        {
            public static event BookingConfirmedHandler? Confirmed;

            public static event Action<IBookingStore>? StoreChanged;

            public static void Notify(MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingCommand command) { }

            public static void MapBookingsModule() => MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingEndpoint.Map();

            public static MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingHandler CreateHandler() =>
                MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingHandler.Create();

            public static Task<MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingHandler> CreateHandlerAsync() =>
                Task.FromResult(CreateHandler());

            public static MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingHandler CurrentHandler => CreateHandler();

            public static MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingCollection CreateCollection() => new();

            public static void AcceptHandler(MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingHandler handler) { }

            public static void AcceptCollection(MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingCollection collection) { }

            public static void AcceptToken(MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingToken token) { }

            public static bool TryGetHandler(out MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingHandler handler)
            {
                handler = CreateHandler();
                return true;
            }

            public static void AcceptStore(IBookingStore store) { }

            public static bool TryGetStore(out IBookingStore store)
            {
                store = null!;
                return false;
            }

            public static BookingEnvelope CreateEnvelope() => new();

            public static StoreEnvelope CreateStoreEnvelope() => new();

            public static (MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingHandler Handler, int Count) CreateHandlerTuple() =>
                (CreateHandler(), 1);
        }
        """;

    [Fact]
    public Task Reports_types_from_another_slice_in_declarations_and_signatures() => VerifyAsync(
        ConfirmBookingSlice,
        """
        using MyApp.Features.Bookings.ConfirmBooking;

        namespace MyApp.Features.Bookings.CancelBooking;

        internal sealed class CancelBookingHandler
        {
            private readonly [|ConfirmBookingHandler|]? _field = null;

            public [|ConfirmBookingCommand|]? Property { get; set; }

            public [|ConfirmBookingValue|]? Handle([|ConfirmBookingCommand|] command, [|ConfirmBookingHandler|][] handlers) => null;

            public void Local()
            {
                [|ConfirmBookingCommand|]? command = null;
                (int, [|ConfirmBookingValue|]) tuple = default;
            }

            public delegate [|ConfirmBookingCommand|] Factory();
        }

        internal delegate void CancelBookingCallback([|ConfirmBookingCommand|] command);
        """);

    // A documentation comment names a type without depending on it: only the code of the slice is checked.
    [Fact]
    public Task Ignores_types_from_another_slice_named_in_documentation_comments() => VerifyAsync(
        ConfirmBookingSlice,
        """
        using MyApp.Features.Bookings.ConfirmBooking;

        namespace MyApp.Features.Bookings.CancelBooking;

        /// <summary>Same as <see cref="MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingHandler.Handle(ConfirmBookingCommand)"/>,
        /// after <see cref="ConfirmBookingCommand"/> and <seealso cref="ConfirmBookingResult{T}"/>.</summary>
        internal sealed class CancelBookingHandler
        {
            /// <param name="command">A <see cref="ConfirmBookingHandler.Result"/>, never a <see cref="ConfirmBookingCommand"/>.</param>
            public int Handle(int command) => command + [|ConfirmBookingHandler|].Order;
        }
        """);

    [Fact]
    public Task Reports_types_from_another_slice_used_as_or_with_generics() => VerifyAsync(
        ConfirmBookingSlice,
        """
        using MyApp.Features.Bookings.ConfirmBooking;

        namespace MyApp.Features.Bookings.CancelBooking;

        internal sealed class CancelBookingHandler<T> where T : [|ConfirmBookingBase|]
        {
            public List<[|ConfirmBookingCommand|]> Commands { get; } = [];

            public [|ConfirmBookingResult|]<int>? Result { get; set; }

            public void Handle<TRule>() where TRule : [|IConfirmBookingRule|] => Process<[|ConfirmBookingCommand|]>();

            private static void Process<TCommand>() { }
        }
        """);

    [Fact]
    public Task Reports_inheritance_from_another_slice() => VerifyAsync(
        ConfirmBookingSlice,
        """
        using MyApp.Features.Bookings.ConfirmBooking;

        namespace MyApp.Features.Bookings.CancelBooking;

        internal sealed class CancelBookingHandler : [|ConfirmBookingBase|], [|IConfirmBookingRule|];

        internal interface ICancelBookingRule : [|IConfirmBookingRule|];
        """);

    [Fact]
    public Task Reports_attributes_from_another_slice() => VerifyAsync(
        ConfirmBookingSlice,
        """
        using MyApp.Features.Bookings.ConfirmBooking;

        namespace MyApp.Features.Bookings.CancelBooking;

        [[|ConfirmBookingMarker|]]
        internal sealed class CancelBookingHandler
        {
            [[|ConfirmBookingMarkerAttribute|]]
            public void Handle([[|ConfirmBookingMarker|]] Guid id) { }
        }
        """);

    [Fact]
    public Task Reports_construction_of_a_type_from_another_slice_once() => VerifyAsync(
        ConfirmBookingSlice,
        """
        using MyApp.Features.Bookings.ConfirmBooking;

        namespace MyApp.Features.Bookings.CancelBooking;

        internal sealed class CancelBookingHandler
        {
            public object Create()
            {
                var handler = new [|ConfirmBookingHandler|]();
                return new [|ConfirmBookingCommand|](Guid.NewGuid());
            }
        }
        """);

    [Fact]
    public Task Reports_static_members_of_another_slice_once_per_access() => VerifyAsync(
        ConfirmBookingSlice,
        """
        using MyApp.Features.Bookings.ConfirmBooking;

        namespace MyApp.Features.Bookings.CancelBooking;

        internal sealed class CancelBookingHandler
        {
            public string Handle()
            {
                [|ConfirmBookingEndpoint|].Map();
                var handler = [|ConfirmBookingHandler|].Create();
                return [|ConfirmBookingHandler|].Name + [|ConfirmBookingHandler|].Order;
            }
        }
        """);

    [Fact]
    public Task Reports_typeof_nameof_and_casts() => VerifyAsync(
        ConfirmBookingSlice,
        """
        using MyApp.Features.Bookings.ConfirmBooking;

        namespace MyApp.Features.Bookings.CancelBooking;

        internal sealed class CancelBookingHandler
        {
            public object?[] Handle(object value) =>
            [
                typeof([|ConfirmBookingHandler|]),
                nameof([|ConfirmBookingCommand|]),
                nameof([|ConfirmBookingHandler|].Handle),
                ([|ConfirmBookingCommand|])value,
                value as [|ConfirmBookingCommand|],
                value is [|ConfirmBookingCommand|],
                value is [|ConfirmBookingCommand|] command ? command : null
            ];
        }
        """);

    [Fact]
    public Task Reports_extension_methods_and_instance_members_reached_without_naming_their_type() => VerifyAsync(
        [],
        ConfirmBookingSlice,
        BookingsModule,
        """
        using MyApp.Features.Bookings.ConfirmBooking;

        namespace MyApp.Features.Bookings.CancelBooking;

        internal sealed class CancelBookingHandler
        {
            public bool Handle(Guid roomId)
            {
                var handler = BookingsModule.[|CreateHandler|]();
                return handler.[|Handle|](null!) == 0 && roomId.[|IsConfirmable|]();
            }
        }
        """);

    [Fact]
    public Task Reports_nested_types_once_and_names_the_nested_source() => VerifyAsync(
        ConfirmBookingSlice,
        """
        namespace MyApp.Features.Bookings.ConfirmBooking.Internal
        {
            public sealed class ConfirmBookingPolicy;
        }

        namespace MyApp.Features.Bookings.CancelBooking
        {
            using MyApp.Features.Bookings.ConfirmBooking;

            internal sealed class CancelBookingHandler
            {
                private sealed class Inner
                {
                    public [|ConfirmBookingHandler|].Result? Result { get; set; }

                    public MyApp.Features.Bookings.ConfirmBooking.Internal.[|ConfirmBookingPolicy|]? Policy { get; set; }

                    public global::MyApp.Features.Bookings.ConfirmBooking.[|ConfirmBookingHandler|].Result? Other { get; set; }
                }
            }
        }
        """);

    [Fact]
    public Task Reports_aliased_types_only_where_they_are_used() => VerifyAsync(
        ConfirmBookingSlice,
        """
        using Confirm = MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingHandler;

        namespace MyApp.Features.Bookings.CancelBooking;

        internal sealed class CancelBookingHandler
        {
            public object Handle() => [|Confirm|].Create();
        }
        """);

    [Fact]
    public Task Reports_composed_aliases_once_at_each_use() => VerifyAsync(
        ConfirmBookingSlice,
        """
        using GenericAlias = System.Collections.Generic.List<MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingCommand>;
        using ArrayAlias = MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingCommand[];
        using TupleAlias = (MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingCommand Command, int Count);
        using NestedAlias = System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingCommand?>>;

        namespace MyApp.Features.Bookings.CancelBooking;

        internal sealed class CancelBookingHandler(
            [|GenericAlias|] commands,
            [|ArrayAlias|] array,
            [|TupleAlias|] tuple,
            [|NestedAlias|] nested);
        """);

    [Fact]
    public async Task Reports_every_distinct_foreign_type_hidden_by_one_alias()
    {
        const string CapturePaymentSlice =
            """
            namespace MyApp.Features.Payments.CapturePayment;

            public sealed class CapturePaymentCommand;
            """;
        const string Consumer =
            """
            using MixedAlias = System.Collections.Generic.Dictionary<
                MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingCommand,
                MyApp.Features.Payments.CapturePayment.CapturePaymentCommand>;

            namespace MyApp.Features.Bookings.CancelBooking;

            internal sealed class CancelBookingHandler(MixedAlias value);
            """;
        var compilation = Compile(
            "MyApp",
            [
                ("Confirm.cs", ConfirmBookingSlice),
                ("Capture.cs", CapturePaymentSlice),
                ("Cancel.cs", Consumer),
                ("Usings.cs", ImplicitUsings)
            ]);

        var diagnostics = await AnalyzeAsync(compilation);

        diagnostics.Select(diagnostic => diagnostic.GetMessage()).ShouldBe(
        [
            Message("CancelBookingHandler", "Bookings/CancelBooking", "ConfirmBookingCommand", "Bookings/ConfirmBooking", "Bookings"),
            Message("CancelBookingHandler", "Bookings/CancelBooking", "CapturePaymentCommand", "Payments/CapturePayment", "Payments")
        ]);
        diagnostics.Select(diagnostic => diagnostic.Location.SourceSpan).Distinct().ShouldHaveSingleItem();
        var span = diagnostics[0].Location.SourceSpan;
        Consumer.Substring(span.Start, span.Length).ShouldBe("MixedAlias");
    }

    [Fact]
    public Task Reports_global_composed_alias_only_where_it_is_used() => VerifyAsync(
        ConfirmBookingSlice,
        """
        global using Commands = System.Collections.Generic.List<MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingCommand>;
        """,
        """
        namespace MyApp.Features.Bookings.CancelBooking;

        internal sealed class CancelBookingHandler([|Commands|] commands);
        """);

    [Fact]
    public Task Allows_unused_or_same_slice_composed_aliases() => VerifyAsync(
        ConfirmBookingSlice,
        """
        using Unused = System.Collections.Generic.List<MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingCommand>;
        using SameSlice = System.Collections.Generic.List<MyApp.Features.Bookings.CancelBooking.CancelBookingCommand>;

        namespace MyApp.Features.Bookings.CancelBooking;

        internal sealed record CancelBookingCommand;

        internal sealed class CancelBookingHandler(SameSlice commands);
        """);

    [Fact]
    public Task Reports_global_usings_only_where_they_are_used() => VerifyAsync(
        ConfirmBookingSlice,
        """
        global using MyApp.Features.Bookings.ConfirmBooking;
        global using static MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingEndpoint;
        """,
        """
        namespace MyApp.Features.Bookings.CancelBooking;

        internal sealed class CancelBookingHandler
        {
            public void Handle([|ConfirmBookingCommand|] command) => [|Map|]();
        }
        """);

    [Fact]
    public Task Reports_partial_classes_once_per_reference() => VerifyAsync(
        ConfirmBookingSlice,
        """
        using MyApp.Features.Bookings.ConfirmBooking;

        namespace MyApp.Features.Bookings.CancelBooking;

        internal sealed partial class CancelBookingHandler : [|ConfirmBookingBase|];
        """,
        """
        namespace MyApp.Features.Bookings.CancelBooking;

        internal sealed partial class CancelBookingHandler
        {
            public void Handle() { }
        }
        """);

    [Fact]
    public Task Reports_slice_types_exposed_by_the_module_contract() => VerifyAsync(
        ConfirmBookingSlice,
        BookingsModule,
        """
        namespace MyApp.Features.Bookings.CancelBooking;

        internal sealed class CancelBookingHandler
        {
            public async Task<object[]> Handle()
            {
                var first = BookingsModule.[|CreateHandler|]();
                var second = await BookingsModule.[|CreateHandlerAsync|]();
                var third = BookingsModule.[|CurrentHandler|];
                return [first, second, third];
            }
        }
        """);

    [Fact]
    public Task Reports_target_typed_construction_and_implicit_conversion() => VerifyAsync(
        ConfirmBookingSlice,
        BookingsModule,
        """
        namespace MyApp.Features.Bookings.CancelBooking;

        internal sealed class CancelBookingHandler
        {
            public void Handle(Guid roomId)
            {
                BookingsModule.AcceptHandler([|new|]());
                BookingsModule.AcceptToken([|roomId|]);
            }
        }
        """);

    [Fact]
    public Task Reports_foreign_types_passed_through_contract_arguments() => VerifyAsync(
        ConfirmBookingSlice,
        BookingsModule,
        """
        namespace MyApp.Features.Bookings.CancelBooking;

        internal sealed class CancelBookingHandler
        {
            public void Handle()
            {
                BookingsModule.TryGetHandler([|out var handler|]);
                BookingsModule.AcceptHandler([|handler|]);
            }
        }
        """);

    [Fact]
    public Task Allows_module_contract_arguments_that_do_not_expose_a_slice_type() => VerifyAsync(
        ConfirmBookingSlice,
        BookingsModule,
        """
        namespace MyApp.Features.Bookings.CancelBooking;

        internal sealed class CancelBookingHandler
        {
            public void Handle()
            {
                BookingsModule.TryGetStore(out var store);
                BookingsModule.AcceptStore(store);
            }
        }
        """);

    [Fact]
    public Task Reports_foreign_types_passed_to_contract_constructors_and_indexers() => VerifyAsync(
        ConfirmBookingSlice,
        BookingsModule,
        """
        namespace MyApp.Features.Bookings.CancelBooking;

        internal sealed class CancelBookingHandler
        {
            public object Handle()
            {
                _ = new ContractBox([|default!|]);
                return new ContractIndex()[[|default!|]];
            }
        }
        """);

    [Fact]
    public Task Allows_contract_constructors_and_indexers_with_contract_parameters() => VerifyAsync(
        ConfirmBookingSlice,
        BookingsModule,
        """
        namespace MyApp.Features.Bookings.CancelBooking;

        internal sealed class CancelBookingHandler
        {
            public object Handle()
            {
                _ = new StoreBox(default!);
                return new StoreIndex()[default!];
            }
        }
        """);

    [Fact]
    public Task Reports_contract_events_whose_delegate_exposes_a_slice_type() => VerifyAsync(
        ConfirmBookingSlice,
        BookingsModule,
        """
        namespace MyApp.Features.Bookings.CancelBooking;

        internal sealed class CancelBookingHandler
        {
            public void Handle() => BookingsModule.[|Confirmed|] += command => _ = command;
        }
        """);

    [Fact]
    public Task Reports_deconstruction_that_implicitly_exposes_a_slice_type() => VerifyAsync(
        ConfirmBookingSlice,
        BookingsModule,
        """
        namespace MyApp.Features.Bookings.CancelBooking;

        internal sealed class CancelBookingHandler
        {
            public object Handle()
            {
                var (handler, count) [|=|] BookingsModule.CreateEnvelope();
                return handler;
            }
        }
        """);

    [Fact]
    public Task Does_not_repeat_a_contract_result_diagnostic_on_direct_tuple_deconstruction() => VerifyAsync(
        ConfirmBookingSlice,
        BookingsModule,
        """
        namespace MyApp.Features.Bookings.CancelBooking;

        internal sealed class CancelBookingHandler
        {
            public object Handle()
            {
                var (handler, count) = BookingsModule.[|CreateHandlerTuple|]();
                return handler;
            }
        }
        """);

    [Fact]
    public Task Reports_contract_method_groups_whose_delegate_exposes_a_slice_type() => VerifyAsync(
        ConfirmBookingSlice,
        BookingsModule,
        """
        namespace MyApp.Features.Bookings.CancelBooking;

        internal sealed class CancelBookingHandler
        {
            public Delegate Handle() => BookingsModule.[|CreateHandler|];
        }
        """);

    [Fact]
    public Task Reports_an_explicit_delegate_alias_without_an_operation_duplicate() => VerifyAsync(
        ConfirmBookingSlice,
        BookingsModule,
        """
        using Handler = MyApp.Features.Bookings.BookingConfirmedHandler;

        namespace MyApp.Features.Bookings.CancelBooking;

        internal sealed class CancelBookingHandler
        {
            public object Handle() => ([|Handler|])BookingsModule.Notify;
        }
        """);

    [Fact]
    public Task Allows_nameof_and_implicit_contract_operations_with_only_contract_types() => VerifyAsync(
        ConfirmBookingSlice,
        BookingsModule,
        """
        namespace MyApp.Features.Bookings.CancelBooking;

        internal sealed class CancelBookingHandler
        {
            public object Handle()
            {
                BookingsModule.StoreChanged += store => _ = store;
                var (store, count) = BookingsModule.CreateStoreEnvelope();
                return new[]
                {
                    nameof(BookingsModule.CurrentHandler),
                    nameof(BookingsModule.Confirmed),
                    store?.ToString(),
                    count.ToString()
                };
            }
        }
        """);

    [Fact]
    public Task Reports_implicit_operator_and_indexer_without_duplicate_type_diagnostics() => VerifyAsync(
        ConfirmBookingSlice,
        BookingsModule,
        """
        namespace MyApp.Features.Bookings.CancelBooking;

        internal sealed class CancelBookingHandler
        {
            public object Handle()
            {
                var left = BookingsModule.[|CreateCollection|]();
                var right = BookingsModule.[|CreateCollection|]();
                var combined = left [|+|] right;
                return [|combined[0]|];
            }
        }
        """);

    [Fact]
    public Task Reports_references_between_slices_of_different_modules() => VerifyAsync(
        ConfirmBookingSlice,
        """
        namespace MyApp.Features.Orders.PlaceOrder;

        internal sealed class PlaceOrderHandler(MyApp.Features.Bookings.ConfirmBooking.[|ConfirmBookingHandler|] handler);
        """);

    [Fact]
    public Task Reports_generated_code_in_a_slice() => VerifyAsync(
        ConfirmBookingSlice,
        """
        // <auto-generated/>
        namespace MyApp.Features.Bookings.CancelBooking;

        internal sealed class CancelBookingHandler(MyApp.Features.Bookings.ConfirmBooking.[|ConfirmBookingHandler|] handler);
        """);

    [Fact]
    public async Task Names_the_source_and_target_symbols_and_slices()
    {
        var compilation = Compile(
            "MyApp",
            [
                ("Confirm.cs", ConfirmBookingSlice),
                ("Cancel.cs",
                    """
                    namespace MyApp.Features.Bookings.CancelBooking;

                    internal sealed class CancelBookingHandler
                    {
                        public void Handle() => MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingEndpoint.Map();

                        private sealed class Inner : MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingBase;
                    }
                    """),
                ("Usings.cs", ImplicitUsings)
            ]);

        var messages = (await AnalyzeAsync(compilation)).Select(diagnostic => diagnostic.GetMessage());

        messages.ShouldBe(
        [
            Message("CancelBookingHandler", "Bookings/CancelBooking", "ConfirmBookingEndpoint", "Bookings/ConfirmBooking", "Bookings"),
            Message("CancelBookingHandler.Inner", "Bookings/CancelBooking", "ConfirmBookingBase", "Bookings/ConfirmBooking", "Bookings")
        ], ignoreOrder: true);
    }

    private static string Message(string source, string sourceSlice, string target, string targetSlice, string targetModule) =>
        $"'{source}' in slice '{sourceSlice}' uses '{target}' from slice '{targetSlice}'. Slices never reference each other. "
        + $"Fix: move the shared rule into a domain aggregate, raise a domain event, or use the public contract of module '{targetModule}'. "
        + "Skill: extract-to-domain. Doc: 'docs/rules/HS016.md'.";

    [Fact]
    public void Reports_an_error_by_default()
    {
        var descriptor = new SliceReferenceAnalyzer().SupportedDiagnostics.ShouldHaveSingleItem();

        descriptor.Id.ShouldBe("HS016");
        descriptor.DefaultSeverity.ShouldBe(DiagnosticSeverity.Error);
        descriptor.IsEnabledByDefault.ShouldBeTrue();
    }

    [Fact]
    public Task Allows_references_inside_the_same_slice() => VerifyAsync(
        ConfirmBookingSlice,
        """
        namespace MyApp.Features.Bookings.ConfirmBooking.Validation
        {
            internal sealed class ConfirmBookingValidator : ConfirmBookingBase
            {
                public bool Validate(ConfirmBookingCommand command) => ConfirmBookingHandler.Create().Handle(command) == 0;
            }
        }
        """);

    [Fact]
    public Task Allows_references_to_the_module_public_contract() => VerifyAsync(
        ConfirmBookingSlice,
        BookingsModule,
        """
        namespace MyApp.Features.Bookings.CancelBooking;

        internal sealed class CancelBookingHandler(IBookingStore store)
        {
            public void Handle() => BookingsModule.MapBookingsModule();
        }
        """);

    [Fact]
    public Task Allows_references_to_the_domain()
    {
        var domain = Compile(
            "MyApp.Domain",
            [
                ("Booking.cs",
                    """
                    namespace MyApp.Domain.Bookings;

                    public sealed class Booking
                    {
                        public static Booking Confirm() => new();
                    }
                    """)
            ]);

        return VerifyAsync(
            [domain.ToMetadataReference()],
            """
            using MyApp.Domain.Bookings;

            namespace MyApp.Features.Bookings.CancelBooking;

            internal sealed class CancelBookingHandler
            {
                public Booking Handle() => Booking.Confirm();
            }
            """);
    }

    [Fact]
    public Task Allows_references_from_code_outside_slices() => VerifyAsync(
        ConfirmBookingSlice,
        BookingsModule,
        """
        namespace MyApp.Shared.Behaviors;

        internal sealed class Logging(MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingHandler handler);
        """);

    [Fact]
    public Task Identifies_slices_only_below_the_assembly_features_namespace() => VerifyAsync(
        """
        namespace Other.Features.Bookings.ConfirmBooking
        {
            public sealed class ConfirmBookingHandler;
        }

        namespace MyApp.Shared.Features.Bookings.ConfirmBooking
        {
            public sealed class ConfirmBookingPolicy;
        }

        namespace MyApp.Features.Bookings.CancelBooking
        {
            internal sealed class CancelBookingHandler(
                Other.Features.Bookings.ConfirmBooking.ConfirmBookingHandler handler,
                MyApp.Shared.Features.Bookings.ConfirmBooking.ConfirmBookingPolicy policy);
        }
        """);

    // dotnet new hardslice-api turns the name of the application into one safe namespace that names the assembly and prefixes
    // every namespace: Acme.Rooms, my-app as my_app, 1Rooms as _1Rooms. Slices are found below each of them.
    [Theory]
    [InlineData("Acme.Rooms")]
    [InlineData("my_app")]
    [InlineData("_1Rooms")]
    public Task Identifies_slices_below_the_features_namespace_of_any_application_name(string application) => VerifyAsync(
        new SliceReferenceAnalyzer(),
        SliceReferenceAnalyzer.DiagnosticId,
        application,
        configuration: null,
        [],
        OutputKind.DynamicallyLinkedLibrary,
        $$"""
        namespace {{application}}.Features.Bookings.ConfirmBooking;

        public sealed class ConfirmBookingHandler;
        """,
        $$"""
        namespace {{application}}.Features.Bookings.CancelBooking;

        internal sealed class CancelBookingHandler({{application}}.Features.Bookings.ConfirmBooking.[|ConfirmBookingHandler|] handler);
        """);
}
