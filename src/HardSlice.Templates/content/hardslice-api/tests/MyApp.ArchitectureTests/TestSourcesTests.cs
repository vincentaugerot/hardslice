namespace MyApp.ArchitectureTests;

public sealed class TestSourcesTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("hardslice-test-sources-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Finds_facts_theories_and_custom_test_attributes_however_they_are_written()
    {
        const string source = """
            public sealed class ProbeTests
            {
                [Fact] public void Plain() { }
                [Xunit.Theory, InlineData(1)] public void Qualified(int value) { }
                [global::Xunit.FactAttribute] public void Aliased() { }
                [RetryFact(3)] public void Custom() { }
                [Trait("Category", "Slow")] public void NotATest() { }
            }
            """;

        TestSources.TestMethodsIn(source).ShouldBe(["Plain", "Qualified", "Aliased", "Custom"]);
    }

    [Fact]
    public void Ignores_commented_out_and_disabled_tests()
    {
        const string source = """
            public sealed class ProbeTests
            {
                // [Fact] public void Commented() { }
                /* [Fact] public void BlockCommented() { } */
                public void Helper() { }
            }
            """;

        TestSources.TestMethodsIn(source).ShouldBeEmpty();
        // Escape the hash: dotnet new interprets conditional directives even inside string literals.
        TestSources.TestMethodsIn("\u0023if false\nclass Tests { [Fact] public void Disabled() { } }\n\u0023endif").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("NET10_0")]
    [InlineData("DEBUG")]
    [InlineData("CUSTOM_TESTS")]
    public void Counts_only_the_active_preprocessor_branch(string symbol)
    {
        var source = string.Join("\n",
            "class Tests {",
            $"\u0023if {symbol}",
            "    [Fact] public void Enabled() { }",
            "\u0023else",
            "    [Fact] public void Disabled() { }",
            "\u0023endif",
            "}");

        TestSources.TestMethodsIn(source, [symbol]).ShouldBe(["Enabled"]);
        TestSources.TestMethodsIn(source, []).ShouldBe(["Disabled"]);
    }

    [Fact]
    public void Uses_the_symbols_of_each_test_project()
    {
        const string enabled = "\u0023if INTEGRATION_TESTS\nclass T { [Fact] public void A() { } }\n\u0023endif";
        const string disabled = "\u0023if !INTEGRATION_TESTS\nclass T { [Fact] public void A() { } }\n\u0023endif";
        Write("tests/App.Tests/Features/Bookings/UnitEnabled/Test.cs", enabled);
        Write("tests/App.Tests/Features/Bookings/UnitDisabled/Test.cs", disabled);
        Write("tests/App.IntegrationTests/Features/Bookings/IntegrationEnabled/Test.cs", enabled);
        Write("tests/App.IntegrationTests/Features/Bookings/IntegrationDisabled/Test.cs", disabled);
        var sources = new TestSources(_root, "App", new Dictionary<string, string[]>
        {
            ["App.Tests"] = ["NET10_0", "DEBUG"],
            ["App.IntegrationTests"] = ["NET10_0", "DEBUG", "INTEGRATION_TESTS"]
        });

        sources.HasTestsIn("Features/Bookings/UnitEnabled", recursive: true).ShouldBeFalse();
        sources.HasTestsIn("Features/Bookings/UnitDisabled", recursive: true).ShouldBeTrue();
        sources.HasTestsIn("Features/Bookings/IntegrationEnabled", recursive: true).ShouldBeTrue();
        sources.HasTestsIn("Features/Bookings/IntegrationDisabled", recursive: true).ShouldBeFalse();
    }

    [Fact]
    public void Counts_tests_in_either_test_project()
    {
        Write("tests/App.Tests/Features/Bookings/ConfirmBooking/ConfirmBookingValidatorTests.cs", "class T { [Fact] public void A() { } }");
        Write("tests/App.IntegrationTests/Features/Bookings/CancelBooking/CancelBookingTests.cs", "class T { [Fact] public void A() { } }");
        var sources = new TestSources(_root, "App");

        sources.HasTestsIn("Features/Bookings/ConfirmBooking", recursive: true).ShouldBeTrue();
        sources.HasTestsIn("Features/Bookings/CancelBooking", recursive: true).ShouldBeTrue();
        sources.HasTestsIn("Features/Bookings/GetBooking", recursive: true).ShouldBeFalse();
    }

    [Fact]
    public void A_folder_with_files_but_no_test_has_no_tests()
    {
        Write("tests/App.Tests/Features/Bookings/ArchiveBooking/ArchiveBookingFixture.cs", "class Fixture { }");
        var sources = new TestSources(_root, "App");

        sources.HasFilesIn("Features/Bookings/ArchiveBooking").ShouldBeTrue();
        sources.HasTestsIn("Features/Bookings/ArchiveBooking", recursive: true).ShouldBeFalse();
    }

    [Fact]
    public void Tests_of_subfolders_count_only_when_recursive()
    {
        Write("tests/App.IntegrationTests/Features/Bookings/ConfirmBooking/Conflicts/ConflictTests.cs", "class T { [Fact] public void A() { } }");
        var sources = new TestSources(_root, "App");

        sources.HasTestsIn("Features/Bookings/ConfirmBooking", recursive: true).ShouldBeTrue();
        sources.HasTestsIn("Features/Bookings/ConfirmBooking", recursive: false).ShouldBeFalse();
    }

    [Fact]
    public void Lists_the_module_and_slice_folders_that_hold_files()
    {
        Write("tests/App.Tests/Features/Bookings/ConfirmBooking/Nested/ProbeTests.cs", "");
        Write("tests/App.IntegrationTests/Features/Bookings/BookingConfigurationTests.cs", "");
        Write("tests/App.IntegrationTests/Features/RootTests.cs", "");
        Directory.CreateDirectory(Path.Combine(_root, "tests/App.Tests/Features/Rooms/Empty"));

        new TestSources(_root, "App").FeatureFolders().ShouldBe(
            [("App.Tests", "Bookings"), ("App.Tests", "Bookings/ConfirmBooking"), ("App.IntegrationTests", "Bookings")],
            ignoreOrder: true);
    }

    private void Write(string relativePath, string content)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
