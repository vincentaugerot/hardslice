namespace MyApp.ArchitectureTests;

// HS018: the tests mirror the slices. The tests of 'src/<App>/Features/<Module>/<Slice>/' are at the same path under
// 'tests/<App>.Tests/' or 'tests/<App>.IntegrationTests/': every slice has one, and every test folder of 'Features' has its slice.
public sealed class TestLayoutTests
{
    private const string Doc = "Skill: add-slice. Doc: 'docs/rules/HS018.md'.";

    private static readonly TestSources Tests = TestSources.Solution;

    private static readonly string Rule =
        $"Tests mirror the slices: the tests of 'src/{Application.WebName}/Features/<Module>/<Slice>/' are at the same path under "
        + $"'tests/{Tests.UnitTests}/' or 'tests/{Tests.IntegrationTests}/'.";

    // Slices on disk and in the compiled namespaces: a namespace may not match its folder.
    [Fact]
    public void Every_slice_has_tests_at_its_path()
    {
        var slices = SourceFolders().Where(folder => folder.Contains('/'));

        Violations.ShouldBeNone(
            from slice in slices
            where !Tests.HasFilesIn($"Features/{slice}")
            select $"HS018: slice '{slice}' has no tests in {Tests.Describe($"Features/{slice}")}. {Rule} "
                + $"Fix: add the tests of slice '{slice}' at that path: integration tests in '{TestSources.PathOf(Tests.IntegrationTests, $"Features/{slice}")}', "
                + $"unit tests in '{TestSources.PathOf(Tests.UnitTests, $"Features/{slice}")}'; move any existing test of the slice there. {Doc}");
    }

    [Fact]
    public void Every_test_folder_of_Features_has_its_module_or_slice()
    {
        var sources = SourceFolders().ToHashSet(StringComparer.Ordinal);

        Violations.ShouldBeNone(
            from test in Tests.FeatureFolders()
            where !sources.Contains(test.Folder)
            let kind = test.Folder.Contains('/') ? "slice" : "module"
            select $"HS018: test folder '{TestSources.PathOf(test.Project, $"Features/{test.Folder}")}' has no {kind} "
                + $"'src/{Application.WebName}/Features/{test.Folder}/'. {Rule} "
                + $"Fix: move these tests to the folder of the {kind} they test; when a slice or module is renamed or moved, its test folders follow. {Doc}");
    }

    // 'Orders' and 'Orders/PlaceOrder' for the modules and slices of 'src/<App>/Features/' and of the compiled namespaces.
    private static IEnumerable<string> SourceFolders()
    {
        var onDisk = TestSources.FeatureFoldersUnder(Path.Combine(Application.SolutionRoot, "src", Application.WebName, "Features"));
        var compiled =
            from type in Application.WebAssembly.GetTypes()
            let location = Location.Of(type.Namespace)
            from folder in new[] { location.Module, location.Slice }
            where folder is not null
            select folder;

        return onDisk.Concat(compiled).Distinct(StringComparer.Ordinal);
    }
}
