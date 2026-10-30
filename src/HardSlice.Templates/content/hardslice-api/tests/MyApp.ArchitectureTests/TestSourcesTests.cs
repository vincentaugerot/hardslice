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
    public void Resolves_attribute_aliases_in_the_file_and_the_enclosing_namespaces()
    {
        const string source = """
            using Test = global::Xunit.FactAttribute;
            using Cases = Xunit.TheoryAttribute;
            using ShortAttribute = Xunit.FactAttribute;
            namespace Outer
            {
                using Retry = Custom.RetryFactAttribute;
                class Tests
                {
                    [Test] public void FactAlias() { }
                    [Cases] public void TheoryAlias() { }
                    [Short] public void ShortenedAlias() { }
                    [@Short] public void VerbatimNameDoesNotExpand() { }
                    [Retry] public void CustomAlias() { }
                }
                namespace Suffix
                {
                    using FactAttribute = Xunit.TraitAttribute;
                    class Tests { [Fact] public void ShadowedSuffixedAlias() { } }
                }
                namespace Inner
                {
                    using Test = Xunit.TraitAttribute;
                    using Fact = Xunit.TraitAttribute;
                    class Tests
                    {
                        [Test] public void ShadowedAlias() { }
                        [Fact] public void ShadowedFactName() { }
                        [Retry] public void InheritedAlias() { }
                        [Xunit.Fact] public void Qualified() { }
                    }
                }
            }
            namespace Sibling
            {
                class Tests { [Retry] public void AliasOutOfScope() { } }
            }
            """;

        TestSources.TestMethodsIn(source).ShouldBe(["FactAlias", "TheoryAlias", "ShortenedAlias", "CustomAlias", "InheritedAlias", "Qualified"]);
        TestSources.TestMethodsIn("namespace Tests; using Test = Xunit.FactAttribute; class T { [Test] public void A() { } }")
            .ShouldBe(["A"]);
    }

    [Fact]
    public void Only_compiled_sources_count_and_deleted_files_do_not_survive_in_the_build_inventory()
    {
        const string folder = "Features/Orders/CancelOrder";
        const string testFile = "tests/App.Tests/Features/Orders/CancelOrder/Test.cs";
        Write(testFile, "class Tests { [Fact] public void A() { } }");
        var inputs = new Dictionary<string, string[]>
        {
            ["App.Tests"] = [],
            ["App.IntegrationTests"] = []
        };
        var sources = new TestSources(_root, "App", projectSources: inputs);

        sources.HasFilesIn(folder).ShouldBeTrue(); // HS018 checks the folder, HS014 the compiled tests.
        sources.HasTestsIn(folder, recursive: true).ShouldBeFalse();
        inputs["App.Tests"] = [Path.Combine(_root, testFile)];
        sources.HasTestsIn(folder, recursive: true).ShouldBeTrue();
        File.Delete(Path.Combine(_root, testFile));
        sources.HasTestsIn(folder, recursive: true).ShouldBeFalse();
    }

    [Fact]
    public void Global_aliases_come_only_from_active_compiled_sources_of_the_same_project()
    {
        const string folder = "Features/Orders/CancelOrder";
        const string testFile = "tests/App.Tests/Features/Orders/CancelOrder/Test.cs";
        const string aliasesFile = "tests/App.Tests/Aliases.cs";
        Write(testFile, "class Tests { [Test] public void A() { } }");
        Write("tests/App.IntegrationTests/Features/Orders/CancelOrder/Test.cs", "class Tests { [Test] public void A() { } }");
        Write(aliasesFile, "\u0023if ALIASES\nglobal using Test = Xunit.FactAttribute;\n\u0023endif");
        var inputs = new Dictionary<string, string[]>
        {
            ["App.Tests"] = [Path.Combine(_root, testFile)],
            ["App.IntegrationTests"] = []
        };
        var symbols = new Dictionary<string, string[]> { ["App.Tests"] = ["ALIASES"], ["App.IntegrationTests"] = [] };
        var sources = new TestSources(_root, "App", symbols, inputs);

        sources.HasTestsIn(folder, recursive: true).ShouldBeFalse();
        symbols["App.Tests"] = [];
        inputs["App.Tests"] = [Path.Combine(_root, testFile), Path.Combine(_root, aliasesFile)];
        sources.HasTestsIn(folder, recursive: true).ShouldBeFalse();
        symbols["App.Tests"] = ["ALIASES"];
        sources.HasTestsIn(folder, recursive: true).ShouldBeTrue();
        Write(testFile, "namespace Local { using Test = Xunit.TraitAttribute; class Tests { [Test] public void A() { } } }");
        inputs["App.IntegrationTests"] = [Path.Combine(_root, "tests/App.IntegrationTests/Features/Orders/CancelOrder/Test.cs")];
        sources.HasTestsIn(folder, recursive: true).ShouldBeFalse();
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
        Write("tests/App.Tests/Features/Orders/UnitEnabled/Test.cs", enabled);
        Write("tests/App.Tests/Features/Orders/UnitDisabled/Test.cs", disabled);
        Write("tests/App.IntegrationTests/Features/Orders/IntegrationEnabled/Test.cs", enabled);
        Write("tests/App.IntegrationTests/Features/Orders/IntegrationDisabled/Test.cs", disabled);
        var sources = new TestSources(_root, "App", new Dictionary<string, string[]>
        {
            ["App.Tests"] = ["NET10_0", "DEBUG"],
            ["App.IntegrationTests"] = ["NET10_0", "DEBUG", "INTEGRATION_TESTS"]
        });

        sources.HasTestsIn("Features/Orders/UnitEnabled", recursive: true).ShouldBeFalse();
        sources.HasTestsIn("Features/Orders/UnitDisabled", recursive: true).ShouldBeTrue();
        sources.HasTestsIn("Features/Orders/IntegrationEnabled", recursive: true).ShouldBeTrue();
        sources.HasTestsIn("Features/Orders/IntegrationDisabled", recursive: true).ShouldBeFalse();
    }

    [Fact]
    public void Counts_tests_in_either_test_project()
    {
        Write("tests/App.Tests/Features/Orders/PlaceOrder/PlaceOrderValidatorTests.cs", "class T { [Fact] public void A() { } }");
        Write("tests/App.IntegrationTests/Features/Orders/CancelOrder/CancelOrderTests.cs", "class T { [Fact] public void A() { } }");
        var sources = new TestSources(_root, "App");

        sources.HasTestsIn("Features/Orders/PlaceOrder", recursive: true).ShouldBeTrue();
        sources.HasTestsIn("Features/Orders/CancelOrder", recursive: true).ShouldBeTrue();
        sources.HasTestsIn("Features/Orders/GetOrder", recursive: true).ShouldBeFalse();
    }

    [Fact]
    public void A_folder_with_files_but_no_test_has_no_tests()
    {
        Write("tests/App.Tests/Features/Orders/ArchiveOrder/ArchiveOrderFixture.cs", "class Fixture { }");
        var sources = new TestSources(_root, "App");

        sources.HasFilesIn("Features/Orders/ArchiveOrder").ShouldBeTrue();
        sources.HasTestsIn("Features/Orders/ArchiveOrder", recursive: true).ShouldBeFalse();
    }

    [Fact]
    public void Tests_of_subfolders_count_only_when_recursive()
    {
        Write("tests/App.IntegrationTests/Features/Orders/PlaceOrder/Conflicts/ConflictTests.cs", "class T { [Fact] public void A() { } }");
        var sources = new TestSources(_root, "App");

        sources.HasTestsIn("Features/Orders/PlaceOrder", recursive: true).ShouldBeTrue();
        sources.HasTestsIn("Features/Orders/PlaceOrder", recursive: false).ShouldBeFalse();
    }

    [Fact]
    public void Lists_the_module_and_slice_folders_that_hold_files()
    {
        Write("tests/App.Tests/Features/Orders/PlaceOrder/Nested/ProbeTests.cs", "");
        Write("tests/App.IntegrationTests/Features/Orders/OrderConfigurationTests.cs", "");
        Write("tests/App.IntegrationTests/Features/RootTests.cs", "");
        Directory.CreateDirectory(Path.Combine(_root, "tests/App.Tests/Features/Rooms/Empty"));

        new TestSources(_root, "App").FeatureFolders().ShouldBe(
            [("App.Tests", "Orders"), ("App.Tests", "Orders/PlaceOrder"), ("App.IntegrationTests", "Orders")],
            ignoreOrder: true);
    }

    private void Write(string relativePath, string content)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
