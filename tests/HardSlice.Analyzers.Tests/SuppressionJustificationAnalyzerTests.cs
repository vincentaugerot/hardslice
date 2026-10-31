using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using static HardSlice.Analyzers.Tests.AnalyzerHarness;

namespace HardSlice.Analyzers.Tests;

// HS000: every suppression of a HardSlice rule carries its justification.
public sealed class SuppressionJustificationAnalyzerTests
{
    private const string Rule = "A HardSlice rule is suppressed only with a reason a reviewer can read.";

    private const string Justified = "// hardslice-justification: approved by the maintainer in review #42";

    private const string AddJustification =
        "Fix: remove the suppression and fix the code; if a maintainer approved it, add '// hardslice-justification: <reason>' on the line above.";

    private static Task<ImmutableArray<Diagnostic>> VerifyAsync(params string[] markedSources) =>
        AnalyzerHarness.VerifyAsync(
            new SuppressionJustificationAnalyzer(), SuppressionJustificationAnalyzer.DiagnosticId, [], OutputKind.DynamicallyLinkedLibrary, markedSources);

    // The first sentence of each message, without its rule and its fix.
    private static IEnumerable<string> Subjects(ImmutableArray<Diagnostic> diagnostics) =>
        diagnostics.Select(diagnostic => diagnostic.GetMessage()[..diagnostic.GetMessage().IndexOf(". A HardSlice rule", StringComparison.Ordinal)]);

    [Fact]
    public async Task Reports_a_pragma_of_a_HardSlice_rule_without_justification()
    {
        var diagnostic = (await VerifyAsync(
            """
            #pragma warning disable [|HS016|]
            namespace MyApp.Features.Bookings.CancelBooking;

            internal sealed class CancelBookingHandler;
            """)).ShouldHaveSingleItem();

        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldBe(
            $"'#pragma warning disable HS016' in 'File0.cs' has no justification. {Rule} {AddJustification} Doc: 'docs/rules/HS000.md'.");
    }

    [Fact]
    public async Task Accepts_a_justification_on_the_line_above_or_on_the_line_of_the_pragma()
    {
        await VerifyAsync(
            $$"""
            {{Justified}}
            #pragma warning disable HS016
            #pragma warning disable HS004 {{Justified}}
            namespace MyApp.Features.Bookings.CancelBooking
            {
                internal sealed class CancelBookingHandler
                {
                    public int Count()
                    {
                        {{Justified}}
            #pragma warning disable HS011
                        return 0;
                    }
                }
            }
            """);
    }

    [Fact]
    public async Task Accepts_a_justification_after_code_on_the_line_above()
    {
        await VerifyAsync(
            $$"""
            namespace MyApp.Domain;

            public sealed class Booking
            {
                public int Count; {{Justified}}
            #pragma warning disable HS007
                public int Total;
            }
            """);
    }

    [Fact]
    public async Task Reports_a_justification_two_lines_above_or_after_a_blank_line()
    {
        await VerifyAsync(
            $$"""
            {{Justified}}

            #pragma warning disable [|HS016|]
            {{Justified}}
            namespace MyApp.Domain
            {
            }
            #pragma warning disable [|HS010|]
            """);
    }

    [Fact]
    public async Task Reports_a_reason_of_fewer_than_ten_characters_that_are_not_spaces()
    {
        var diagnostics = await VerifyAsync(
            """
            // hardslice-justification: needed
            #pragma warning disable [|HS016|]
            // hardslice-justification: a b c d e f g h i
            #pragma warning disable [|HS004|]
            // hardslice-justification:
            #pragma warning disable [|HS007|]
            // hardslice-justification: a b c d e f g h i j
            #pragma warning disable HS010
            //hardslice-justification: approved in review #42
            #pragma warning disable [|HS011|]
            // hardslice-justification:approved in review #42
            #pragma warning disable HS015
            namespace MyApp.Domain;
            """);

        Subjects(diagnostics).ShouldBe(
        [
            "'#pragma warning disable HS016' in 'File0.cs' has a justification of fewer than 10 characters",
            "'#pragma warning disable HS004' in 'File0.cs' has a justification of fewer than 10 characters",
            "'#pragma warning disable HS007' in 'File0.cs' has a justification of fewer than 10 characters",
            "'#pragma warning disable HS011' in 'File0.cs' has no justification"
        ]);
        diagnostics[0].GetMessage().ShouldContain(
            "Fix: remove the suppression and fix the code; if a maintainer approved it, give the reason in at least 10 characters after '// hardslice-justification:'.");
    }

    // Only a single-line comment that starts with the form counts: not a block comment, a doc comment, nor a string.
    [Fact]
    public async Task Reports_a_justification_that_is_not_a_single_line_comment_of_the_form()
    {
        await VerifyAsync(
            """
            /* hardslice-justification: approved by the maintainer in review #42 */
            #pragma warning disable [|HS016|]
            /// hardslice-justification: approved by the maintainer in review #42
            #pragma warning disable [|HS004|]
            // Justification: approved by the maintainer in review #42
            #pragma warning disable [|HS007|]
            // see below. hardslice-justification: approved by the maintainer in review #42
            #pragma warning disable [|HS010|]
            namespace MyApp.Domain
            {
                public static class Texts
                {
                    public const string Text = @"
            // hardslice-justification: approved by the maintainer in review #42";
            #pragma warning disable [|HS011|]
                }
            }
            """);
    }

    [Fact]
    public async Task Reports_each_HardSlice_identifier_of_a_pragma_and_ignores_the_others()
    {
        var diagnostics = await VerifyAsync(
            """
            #pragma warning disable [|HS016|], CS0168, IDE0051, 1591, [|HS004|]
            #pragma warning disable CA1822, xUnit1004, hs016, HSX, HS, HS01a
            #pragma warning restore HS016
            namespace MyApp.Domain;
            """);

        Subjects(diagnostics).ShouldBe(
        [
            "'#pragma warning disable HS016' in 'File0.cs' has no justification",
            "'#pragma warning disable HS004' in 'File0.cs' has no justification"
        ]);
    }

    [Fact]
    public async Task One_justification_covers_every_identifier_of_its_pragma()
    {
        await VerifyAsync(
            $$"""
            {{Justified}}
            #pragma warning disable HS016, HS004
            namespace MyApp.Domain;
            """);
    }

    // A pragma without identifier disables every warning, the HardSlice rules included.
    [Fact]
    public async Task Reports_a_pragma_that_disables_every_warning_unless_justified()
    {
        var diagnostic = (await VerifyAsync(
            $$"""
            [|#pragma warning disable|]
            namespace MyApp.Domain
            {
                public static class Clock
                {
                    {{Justified}}
            #pragma warning disable
                }
            }
            """)).ShouldHaveSingleItem();

        diagnostic.GetMessage().ShouldBe(
            "'#pragma warning disable' in 'File0.cs' disables every warning, the HardSlice rules included, and has no justification. "
            + $"{Rule} Fix: name the warnings it disables; for a HardSlice rule, remove the suppression and fix the code; "
            + "if a maintainer approved it, add '// hardslice-justification: <reason>' on the line above. Doc: 'docs/rules/HS000.md'.");
    }

    [Fact]
    public async Task Reports_a_pragma_of_HS000_even_justified_and_keeps_reporting_after_it()
    {
        var diagnostics = await VerifyAsync(
            $$"""
            {{Justified}}
            #pragma warning disable [|HS000|]
            #pragma warning disable [|HS016|]
            #pragma warning disable [|HS000|], [|HS004|]
            namespace MyApp.Domain;
            """);

        diagnostics[0].GetMessage().ShouldBe(
            $"'#pragma warning disable HS000' in 'File0.cs' suppresses HS000, which no justification allows. {Rule} "
            + "Fix: remove it; HS000 reports every suppression of a HardSlice rule that has no justification. Doc: 'docs/rules/HS000.md'.");
    }

    [Fact]
    public async Task Ignores_a_pragma_in_an_inactive_region()
    {
        await VerifyAsync(
            """
            #if HARDSLICE_NEVER_DEFINED
            #pragma warning disable HS016
            #endif
            #if !HARDSLICE_NEVER_DEFINED
            #pragma warning disable [|HS004|]
            #endif
            namespace MyApp.Domain;
            """);
    }

    [Fact]
    public async Task Reports_SuppressMessage_of_a_HardSlice_rule_on_every_target()
    {
        var diagnostics = await VerifyAsync(
            """
            using System.Diagnostics.CodeAnalysis;

            [assembly: SuppressMessage("HardSlice.Architecture", [|"HS016"|], Scope = "namespaceanddescendants", Target = "~N:MyApp.Features")]
            [module: SuppressMessage("HardSlice.Architecture", [|"HS004"|])]

            namespace MyApp.Domain
            {
                [SuppressMessage("HardSlice.Architecture", [|"HS007"|])]
                public sealed class Booking
                {
                    [SuppressMessage("HardSlice.Architecture", [|"HS008"|])]
                    private readonly object room = new();

                    [SuppressMessage("Style", [|"HS007:EntityState"|])]
                    public int Status { get; private set; }

                    [return: SuppressMessage("HardSlice.Architecture", [|"HS005"|])]
                    public object Confirm([SuppressMessage("HardSlice.Architecture", [|"HS010"|])] DateTimeOffset now)
                    {
                        [SuppressMessage("HardSlice.Architecture", [|"HS011"|])]
                        int Local() => 0;

                        return Local() + room.GetHashCode() + now.Second;
                    }

                    [SuppressMessage("HardSlice.Architecture", [|"HS009"|], Justification = "The record is generated elsewhere.")]
                    public event EventHandler? Changed;
                }
            }
            """);

        Subjects(diagnostics).Take(2).ShouldBe(
        [
            "'[SuppressMessage(\"HardSlice.Architecture\", \"HS016\")]' in 'File0.cs' has no justification",
            "'[SuppressMessage(\"HardSlice.Architecture\", \"HS004\")]' in 'File0.cs' has no justification"
        ]);
        Subjects(diagnostics).ShouldContain("'[SuppressMessage(\"Style\", \"HS007:EntityState\")]' in 'File0.cs' has no justification");
    }

    [Fact]
    public async Task Recognizes_the_attributes_by_their_symbols()
    {
        var diagnostics = await VerifyAsync(
            """
            using Ignore = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;

            namespace MyApp.Domain
            {
                public static class Rules
                {
                    public const string Id = "HS016";
                }

                [Ignore("HardSlice.Architecture", [|Rules.Id|])]
                [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", [|"HS004"|])]
                [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute(checkId: [|"HS007"|], category: "HardSlice.Architecture")]
                [SuppressMessage("HardSlice.Architecture", "HS008")]
                [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822")]
                [System.Diagnostics.CodeAnalysis.SuppressMessage("HardSlice.Architecture", "hs016")]
                public sealed class Booking;

                // An attribute of the application with the same name suppresses nothing.
                [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
                public sealed class SuppressMessageAttribute(string category, string checkId) : Attribute
                {
                    public string Category { get; } = category;

                    public string CheckId { get; } = checkId;
                }
            }
            """);

        Subjects(diagnostics).ShouldBe(
        [
            "'[SuppressMessage(\"HardSlice.Architecture\", \"HS016\")]' in 'File0.cs' has no justification",
            "'[UnconditionalSuppressMessage(\"Trimming\", \"HS004\")]' in 'File0.cs' has no justification",
            "'[SuppressMessage(\"HardSlice.Architecture\", \"HS007\")]' in 'File0.cs' has no justification"
        ]);
    }

    [Fact]
    public async Task Accepts_a_justified_attribute_whatever_its_Justification_property()
    {
        await VerifyAsync(
            $$"""
            using System.Diagnostics.CodeAnalysis;

            {{Justified}}
            [assembly: SuppressMessage("HardSlice.Architecture", "HS016")]

            namespace MyApp.Domain
            {
                {{Justified}}
                [SuppressMessage("HardSlice.Architecture", "HS007")]
                public sealed class Booking
                {
                    [SuppressMessage("HardSlice.Architecture", "HS007")] [SuppressMessage("HardSlice.Architecture", "HS008")] {{Justified}}
                    public int Status { get; set; }

                    [SuppressMessage("HardSlice.Architecture", [|"HS007"|], Justification = "Approved by the maintainer in review #42.")]
                    public int Total { get; set; }
                }
            }
            """);
    }

    [Fact]
    public async Task Reports_a_SuppressMessage_of_HS000_even_justified_and_keeps_reporting_under_it()
    {
        var diagnostics = await VerifyAsync(
            $$"""
            using System.Diagnostics.CodeAnalysis;

            [assembly: SuppressMessage("HardSlice.Architecture", [|"HS000"|])]

            namespace MyApp.Domain
            {
                {{Justified}}
                [SuppressMessage("HardSlice.Architecture", [|"HS000"|])]
                [SuppressMessage("HardSlice.Architecture", [|"HS007"|])]
                public sealed class Booking
                {
            #pragma warning disable [|HS008|]
                }
            }
            """);

        Subjects(diagnostics).First().ShouldBe("'[SuppressMessage(\"HardSlice.Architecture\", \"HS000\")]' in 'File0.cs' suppresses HS000, which no justification allows");
    }

    // HS000 would hide the suppressions it reports: no setting of the project turns it off.
    [Fact]
    public async Task Neither_a_severity_setting_nor_NoWarn_turns_HS000_off()
    {
        var compilation = Compile(
            "MyApp",
            [("/solution/src/MyApp/Booking.cs", "#pragma warning disable HS016\nnamespace MyApp.Domain;"), ("/solution/src/MyApp/Usings.cs", ImplicitUsings)]);
        var editorConfig =
            """
            root = true

            [*.cs]
            dotnet_diagnostic.HS000.severity = none
            """;
        var globalConfig =
            """
            is_global = true

            dotnet_diagnostic.HS000.severity = none
            """;

        var configured = await AnalyzeAllAsync(compilation, null, [("/solution/.editorconfig", editorConfig), ("/solution/.globalconfig", globalConfig)]);
        var noWarn = await AnalyzeAllAsync(
            compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions([new("HS000", ReportDiagnostic.Suppress)])),
            null);

        configured.ShouldContain(diagnostic => diagnostic.Id == "HS000");
        noWarn.ShouldContain(diagnostic => diagnostic.Id == "HS000");
    }

    // A test project cannot hide a HardSlice rule either.
    [Fact]
    public async Task Reports_in_test_projects()
    {
        var compilation = Compile(
            "MyApp.Tests",
            [("BookingTests.cs", "namespace MyApp.Tests;\n\n[System.Diagnostics.CodeAnalysis.SuppressMessage(\"HardSlice.Architecture\", \"HS016\")]\npublic sealed class BookingTests;"), ("Usings.cs", ImplicitUsings)]);

        var diagnostic = (await AnalyzeAllAsync(compilation, null, isTestProject: true)).Where(diagnostic => diagnostic.Id == "HS000").ShouldHaveSingleItem();

        diagnostic.GetMessage().ShouldStartWith("'[SuppressMessage(\"HardSlice.Architecture\", \"HS016\")]' in 'BookingTests.cs' has no justification.");
    }

    // An auto-generated header must not hide a suppression of a HardSlice rule. A pragma without identifier, which source
    // generators write at the top of their output, is not reported there.
    [Fact]
    public async Task Reports_in_generated_code_the_pragmas_that_name_a_HardSlice_rule()
    {
        await VerifyAsync(
            """
            // <auto-generated/>
            #pragma warning disable
            #pragma warning disable [|HS016|]
            namespace MyApp.Domain;

            [System.CodeDom.Compiler.GeneratedCode("probe", "1.0")]
            [System.Diagnostics.CodeAnalysis.SuppressMessage("HardSlice.Architecture", [|"HS007"|])]
            public sealed class Booking;
            """);
    }

    // The suppression it allows still applies: HS000 checks the justification, not the rule it suppresses.
    [Theory]
    [InlineData(Justified, "")]
    [InlineData("", "HS000")]
    public async Task A_justified_suppression_still_turns_its_rule_off(string justification, string expected)
    {
        var compilation = Compile(
            "MyApp",
            [
                ("Features/Bookings/ConfirmBooking/ConfirmBooking.cs", "namespace MyApp.Features.Bookings.ConfirmBooking;\n\npublic sealed record ConfirmBookingCommand;"),
                ("Features/Bookings/CancelBooking/CancelBooking.cs",
                    $"{justification}\n#pragma warning disable HS016\nnamespace MyApp.Features.Bookings.CancelBooking;\n\n"
                    + "internal sealed class CancelBookingHandler(MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingCommand command);"),
                ("Usings.cs", ImplicitUsings)
            ]);

        var diagnostics = await compilation
            .WithAnalyzers([new SliceReferenceAnalyzer(), new SuppressionJustificationAnalyzer()], Options(null))
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        string.Join(" ", diagnostics.Select(diagnostic => diagnostic.Id)).ShouldBe(expected);
    }

    [Fact]
    public async Task Ignores_code_without_suppression()
    {
        await VerifyAsync(
            """
            #nullable enable
            #region Booking
            namespace MyApp.Domain;

            // hardslice-justification: a comment alone suppresses nothing
            [Obsolete("Use Reservation.")]
            public sealed class Booking;
            #endregion
            """);
    }
}
