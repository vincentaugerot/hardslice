using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using static HardSlice.Analyzers.Tests.AnalyzerHarness;

namespace HardSlice.Analyzers.Tests;

// HS010: the domain and the slices never read the system clock.
public sealed class SystemClockAnalyzerTests
{
    private const string Rule = "The domain and the slices never read the system clock.";

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

    private const string DomainContracts =
        """
        namespace MyApp.Domain
        {
            public interface IEntity;

            public interface IAggregateRoot : IEntity;

            public interface IDomainEvent;
        }
        """;

    private const string Booking =
        """
        namespace MyApp.Domain.Bookings;

        public sealed class Booking : IAggregateRoot
        {
            private Booking(DateTimeOffset confirmedAt) => ConfirmedAt = confirmedAt;

            public DateTimeOffset ConfirmedAt { get; }

            public static Booking Confirm() => new(DateTimeOffset.[|UtcNow|]);

            public static Booking Confirm(DateTimeOffset now) => new(now);
        }
        """;

    // The application: its slices are checked, without hardslice.json, which they do not need.
    private static Task<ImmutableArray<Diagnostic>> VerifyAsync(params string[] markedSources) =>
        VerifyAsync([], markedSources);

    private static Task<ImmutableArray<Diagnostic>> VerifyAsync(MetadataReference[] references, params string[] markedSources) =>
        AnalyzerHarness.VerifyAsync(
            new SystemClockAnalyzer(), SystemClockAnalyzer.DiagnosticId, "MyApp", Configuration, references, OutputKind.DynamicallyLinkedLibrary, markedSources);

    // The domain, compiled on its own with its contracts and the hardslice.json of the template.
    private static Task<ImmutableArray<Diagnostic>> VerifyDomainAsync(string? configuration, params string[] markedSources) =>
        AnalyzerHarness.VerifyAsync(
            new SystemClockAnalyzer(),
            SystemClockAnalyzer.DiagnosticId,
            "MyApp.Domain",
            configuration,
            [],
            OutputKind.DynamicallyLinkedLibrary,
            [.. markedSources, DomainContracts]);

    // The first sentence of each message, without its rule and its fix.
    private static IEnumerable<string> Subjects(ImmutableArray<Diagnostic> diagnostics) =>
        diagnostics.Select(diagnostic => diagnostic.GetMessage().Split(". ")[0]);

    private static IEnumerable<string> Fixes(ImmutableArray<Diagnostic> diagnostics) =>
        diagnostics.Select(diagnostic => diagnostic.GetMessage().Split(" Fix: ")[1].Split(". Doc: ")[0]);

    [Fact]
    public async Task Reports_a_handler_that_reads_DateTimeOffset_UtcNow()
    {
        var diagnostics = await VerifyAsync(
            """
            namespace MyApp.Features.Bookings.ConfirmBooking;

            internal sealed class ConfirmBookingHandler
            {
                public DateTimeOffset Handle() => DateTimeOffset.[|UtcNow|];
            }
            """);

        diagnostics.ShouldHaveSingleItem().GetMessage().ShouldBe(
            "'ConfirmBookingHandler.Handle' in slice 'Bookings/ConfirmBooking' reads 'DateTimeOffset.UtcNow'. "
            + "The domain and the slices never read the system clock. "
            + "Fix: inject TimeProvider and call GetUtcNow(), or pass the instant to the domain as a parameter. Doc: 'docs/rules/HS010.md'.");
    }

    [Fact]
    public async Task Reports_the_five_clock_properties_with_the_TimeProvider_call_that_replaces_each()
    {
        var diagnostics = await VerifyAsync(
            """
            namespace MyApp.Features.Bookings.ListBookings;

            internal sealed class ListBookingsHandler
            {
                public object[] Handle() =>
                [
                    DateTime.[|Now|],
                    DateTime.[|UtcNow|],
                    DateTime.[|Today|],
                    DateTimeOffset.[|Now|],
                    DateTimeOffset.[|UtcNow|],
                    System.DateTimeOffset.[|UtcNow|].AddDays(1)
                ];
            }
            """);

        diagnostics.Select(diagnostic => diagnostic.GetMessage().Split(" reads ")[1].Split(". ")[0]).ShouldBe(
        [
            "'DateTime.Now'",
            "'DateTime.UtcNow'",
            "'DateTime.Today'",
            "'DateTimeOffset.Now'",
            "'DateTimeOffset.UtcNow'",
            "'DateTimeOffset.UtcNow'"
        ]);
        Fixes(diagnostics).ShouldBe(
        [
            "inject TimeProvider and call GetLocalNow().DateTime, or pass the instant to the domain as a parameter",
            "inject TimeProvider and call GetUtcNow().UtcDateTime, or pass the instant to the domain as a parameter",
            "inject TimeProvider and call GetLocalNow().Date, or pass the instant to the domain as a parameter",
            "inject TimeProvider and call GetLocalNow(), or pass the instant to the domain as a parameter",
            "inject TimeProvider and call GetUtcNow(), or pass the instant to the domain as a parameter",
            "inject TimeProvider and call GetUtcNow(), or pass the instant to the domain as a parameter"
        ]);
        diagnostics.ShouldAllBe(diagnostic => diagnostic.GetMessage().Contains(Rule));
    }

    [Fact]
    public async Task Reports_a_static_using_a_filter_of_an_expression_tree_and_a_generic_method_of_a_slice()
    {
        var diagnostics = await VerifyAsync(
            """
            using System.Linq.Expressions;
            using static System.DateTime;

            namespace MyApp.Features.Bookings.CancelBooking
            {
                internal sealed class CancelBookingHandler
                {
                    private readonly DateTime _createdAt = [|Now|];

                    public IQueryable<DateTimeOffset> Expired(IQueryable<DateTimeOffset> ends) =>
                        ends.Where(end => end < DateTimeOffset.[|UtcNow|]);

                    public Expression<Func<DateTimeOffset, bool>> IsPast { get; } = end => end < DateTimeOffset.[|UtcNow|];

                    public IEnumerable<DateTimeOffset> Expired(IEnumerable<DateTimeOffset> ends) =>
                        ends.Where(end => end < DateTimeOffset.[|UtcNow|]);

                    public T Stamp<T>(Func<DateTime, T> stamp) where T : notnull => stamp([|UtcNow|]);

                    private sealed class Clock
                    {
                        public DateTime Date => [|Today|];
                    }
                }
            }

            namespace MyApp.Features.Bookings.CancelBooking.Policies
            {
                internal static class CancellationPolicy
                {
                    public static bool IsLate(DateTimeOffset start) => start < DateTimeOffset.[|Now|].AddHours(24);
                }
            }
            """);

        Subjects(diagnostics).ShouldBe(
        [
            "'CancelBookingHandler._createdAt' in slice 'Bookings/CancelBooking' reads 'DateTime.Now'",
            "'CancelBookingHandler.Expired' in slice 'Bookings/CancelBooking' reads 'DateTimeOffset.UtcNow'",
            "'CancelBookingHandler.IsPast' in slice 'Bookings/CancelBooking' reads 'DateTimeOffset.UtcNow'",
            "'CancelBookingHandler.Expired' in slice 'Bookings/CancelBooking' reads 'DateTimeOffset.UtcNow'",
            "'CancelBookingHandler.Stamp<T>' in slice 'Bookings/CancelBooking' reads 'DateTime.UtcNow'",
            "'CancelBookingHandler.Clock.Date' in slice 'Bookings/CancelBooking' reads 'DateTime.Today'",
            "'CancellationPolicy.IsLate' in slice 'Bookings/CancelBooking' reads 'DateTimeOffset.Now'"
        ]);
        Fixes(diagnostics).ElementAt(1).ShouldBe(
            "read the instant once before the query, from an injected TimeProvider with GetUtcNow(), and use that variable in the expression");
        Fixes(diagnostics).ElementAt(2).ShouldBe(
            "read the instant once before the query, from an injected TimeProvider with GetUtcNow(), and use that variable in the expression");
        Fixes(diagnostics).ElementAt(3).ShouldBe("inject TimeProvider and call GetUtcNow(), or pass the instant to the domain as a parameter");
    }

    [Fact]
    public async Task Accepts_TimeProvider_an_instant_in_parameter_Shared_the_module_and_Program()
    {
        await VerifyAsync(
            """
            namespace MyApp.Features.Bookings.ConfirmBooking
            {
                internal sealed class ConfirmBookingHandler(TimeProvider timeProvider)
                {
                    public DateTimeOffset Handle() => timeProvider.GetUtcNow();

                    public DateTimeOffset Fallback() => TimeProvider.System.GetUtcNow();

                    public string Describe(DateTimeOffset now) =>
                        $"{nameof(DateTime.Now)} {nameof(DateTimeOffset.UtcNow)} {DateTime.MinValue} {DateTimeOffset.UnixEpoch} {now.LocalDateTime} {DateTime.Parse("2026-10-30")}";

                    public DateTime Lookalike() => MyApp.Shared.Clock.UtcNow;
                }
            }

            namespace MyApp.Features.Bookings
            {
                public static class BookingsModule
                {
                    public static DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
                }
            }

            namespace MyApp.Shared.Behaviors
            {
                internal sealed class LoggingBehavior
                {
                    public DateTime Started() => DateTime.UtcNow;
                }
            }

            namespace MyApp.Shared
            {
                public static class Clock
                {
                    public static DateTime UtcNow => DateTime.UtcNow;
                }
            }

            namespace MyApp.Infrastructure
            {
                internal static class Startup
                {
                    public static DateTime Now() => DateTime.Now;
                }
            }
            """);
    }

    [Fact]
    public async Task Does_not_report_the_top_level_statements_of_Program()
    {
        var compilation = Compile(
            "MyApp",
            [("Program.cs", "Console.WriteLine(DateTimeOffset.UtcNow);"), ("Usings.cs", ImplicitUsings)],
            outputKind: OutputKind.ConsoleApplication);

        var diagnostics = await compilation
            .WithAnalyzers([new SystemClockAnalyzer()], Options(Configuration))
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Reports_the_domain_in_its_own_compilation()
    {
        var diagnostics = await VerifyDomainAsync(
            Configuration,
            Booking,
            """
            namespace MyApp.Domain.Bookings.Events
            {
                public sealed record BookingConfirmed(Guid BookingId) : IDomainEvent
                {
                    public DateTime OccurredAt { get; } = DateTime.[|UtcNow|];
                }
            }

            namespace MyApp.Domain.Rooms
            {
                public static class Availability<TRoom>
                {
                    public static bool IsOpen(TRoom room) => DateTime.[|Today|].DayOfWeek != DayOfWeek.Sunday;

                    public static Func<DateTimeOffset, bool> IsPast { get; } = end => end < DateTimeOffset.[|Now|];
                }
            }
            """);

        Subjects(diagnostics).ShouldBe(
        [
            "'Booking.Confirm' in domain project 'MyApp.Domain' reads 'DateTimeOffset.UtcNow'",
            "'BookingConfirmed.OccurredAt' in domain project 'MyApp.Domain' reads 'DateTime.UtcNow'",
            "'Availability<TRoom>.IsOpen' in domain project 'MyApp.Domain' reads 'DateTime.Today'",
            "'Availability<TRoom>.IsPast' in domain project 'MyApp.Domain' reads 'DateTimeOffset.Now'"
        ]);
        diagnostics[0].GetMessage().ShouldBe(
            "'Booking.Confirm' in domain project 'MyApp.Domain' reads 'DateTimeOffset.UtcNow'. "
            + "The domain and the slices never read the system clock. "
            + "Fix: take the instant as a parameter; the handler reads it from an injected TimeProvider with GetUtcNow(). Doc: 'docs/rules/HS010.md'.");
    }

    // Without a usable entity contract, the domain cannot be recognized: HS900 reports the configuration instead.
    [Fact]
    public async Task Does_not_recognize_the_domain_without_its_entity_contract()
    {
        await VerifyDomainAsync(configuration: null, Booking.Replace("[|", "").Replace("|]", ""));
        await VerifyDomainAsync(
            Configuration.Replace("\"MyApp.Domain.IEntity, MyApp.Domain\"", "\"MyApp.Domain.Entity, MyApp.Domain\""),
            Booking.Replace("[|", "").Replace("|]", ""));

        var domain = Compile(
            "MyApp.Domain",
            [("Booking.cs", Booking.Replace("[|", "").Replace("|]", "")), ("Contracts.cs", DomainContracts), ("Usings.cs", ImplicitUsings)]);
        var diagnostic = (await AnalyzeAllAsync(domain, configuration: null)).ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(ClassificationAnalyzer.ConfigurationId);
        diagnostic.GetMessage().ShouldContain("classify the types checked by HS004, HS005, HS006, HS007, HS008, HS010, HS901 and HS902.");
    }

    // The domain is checked in its own compilation only: the application that references it reads its symbols, not its code.
    [Fact]
    public async Task Checks_the_slices_of_an_application_that_references_the_domain_and_not_the_rest()
    {
        var domain = Compile(
            "MyApp.Domain",
            [("Booking.cs", Booking.Replace("[|", "").Replace("|]", "")), ("Contracts.cs", DomainContracts), ("Usings.cs", ImplicitUsings)]);

        var diagnostics = await VerifyAsync(
            [Reference(domain, asMetadata: true)],
            """
            using MyApp.Domain.Bookings;

            namespace MyApp.Features.Bookings.ConfirmBooking
            {
                internal sealed class ConfirmBookingHandler
                {
                    public Booking Handle() => Booking.Confirm(DateTimeOffset.[|UtcNow|]);

                    public Booking HandleWithDomainClock() => Booking.Confirm();
                }
            }

            namespace MyApp.Features.Bookings
            {
                internal static class BookingsSeed
                {
                    public static Booking First() => Booking.Confirm(DateTimeOffset.UtcNow);
                }
            }
            """);

        Subjects(diagnostics).ShouldBe(["'ConfirmBookingHandler.Handle' in slice 'Bookings/ConfirmBooking' reads 'DateTimeOffset.UtcNow'"]);
    }

    // An auto-generated header does not exempt code from the rule.
    [Fact]
    public async Task Reports_generated_code()
    {
        await VerifyAsync(
            """
            // <auto-generated/>
            namespace MyApp.Features.Bookings.GetBooking;

            internal static class GeneratedClock
            {
                public static DateTime Read() => DateTime.[|UtcNow|];
            }
            """);
    }

    [Fact]
    public async Task Does_not_apply_to_test_projects()
    {
        var compilation = Compile(
            "MyApp.Tests",
            [
                ("Tests.cs",
                    """
                    namespace MyApp.Tests.Features.Bookings.ConfirmBooking;

                    public sealed class ConfirmBookingTests
                    {
                        public DateTimeOffset Now() => DateTimeOffset.UtcNow;
                    }
                    """),
                ("Usings.cs", ImplicitUsings)
            ]);

        var diagnostics = await compilation
            .WithAnalyzers([new SystemClockAnalyzer()], Options(Configuration, isTestProject: true))
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        diagnostics.ShouldBeEmpty();
    }
}
