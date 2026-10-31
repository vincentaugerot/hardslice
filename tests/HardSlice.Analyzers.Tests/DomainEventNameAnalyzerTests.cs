using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using static HardSlice.Analyzers.Tests.AnalyzerHarness;

namespace HardSlice.Analyzers.Tests;

// HS019: a domain event is named as a past fact. Off by default: each test turns it on as the All mode does, as a suggestion.
public sealed class DomainEventNameAnalyzerTests
{
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

    private const string Contracts =
        """
        namespace MyApp.Domain
        {
            public interface IEntity;

            public interface IAggregateRoot : IEntity;

            public interface IDomainEvent;
        }
        """;

    private static CSharpCompilation CompileDomain(params (string Path, string Source)[] files) =>
        Compile("MyApp.Domain", [.. files, ("Contracts.cs", Contracts), ("Usings.cs", ImplicitUsings)]);

    // A domain event per name, in its own file.
    private static CSharpCompilation CompileEvents(params string[] names) =>
        CompileDomain(names.Select(name => ($"{name}.cs", $"namespace MyApp.Domain.Events; public sealed record {name}(Guid Id) : IDomainEvent;")).ToArray());

    private static Compilation WithHS019(Compilation compilation, ReportDiagnostic severity = ReportDiagnostic.Info) =>
        compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(
            compilation.Options.SpecificDiagnosticOptions.SetItem(DomainEventNameAnalyzer.DiagnosticId, severity)));

    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(Compilation compilation, string? configuration = Configuration) =>
        WithHS019(compilation)
            .WithAnalyzers([new DomainEventNameAnalyzer()], Options(configuration))
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

    // The name each diagnostic reports, in source order.
    private static async Task<string[]> ReportedAsync(Compilation compilation) =>
        (await AnalyzeAsync(compilation))
            .OrderBy(diagnostic => diagnostic.Location.SourceTree!.FilePath, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Location.SourceSpan.Start)
            .Select(diagnostic => diagnostic.Location.SourceTree!.GetText(TestContext.Current.CancellationToken).ToString(diagnostic.Location.SourceSpan))
            .ToArray();

    [Fact]
    public async Task Suggests_a_past_fact_for_an_event_named_as_a_command()
    {
        var diagnostic = (await AnalyzeAsync(CompileEvents("ConfirmBooking"))).ShouldHaveSingleItem();

        diagnostic.Id.ShouldBe(DomainEventNameAnalyzer.DiagnosticId);
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Info);
        diagnostic.GetMessage().ShouldBe(
            "domain event 'ConfirmBooking' is not named as a past fact. Domain events are named in the past tense. "
            + "Fix: rename it, for example to 'BookingConfirmed'. Skill: add-domain-event. Doc: 'docs/rules/HS019.md'.");
        diagnostic.Location.SourceTree!.GetText(TestContext.Current.CancellationToken).ToString(diagnostic.Location.SourceSpan).ShouldBe("ConfirmBooking");
    }

    [Theory]
    [InlineData("ConfirmBooking")]
    [InlineData("BookingConfirmation")]
    [InlineData("BookingCancel")]
    [InlineData("PayInvoice")]
    [InlineData("RoomCleaning")]
    [InlineData("BookingConfirmedSnapshot")]
    [InlineData("BookingEvent")]
    [InlineData("BookingV2")]
    public async Task Reports_a_name_whose_last_word_is_not_a_past_participle(string name)
    {
        (await ReportedAsync(CompileEvents(name))).ShouldBe([name]);
    }

    // The last word ends with 'ed', or is an irregular past participle of the list of the sheet, whatever its case. A version
    // suffix and trailing underscores are not words. 'RoomSeed' and 'MediaEmbed' pass: the rule reads the form, not the meaning.
    [Theory]
    [InlineData("BookingConfirmed")]
    [InlineData("BookingCancelled")]
    [InlineData("BookingPaid")]
    [InlineData("InvoiceSent")]
    [InlineData("ReportWritten")]
    [InlineData("RoomBuilt")]
    [InlineData("SeatHeld")]
    [InlineData("TicketSold")]
    [InlineData("SeatTaken")]
    [InlineData("MoneyWithdrawn")]
    [InlineData("PasswordReset")]
    [InlineData("RefundPAID")]
    [InlineData("HTTPRequestRECEIVED")]
    [InlineData("BookingConfirmedV2")]
    [InlineData("BookingConfirmed2")]
    [InlineData("Booking_Paid")]
    [InlineData("BookingConfirmed_")]
    [InlineData("Paid")]
    [InlineData("RoomSeed")]
    [InlineData("MediaEmbed")]
    public async Task Accepts_a_name_whose_last_word_is_a_past_participle(string name)
    {
        (await ReportedAsync(CompileEvents(name))).ShouldBeEmpty();
    }

    // The sheet publishes the list of irregular past participles: every one of them is accepted.
    [Fact]
    public async Task Accepts_every_irregular_past_participle_the_sheet_lists()
    {
        const string Prefix = "- or be one of these irregular past participles, whatever its case: ";
        var sheet = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "TemplateDocs", "rules", "HS019.md"));
        var participles = sheet.Single(line => line.StartsWith(Prefix, StringComparison.Ordinal))[Prefix.Length..].TrimEnd('.').Split(", ");
        participles.Length.ShouldBeGreaterThan(100);
        participles.ShouldContain("Paid");

        (await ReportedAsync(CompileEvents([.. participles.Select(participle => $"Booking{participle}")]))).ShouldBeEmpty();
        (await ReportedAsync(CompileEvents("BookingForsaken"))).ShouldBe(["BookingForsaken"]);
    }

    // dotnet new hardslice-event warns about the names this rule reports, and only about them: the pattern of its warning follows
    // the rule on the PascalCase names the template accepts, irregular participles in any case, versions and acronyms included.
    [Fact]
    public async Task The_warning_of_dotnet_new_hardslice_event_follows_the_rule()
    {
        const string Prefix = "- or be one of these irregular past participles, whatever its case: ";
        var sheet = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "TemplateDocs", "rules", "HS019.md"));
        var participles = sheet.Single(line => line.StartsWith(Prefix, StringComparison.Ordinal))[Prefix.Length..].TrimEnd('.').Split(", ");
        using var template = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "EventTemplate", "template.json")));
        var symbols = template.RootElement.GetProperty("symbols");
        var pascalCase = new Regex(symbols.GetProperty("NameIsPascalCase").GetProperty("parameters").GetProperty("pattern").GetString()!);
        var pastFact = new Regex(symbols.GetProperty("NameIsPastFact").GetProperty("parameters").GetProperty("pattern").GetString()!);
        string[] names =
        [
            "RoomRenamed", "RenameRoom", "RoomRenaming", "BookingConfirmedV2", "BookingConfirmed2", "BookingConfirmedv12", "BookingV2",
            "Booking2", "BookedV", "BookingEd", "BookingED", "BookeD", "BOOKED", "RefundPAID", "ROOMPAID", "HTTPPaid", "HTTPRequestRECEIVED",
            "Room2Paid", "Thread", "AlreadyRead", "Bookingpaid", "RoomSeed", "MediaEmbed", "Paid", "Ed", "BookingForsaken", "BookingEvent",
            .. participles.Select(participle => $"Booking{participle}"),
            .. participles.Select(participle => $"Booking{participle.ToUpperInvariant()}V3"),
            .. participles.Select(participle => $"Room{participle.ToLowerInvariant()}")
        ];
        names.ShouldAllBe(name => pascalCase.IsMatch(name));

        var reported = await ReportedAsync(CompileEvents(names));

        names.Where(name => pastFact.IsMatch(name) == reported.Contains(name))
            .ShouldBeEmpty("The warning of dotnet new hardslice-event disagrees with HS019 on these names.");
    }

    [Fact]
    public async Task Checks_classes_structs_generic_and_nested_events()
    {
        var domain = CompileDomain(
            ("Events.cs",
                """
                namespace MyApp.Domain.Events;

                public sealed class ConfirmBooking : IDomainEvent;

                public readonly record struct PayBooking(Guid BookingId) : IDomainEvent;

                public sealed record EntityChange<TEntity>(TEntity Entity) : IDomainEvent where TEntity : IEntity;

                public sealed record EntityChanged<TEntity>(TEntity Entity) : IDomainEvent where TEntity : IEntity;

                public static class BookingEvents
                {
                    public sealed record Confirmation(Guid BookingId) : IDomainEvent;

                    public sealed record Confirmed(Guid BookingId) : IDomainEvent;
                }
                """));

        var diagnostics = await AnalyzeAsync(domain);

        diagnostics.Select(diagnostic => diagnostic.GetMessage().Split(" is not named")[0]).Order(StringComparer.Ordinal).ShouldBe(
        [
            "domain event 'BookingEvents.Confirmation'",
            "domain event 'ConfirmBooking'",
            "domain event 'EntityChange<TEntity>'",
            "domain event 'PayBooking'"
        ]);
    }

    // An abstract event and an event interface name a family of events, not a fact; another type is not an event.
    [Fact]
    public async Task Does_not_check_abstract_events_event_interfaces_and_other_types()
    {
        var domain = CompileDomain(
            ("Events.cs",
                """
                namespace MyApp.Domain.Bookings;

                public interface IBookingEvent : IDomainEvent;

                public abstract record BookingEvent(Guid BookingId) : IBookingEvent;

                public sealed record BookingConfirmed(Guid BookingId) : BookingEvent(BookingId);

                public sealed record ConfirmBookingRequest(Guid BookingId);

                public sealed class Booking : IAggregateRoot;
                """));

        (await ReportedAsync(domain)).ShouldBeEmpty();
    }

    // HS901 reports a type that is both an entity and a domain event.
    [Fact]
    public async Task Leaves_a_type_that_is_also_an_entity_to_HS901()
    {
        var domain = CompileDomain(("BookingSnapshot.cs", "namespace MyApp.Domain.Bookings; public sealed record BookingSnapshot : IAggregateRoot, IDomainEvent;"));

        (await ReportedAsync(domain)).ShouldBeEmpty();
    }

    // Off by default, as in the Recommended mode: no setting, no suggestion.
    [Fact]
    public async Task Is_off_by_default()
    {
        (await AnalyzeAllAsync(CompileEvents("ConfirmBooking"), Configuration)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Checks_an_event_declared_outside_the_domain(bool asMetadata)
    {
        var domain = CompileDomain();
        var application = Compile(
            "MyApp",
            [
                ("Events.cs", "namespace MyApp.Features.Bookings; public sealed record RemindBooking(Guid BookingId) : MyApp.Domain.IDomainEvent;"),
                ("Usings.cs", ImplicitUsings)
            ],
            [Reference(domain, asMetadata)]);

        (await ReportedAsync(application)).ShouldBe(["RemindBooking"]);
    }

    [Fact]
    public async Task Does_not_report_the_events_of_a_referenced_domain()
    {
        var domain = CompileEvents("ConfirmBooking");
        var application = Compile(
            "MyApp",
            [("Events.cs", "namespace MyApp; public static class Events { public static object Raise() => new MyApp.Domain.Events.ConfirmBooking(Guid.Empty); }"), ("Usings.cs", ImplicitUsings)],
            [Reference(domain, asMetadata: true)]);

        (await ReportedAsync(application)).ShouldBeEmpty();
    }

    // With the domainEvent contract unusable, HS900 names HS019 once a setting turns it on.
    [Fact]
    public async Task Stays_silent_without_its_domain_event_contract()
    {
        var configuration = Configuration.Replace("    \"domainEvent\": \"MyApp.Domain.IDomainEvent, MyApp.Domain\",\n", "");
        configuration.ShouldNotBe(Configuration);

        (await AnalyzeAsync(CompileEvents("ConfirmBooking"), configuration)).ShouldBeEmpty();
        var diagnostic = (await AnalyzeAllAsync(WithHS019(CompileEvents("ConfirmBooking")), configuration)).ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe(ClassificationAnalyzer.ConfigurationId);
        diagnostic.GetMessage().ShouldContain("classify the types checked by HS009, HS019 and HS901.");
    }

    [Fact]
    public async Task Does_not_apply_to_test_projects()
    {
        var diagnostics = await WithHS019(CompileEvents("ConfirmBooking"))
            .WithAnalyzers([new DomainEventNameAnalyzer()], Options(Configuration, isTestProject: true))
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        diagnostics.ShouldBeEmpty();
    }
}
