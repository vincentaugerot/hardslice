using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using static HardSlice.Analyzers.Tests.AnalyzerHarness;

namespace HardSlice.Analyzers.Tests;

// The global config of each analysis mode, as build/HardSlice.Analyzers.targets gives it to the compiler.
public sealed class AnalysisModeTests
{
    // The matrix of the analysis modes: the severity of each rule in Minimum, Recommended and All, none when the mode
    // turns it off. A rule ticket adds its line here and in the three global configs.
    private static readonly (string Rule, string Minimum, string Recommended, string All)[] Matrix =
    [
        ("HS003", "error", "error", "error"),
        ("HS004", "none", "error", "error"),
        ("HS005", "none", "error", "error"),
        ("HS010", "error", "error", "error"),
        ("HS011", "error", "error", "error"),
        ("HS015", "none", "error", "error"),
        ("HS016", "error", "error", "error"),
        ("HS900", "error", "error", "error"),
        ("HS901", "error", "error", "error"),
        ("HS902", "error", "error", "error")
    ];

    public static readonly TheoryData<string> Modes = ["Minimum", "Recommended", "All"];

    public static TheoryData<string, string> Cells()
    {
        var cells = new TheoryData<string, string>();
        foreach (var mode in Modes)
        {
            foreach (var row in Matrix)
                cells.Add(mode, row.Rule);
        }

        return cells;
    }

    private const string SourceDirectory = "/solution/src/MyApp/";

    private const string Configuration =
        """
        {
          "style": "slices",
          "contracts": {
            "entity": "MyApp.Domain.IEntity, MyApp",
            "aggregateRoot": "MyApp.Domain.IAggregateRoot, MyApp",
            "domainEvent": "MyApp.Domain.IDomainEvent, MyApp",
            "command": "MyApp.Shared.Messaging.ICommand`1, MyApp",
            "query": "MyApp.Shared.Messaging.IQuery`1, MyApp",
            "requestHandler": "MyApp.Shared.Messaging.IRequestHandler`2, MyApp"
          },
          "shared": {
            "allowedFolders": ["Messaging"]
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

        namespace MyApp.Shared.Messaging
        {
            public interface IRequest<TResponse>;

            public interface ICommand<TResponse> : IRequest<TResponse>;

            public interface IQuery<TResponse> : IRequest<TResponse>;

            public interface IRequestHandler<in TRequest, TResponse> where TRequest : IRequest<TResponse>;
        }
        """;

    // One probe per rule: files that violate it alone, by their path in the project, analyzed with or without hardslice.json.
    private static readonly Dictionary<string, ((string Path, string Source)[] Files, string? Configuration)> Probes = new()
    {
        ["HS003"] = ([("Health/HealthEndpoint.cs", EndpointDatabaseAccess)], Configuration),
        ["HS004"] = ([("Features/Bookings/ArchiveBooking/ArchiveBooking.cs", MisnamedCommand)], Configuration),
        ["HS005"] = ([("Domain/Bookings/Booking.cs", Aggregate), ("Features/Bookings/ConfirmBooking/ConfirmBooking.cs", CommandReturningTheAggregate)], Configuration),
        ["HS010"] = ([("Domain/Bookings/BookingClock.cs", SystemClockRead)], Configuration),
        ["HS011"] = ([("Domain/Bookings/BookingCounter.cs", BlockingCall)], Configuration),
        ["HS015"] = ([("Features/Bookings/ListBookings/ListBookings.cs", SliceEndpointOutsideItsGroup)], Configuration),
        ["HS016"] = (CrossSliceReference(), Configuration),
        ["HS900"] = ([], null),
        ["HS901"] = ([AmbiguousType()], Configuration),
        ["HS902"] = ([("Features/Rooms/RoomConfiguration.cs", UnclassifiedEntity)], Configuration)
    };

    // A technical endpoint outside Features: HS015 does not check it.
    private const string EndpointDatabaseAccess =
        """
        using Microsoft.AspNetCore.Builder;
        using Microsoft.AspNetCore.Routing;
        using Microsoft.EntityFrameworkCore;

        namespace MyApp.Health
        {
            internal static class HealthEndpoint
            {
                public static void Map(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/health", (DbContext db) => db.ToString());
            }
        }
        """;

    // A slice whose command is not named after it.
    private const string MisnamedCommand =
        """
        using MyApp.Shared.Messaging;

        namespace MyApp.Features.Bookings.ArchiveBooking
        {
            public sealed record ArchiveCommand(Guid BookingId) : ICommand<bool>;

            internal sealed class ArchiveBookingHandler : IRequestHandler<ArchiveCommand, bool>;

            internal static class ArchiveBookingEndpoint;
        }
        """;

    // An aggregate root: the contracts make MyApp the domain.
    private const string Aggregate =
        """
        namespace MyApp.Domain.Bookings
        {
            public sealed class Booking : IAggregateRoot;
        }
        """;

    // A complete slice whose command handler returns the aggregate.
    private const string CommandReturningTheAggregate =
        """
        using MyApp.Domain.Bookings;
        using MyApp.Shared.Messaging;

        namespace MyApp.Features.Bookings.ConfirmBooking
        {
            public sealed record ConfirmBookingCommand : ICommand<Booking>;

            internal sealed class ConfirmBookingHandler : IRequestHandler<ConfirmBookingCommand, Booking>;

            internal static class ConfirmBookingEndpoint;
        }
        """;

    // A domain service that reads the system clock: the contracts make MyApp the domain.
    private const string SystemClockRead =
        """
        namespace MyApp.Domain.Bookings
        {
            public static class BookingClock
            {
                public static DateTimeOffset Now() => DateTimeOffset.UtcNow;
            }
        }
        """;

    // A domain service that blocks on a task.
    private const string BlockingCall =
        """
        namespace MyApp.Domain.Bookings
        {
            public static class BookingCounter
            {
                public static int Count(Task<int> count) => count.Result;
            }
        }
        """;

    // A slice endpoint mapped on the application, which returns a string and that no module calls.
    private const string SliceEndpointOutsideItsGroup =
        """
        using Microsoft.AspNetCore.Builder;
        using Microsoft.AspNetCore.Routing;
        using MyApp.Shared.Messaging;

        namespace MyApp.Features.Bookings.ListBookings
        {
            public sealed record ListBookingsQuery : IQuery<string>;

            internal sealed class ListBookingsHandler : IRequestHandler<ListBookingsQuery, string>;

            internal static class ListBookingsEndpoint
            {
                public static void Map(IEndpointRouteBuilder endpoints) => endpoints.MapGet("/bookings", () => "bookings");
            }
        }
        """;

    private const string UnclassifiedEntity =
        """
        using Microsoft.EntityFrameworkCore;
        using Microsoft.EntityFrameworkCore.Metadata.Builders;

        namespace MyApp.Domain.Rooms
        {
            public sealed class Room
            {
                public Guid Id { get; private set; }
            }
        }

        namespace MyApp.Features.Rooms
        {
            internal sealed class RoomConfiguration : IEntityTypeConfiguration<MyApp.Domain.Rooms.Room>
            {
                public void Configure(EntityTypeBuilder<MyApp.Domain.Rooms.Room> builder) => builder.HasKey(room => room.Id);
            }
        }
        """;

    [Theory]
    [MemberData(nameof(Modes))]
    public void Each_mode_file_is_a_global_config_that_sets_every_rule_of_the_matrix(string mode)
    {
        var configs = AnalyzerConfigSet.Create(ImmutableArray.Create(AnalyzerConfig.Parse(ModeFile(mode), ModePath(mode))));

        configs.GlobalConfigOptions.Diagnostics.ShouldBeEmpty();
        configs.GlobalConfigOptions.TreeOptions
            .OrderBy(option => option.Key, StringComparer.Ordinal)
            .Select(option => $"{option.Key.ToUpperInvariant()} {Severity(option.Value)}")
            .ShouldBe(Matrix.Select(row => $"{row.Rule} {SeverityIn(row, mode)}"));
    }

    // A rule ticket that forgets its line in the matrix fails here.
    [Fact]
    public void The_matrix_holds_every_rule_of_the_analyzers()
    {
        Descriptors().Select(descriptor => descriptor.Id).Distinct().Order(StringComparer.Ordinal)
            .ShouldBe(Matrix.Select(row => row.Rule));
    }

    [Fact]
    public void Default_severities_of_the_descriptors_are_the_Recommended_mode()
    {
        foreach (var descriptor in Descriptors())
        {
            var recommended = SeverityIn(Matrix.Single(row => row.Rule == descriptor.Id), "Recommended");
            (descriptor.IsEnabledByDefault ? Severity(descriptor.DefaultSeverity) : "none")
                .ShouldBe(recommended, $"{descriptor.Id} must default to its Recommended severity.");
        }
    }

    [Fact]
    public void A_mode_turns_a_rule_on_at_its_own_severity_and_never_turns_every_rule_off()
    {
        foreach (var descriptor in Descriptors())
        {
            var row = Matrix.Single(row => row.Rule == descriptor.Id);
            Modes.Select(mode => SeverityIn(row, mode)).Where(severity => severity != "none").Distinct()
                .ShouldBe([Severity(descriptor.DefaultSeverity)], $"{descriptor.Id} keeps one severity in every mode that turns it on.");
        }

        foreach (var mode in Modes)
            Matrix.ShouldContain(row => SeverityIn(row, mode) != "none", $"{mode} turns every rule off.");
    }

    // HS019 is a heuristic: in a solution that treats warnings as errors, no mode makes a heuristic rule blocking.
    [Fact]
    public void No_mode_reports_a_rule_as_a_warning()
    {
        Matrix.Where(row => Modes.Any(mode => SeverityIn(row, mode) == "warning")).ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public async Task Each_mode_reports_a_rule_exactly_when_its_row_turns_it_on(string mode, string rule)
    {
        var diagnostics = await AnalyzeProbeAsync(rule, (ModePath(mode), ModeFile(mode)));

        diagnostics.Select(diagnostic => diagnostic.Id).Distinct()
            .ShouldBe(IsOn(rule, mode) ? [rule] : [], $"{mode} {(IsOn(rule, mode) ? "turns on" : "turns off")} {rule}.");
    }

    // dotnet_diagnostic.<rule>.severity = none in .editorconfig turns off a rule the mode keeps, and its severity turns
    // on a rule the mode turns off.
    [Theory]
    [MemberData(nameof(Cells))]
    public async Task An_editorconfig_setting_takes_precedence_over_the_mode(string mode, string rule)
    {
        var on = IsOn(rule, mode);
        var setting = on ? "none" : Severity(Descriptors().First(descriptor => descriptor.Id == rule).DefaultSeverity);
        var editorConfig = $"""
            root = true

            [*.cs]
            dotnet_diagnostic.{rule}.severity = {setting}
            """;

        var diagnostics = await AnalyzeProbeAsync(rule, (ModePath(mode), ModeFile(mode)), ("/solution/.editorconfig", editorConfig));

        diagnostics.Select(diagnostic => diagnostic.Id).Distinct()
            .ShouldBe(on ? [] : [rule], $"{rule} = {setting} in .editorconfig under {mode}.");
    }

    // A suppression in source acts on the rule it names only: the other HardSlice rule of the same compilation stays.
    [Theory]
    [InlineData("HS016", "pragma", "HS901")]
    [InlineData("HS016", "attribute", "HS901")]
    [InlineData("HS901", "pragma", "HS016")]
    [InlineData("HS901", "attribute", "HS016")]
    public async Task Pragma_and_SuppressMessage_turn_off_a_single_HardSlice_rule(string suppressed, string suppression, string kept)
    {
        var compilation = CompileProbe(
            [.. CrossSliceReference(suppressed == "HS016" ? suppression : null), AmbiguousType(suppressed == "HS901" ? suppression : null)]);

        var diagnostics = await AnalyzeAllAsync(compilation, Configuration, [(ModePath("All"), ModeFile("All"))]);

        diagnostics.Select(diagnostic => diagnostic.Id).Distinct().ShouldBe([kept]);
    }

    private static Task<ImmutableArray<Diagnostic>> AnalyzeProbeAsync(string rule, params (string Path, string Text)[] analyzerConfigs)
    {
        var probe = Probes[rule];
        return AnalyzeAllAsync(CompileProbe(probe.Files), probe.Configuration, analyzerConfigs);
    }

    private static Compilation CompileProbe(params (string Path, string Source)[] files) =>
        Compile(
            "MyApp",
            files.Select(file => ($"{SourceDirectory}{file.Path}", file.Source))
                .Append(($"{SourceDirectory}Contracts.cs", Contracts))
                .Append(($"{SourceDirectory}Usings.cs", ImplicitUsings)),
            [.. PackageReferences("Microsoft.EntityFrameworkCore")]);

    // HS016: the handler of CancelBooking uses the command of ConfirmBooking. Both slices are complete, in their folders.
    private static (string Path, string Source)[] CrossSliceReference(string? suppression = null) =>
    [
        ("Features/Bookings/ConfirmBooking/ConfirmBooking.cs",
            """
            using MyApp.Shared.Messaging;

            namespace MyApp.Features.Bookings.ConfirmBooking
            {
                public sealed record ConfirmBookingCommand : ICommand<bool>;

                internal sealed class ConfirmBookingHandler : IRequestHandler<ConfirmBookingCommand, bool>;

                internal static class ConfirmBookingEndpoint;
            }
            """),
        ("Features/Bookings/CancelBooking/CancelBooking.cs",
            $$"""
            {{(suppression == "pragma" ? "#pragma warning disable HS016" : "")}}
            using MyApp.Shared.Messaging;

            namespace MyApp.Features.Bookings.CancelBooking
            {
                public sealed record CancelBookingCommand : ICommand<bool>;

                {{(suppression == "attribute" ? "[System.Diagnostics.CodeAnalysis.SuppressMessage(\"HardSlice.Architecture\", \"HS016\")]" : "")}}
                internal sealed class CancelBookingHandler(MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingCommand command)
                    : IRequestHandler<CancelBookingCommand, bool>;

                internal static class CancelBookingEndpoint;
            }
            """)
    ];

    // HS901: a domain type that is both an aggregate root and a domain event.
    private static (string Path, string Source) AmbiguousType(string? suppression = null) =>
        ("Domain/Bookings/BookingSnapshot.cs", $$"""
        {{(suppression == "pragma" ? "#pragma warning disable HS901" : "")}}
        namespace MyApp.Domain.Bookings
        {
            {{(suppression == "attribute" ? "[System.Diagnostics.CodeAnalysis.SuppressMessage(\"HardSlice.Configuration\", \"HS901\")]" : "")}}
            public sealed class BookingSnapshot : IAggregateRoot, IDomainEvent;
        }
        """);

    // A rule may have several descriptors, such as HS015 and the part of it reported at the end of the compilation.
    private static IEnumerable<DiagnosticDescriptor> Descriptors() =>
        AnalyzerHarness.Analyzers().SelectMany(analyzer => analyzer.SupportedDiagnostics).OrderBy(descriptor => descriptor.Id, StringComparer.Ordinal);

    private static bool IsOn(string rule, string mode) => SeverityIn(Matrix.Single(row => row.Rule == rule), mode) != "none";

    private static string SeverityIn((string Rule, string Minimum, string Recommended, string All) row, string mode) =>
        mode switch
        {
            "Minimum" => row.Minimum,
            "Recommended" => row.Recommended,
            "All" => row.All,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };

    // The names of severities in .editorconfig and global config files.
    private static string Severity(ReportDiagnostic severity) =>
        severity switch
        {
            ReportDiagnostic.Error => "error",
            ReportDiagnostic.Warn => "warning",
            ReportDiagnostic.Info => "suggestion",
            ReportDiagnostic.Hidden => "silent",
            ReportDiagnostic.Suppress => "none",
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null)
        };

    private static string Severity(DiagnosticSeverity severity) =>
        severity switch
        {
            DiagnosticSeverity.Error => "error",
            DiagnosticSeverity.Warning => "warning",
            DiagnosticSeverity.Info => "suggestion",
            _ => "silent"
        };

    private static string ModePath(string mode) => $"/packages/hardslice.analyzers/build/HardSlice.{mode}.globalconfig";

    private static string ModeFile(string mode) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "AnalysisModes", $"HardSlice.{mode}.globalconfig"));
}
