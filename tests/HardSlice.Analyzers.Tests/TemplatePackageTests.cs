using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace HardSlice.Analyzers.Tests;

public sealed class TemplatePackageTests
{
    // Written by the pack of HardSlice.Templates in place of the version of the HardSlice packages.
    private const string VersionToken = "HARDSLICE_VERSION";

    // Directory.Build.props holds the only version: the pack of the template writes it where the template pins the analyzers and the tool.
    [Fact]
    public async Task Packed_template_pins_the_analyzers_and_the_tool_at_the_version_of_the_HardSlice_packages()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var repositoryRoot = FindRepositoryRoot();
        var templateRoot = TemplateRoot(repositoryRoot);
        var version = PackageVersion(repositoryRoot);
        var templateProject = Path.Combine(repositoryRoot, "src", "HardSlice.Templates", "HardSlice.Templates.csproj");
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), $"hardslice-template-package-{Guid.NewGuid():N}");
        var feedDirectory = Path.Combine(temporaryDirectory, "feed");
        // The pack builds in its own folders: it never races the build of the solution for obj/.
        string[] isolation =
        [
            $"-p:BaseOutputPath={Path.Combine(temporaryDirectory, "pack-bin")}{Path.DirectorySeparatorChar}",
            $"-p:BaseIntermediateOutputPath={Path.Combine(temporaryDirectory, "pack-obj")}{Path.DirectorySeparatorChar}",
            "-p:DefaultItemExcludes=obj/**"
        ];

        try
        {
            var restore = await Dotnet.RunAsync(repositoryRoot, null, cancellationToken, ["restore", templateProject, .. isolation]);
            restore.ExitCode.ShouldBe(0, restore.Output);
            var pack = await Dotnet.RunAsync(
                repositoryRoot,
                null,
                cancellationToken,
                ["pack", templateProject, "--configuration", "Release", "--no-restore", "--output", feedDirectory, .. isolation]);
            pack.ExitCode.ShouldBe(0, pack.Output);

            using var archive = ZipFile.OpenRead(
                Directory.EnumerateFiles(feedDirectory, $"HardSlice.Templates.{version}.nupkg").ShouldHaveSingleItem());
            var packedPackages = TemplateEntry(archive, "Directory.Packages.props");
            var packedManifest = TemplateEntry(archive, ".config/dotnet-tools.json");

            AnalyzerReferenceVersion(packedPackages).ShouldBe(version);
            using var manifest = JsonDocument.Parse(packedManifest);
            var tools = manifest.RootElement.GetProperty("tools");
            tools.GetProperty("hardslice.tool").GetProperty("version").GetString().ShouldBe(version);
            tools.GetProperty("dotnet-ef").GetProperty("version").GetString()
                .ShouldBe(PackageVersions(Path.Combine(templateRoot, "Directory.Packages.props"))["Microsoft.EntityFrameworkCore.Design"]);

            // The pack replaces the token and nothing else.
            packedPackages.TrimEnd().ShouldBe(
                File.ReadAllText(Path.Combine(templateRoot, "Directory.Packages.props")).Replace(VersionToken, version).TrimEnd());
            packedManifest.TrimEnd().ShouldBe(
                File.ReadAllText(Path.Combine(templateRoot, ".config", "dotnet-tools.json")).Replace(VersionToken, version).TrimEnd());
            archive.Entries.Select(entry => entry.FullName).ShouldContain("README.md");
            // The item templates ship in the same package, beside hardslice-api.
            PackedFiles(archive, "/hardslice-command/").ShouldBe(CommandTemplateFiles.Order(StringComparer.Ordinal));
            PackedFiles(archive, "/hardslice-query/").ShouldBe(QueryTemplateFiles.Order(StringComparer.Ordinal));
            PackedFiles(archive, "/hardslice-module/").ShouldBe(ModuleTemplateFiles.Order(StringComparer.Ordinal));
            PackedFiles(archive, "/hardslice-aggregate/").ShouldBe(AggregateTemplateFiles.Order(StringComparer.Ordinal));
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory))
                Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    // hardslice-command generates the three files of a command slice, at the paths of HS004 and HS018, and nothing else.
    [Fact]
    public void Command_item_template_generates_a_slice_its_validator_tests_and_its_integration_tests_only()
    {
        var root = CommandTemplateRoot(FindRepositoryRoot());
        using var template = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".template.config", "template.json")));
        var config = template.RootElement;

        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ShouldBe(CommandTemplateFiles.Order(StringComparer.Ordinal));
        config.GetProperty("shortName").GetString().ShouldBe("hardslice-command");
        config.GetProperty("tags").GetProperty("type").GetString().ShouldBe("item");
        config.GetProperty("sourceName").GetString().ShouldBe("CommandName");
        // The application is read from the web project, never written in the template.
        var application = config.GetProperty("symbols").GetProperty("AppName");
        application.GetProperty("type").GetString().ShouldBe("bind");
        application.GetProperty("binding").GetString().ShouldBe("msbuild:RootNamespace");
        application.GetProperty("replaces").GetString().ShouldBe("MyApp");
    }

    // Every refusal fails the generation, after excluding every file, and says that nothing was created.
    [Fact]
    public void Command_item_template_refuses_with_an_error_that_creates_nothing()
    {
        using var template = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(CommandTemplateRoot(FindRepositoryRoot()), ".template.config", "template.json")));
        var postActions = template.RootElement.GetProperty("postActions").EnumerateArray().ToArray();
        var refusals = postActions.Where(action => action.GetProperty("id").GetString()!.StartsWith("refuse", StringComparison.Ordinal)).ToArray();

        refusals.Length.ShouldBe(7);
        refusals.ShouldAllBe(action => !action.GetProperty("continueOnError").GetBoolean());
        refusals.Select(action => action.GetProperty("manualInstructions")[0].GetProperty("text").GetString()!)
            .ShouldAllBe(text => text.StartsWith("error: ") && text.EndsWith(" Nothing was created."));
        template.RootElement.GetProperty("sources")[0].GetProperty("modifiers")[0].GetProperty("condition").GetString().ShouldBe("(!Accepted)");
        postActions.Last().GetProperty("condition").GetString().ShouldBe("(Accepted)");
    }

    // hardslice-query generates the two files of a query slice, at the paths of HS004 and HS018, and nothing else: a query has no validator.
    [Fact]
    public void Query_item_template_generates_a_slice_and_its_integration_tests_only()
    {
        var root = QueryTemplateRoot(FindRepositoryRoot());
        using var template = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".template.config", "template.json")));
        var config = template.RootElement;

        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ShouldBe(QueryTemplateFiles.Order(StringComparer.Ordinal));
        config.GetProperty("shortName").GetString().ShouldBe("hardslice-query");
        config.GetProperty("tags").GetProperty("type").GetString().ShouldBe("item");
        config.GetProperty("sourceName").GetString().ShouldBe("QueryName");
        var symbols = config.GetProperty("symbols");
        var application = symbols.GetProperty("AppName");
        application.GetProperty("type").GetString().ShouldBe("bind");
        application.GetProperty("binding").GetString().ShouldBe("msbuild:RootNamespace");
        application.GetProperty("replaces").GetString().ShouldBe("MyApp");
        symbols.GetProperty("Aggregate").GetProperty("isRequired").GetBoolean().ShouldBeTrue();
        // Never list: dotnet new would read '--result list' as its own list command.
        symbols.GetProperty("Result").GetProperty("choices").EnumerateArray()
            .Select(choice => choice.GetProperty("choice").GetString())
            .ShouldBe(["single", "many"]);
        symbols.GetProperty("Result").GetProperty("defaultValue").GetString().ShouldBe("single");
    }

    // Both results read the aggregate without tracking and project it to the DTO of the slice (HS006).
    [Fact]
    public void Query_item_template_reads_its_aggregate_without_tracking_in_both_results()
    {
        var slice = File.ReadAllText(
            Path.Combine(QueryTemplateRoot(FindRepositoryRoot()), "src", "MyApp", "Features", "ModuleName", "QueryName", "QueryName.cs"));

        Regex.Matches(slice, @"db\.Set<AggregateName>\(\)\s+\.AsNoTracking\(\)").Count.ShouldBe(2);
        Regex.Matches(slice, @"\.Select\(aggregate => new QueryNameDto\(").Count.ShouldBe(2);
        slice.ShouldNotContain("AsTracking()");
        slice.ShouldNotContain("SaveChanges");
    }

    // Every refusal fails the generation, after excluding every file, and says that nothing was created.
    [Fact]
    public void Query_item_template_refuses_with_an_error_that_creates_nothing()
    {
        using var template = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(QueryTemplateRoot(FindRepositoryRoot()), ".template.config", "template.json")));
        var postActions = template.RootElement.GetProperty("postActions").EnumerateArray().ToArray();
        var refusals = postActions.Where(action => action.GetProperty("id").GetString()!.StartsWith("refuse", StringComparison.Ordinal)).ToArray();

        refusals.Length.ShouldBe(7);
        refusals.ShouldAllBe(action => !action.GetProperty("continueOnError").GetBoolean());
        refusals.Select(action => action.GetProperty("manualInstructions")[0].GetProperty("text").GetString()!)
            .ShouldAllBe(text => text.StartsWith("error: ") && text.EndsWith(" Nothing was created."));
        template.RootElement.GetProperty("sources")[0].GetProperty("modifiers")[0].GetProperty("condition").GetString().ShouldBe("(!Accepted)");
        postActions.Last().GetProperty("condition").GetString().ShouldBe("(Accepted)");
    }

    // hardslice-module generates the contract of the module and, since dotnet new creates no empty folder, a .gitkeep in the folder
    // of its domain and in each of its test folders: no C# file that HS018 would read as a test.
    [Fact]
    public void Module_item_template_generates_the_contract_of_the_module_and_its_folders_only()
    {
        var root = ModuleTemplateRoot(FindRepositoryRoot());
        using var template = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".template.config", "template.json")));
        var config = template.RootElement;

        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ShouldBe(ModuleTemplateFiles.Order(StringComparer.Ordinal));
        config.GetProperty("shortName").GetString().ShouldBe("hardslice-module");
        config.GetProperty("tags").GetProperty("type").GetString().ShouldBe("item");
        config.GetProperty("sourceName").GetString().ShouldBe("ModuleName");
        var symbols = config.GetProperty("symbols");
        var application = symbols.GetProperty("AppName");
        application.GetProperty("type").GetString().ShouldBe("bind");
        application.GetProperty("binding").GetString().ShouldBe("msbuild:RootNamespace");
        application.GetProperty("replaces").GetString().ShouldBe("MyApp");
        // The route of the group has no default value: the name of the module in kebab-case replaces it.
        symbols.GetProperty("Route").TryGetProperty("defaultValue", out _).ShouldBeFalse();
        symbols.GetProperty("GroupRoute").GetProperty("replaces").GetString().ShouldBe("module-route");
    }

    // The contract has the shape of BookingsModule: registration and mapping on one group, which the lines of the slice templates complete.
    [Fact]
    public void Module_item_template_registers_and_maps_the_slices_on_one_group()
    {
        var contract = File.ReadAllLines(
            Path.Combine(ModuleTemplateRoot(FindRepositoryRoot()), "src", "MyApp", "Features", "ModuleName", "ModuleNameModule.cs"));

        contract.ShouldContain("namespace MyApp.Features.ModuleName;");
        contract.ShouldContain("public static class ModuleNameModule");
        contract.ShouldContain("    public static IServiceCollection AddModuleNameModule(this IServiceCollection services) =>");
        contract.ShouldContain("        services;");
        contract.ShouldContain("    public static IEndpointRouteBuilder MapModuleNameModule(this IEndpointRouteBuilder endpoints)");
        contract.ShouldContain("        var group = endpoints.MapGroup(\"/module-route\");");
        contract.ShouldContain("        return endpoints;");
        contract.ShouldNotContain(line => line.StartsWith("using ", StringComparison.Ordinal));
    }

    // Every refusal fails the generation, after excluding every file, and says that nothing was created.
    [Fact]
    public void Module_item_template_refuses_with_an_error_that_creates_nothing()
    {
        using var template = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(ModuleTemplateRoot(FindRepositoryRoot()), ".template.config", "template.json")));
        var postActions = template.RootElement.GetProperty("postActions").EnumerateArray().ToArray();
        var refusals = postActions.Where(action => action.GetProperty("id").GetString()!.StartsWith("refuse", StringComparison.Ordinal)).ToArray();

        refusals.Length.ShouldBe(5);
        refusals.ShouldAllBe(action => !action.GetProperty("continueOnError").GetBoolean());
        refusals.Select(action => action.GetProperty("manualInstructions")[0].GetProperty("text").GetString()!)
            .ShouldAllBe(text => text.StartsWith("error: ") && text.EndsWith(" Nothing was created."));
        template.RootElement.GetProperty("sources")[0].GetProperty("modifiers")[0].GetProperty("condition").GetString().ShouldBe("(!Accepted)");
        postActions.Last().GetProperty("condition").GetString().ShouldBe("(Accepted)");
    }

    // hardslice-aggregate generates the root, its identifier, its mapping in the module and its unit tests, and nothing else: no migration.
    [Fact]
    public void Aggregate_item_template_generates_the_aggregate_its_identifier_its_mapping_and_its_unit_tests_only()
    {
        var root = AggregateTemplateRoot(FindRepositoryRoot());
        using var template = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".template.config", "template.json")));
        var config = template.RootElement;

        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ShouldBe(AggregateTemplateFiles.Order(StringComparer.Ordinal));
        config.GetProperty("shortName").GetString().ShouldBe("hardslice-aggregate");
        config.GetProperty("tags").GetProperty("type").GetString().ShouldBe("item");
        config.GetProperty("sourceName").GetString().ShouldBe("AggregateName");
        var symbols = config.GetProperty("symbols");
        var application = symbols.GetProperty("AppName");
        application.GetProperty("type").GetString().ShouldBe("bind");
        application.GetProperty("binding").GetString().ShouldBe("msbuild:RootNamespace");
        application.GetProperty("replaces").GetString().ShouldBe("MyApp");
        symbols.GetProperty("Module").GetProperty("isRequired").GetBoolean().ShouldBeTrue();
        // The table has no default value: the name of the aggregate followed by s replaces it.
        symbols.GetProperty("Table").TryGetProperty("defaultValue", out _).ShouldBeFalse();
        symbols.GetProperty("TableOrDefault").GetProperty("replaces").GetString().ShouldBe("AggregateTable");
    }

    // The aggregate has the shape of Booking, which HS007, HS008, HS901 and HS902 accept, and its mapping the shape of BookingConfiguration.
    [Fact]
    public void Aggregate_item_template_encapsulates_its_state_and_maps_it_in_the_module()
    {
        var root = AggregateTemplateRoot(FindRepositoryRoot());
        var aggregate = File.ReadAllLines(Path.Combine(root, "src", "MyApp.Domain", "ModuleName", "AggregateName.cs"));
        var identifier = File.ReadAllLines(Path.Combine(root, "src", "MyApp.Domain", "ModuleName", "AggregateNameId.cs"));
        var configuration = File.ReadAllLines(Path.Combine(root, "src", "MyApp", "Features", "ModuleName", "AggregateNameConfiguration.cs"));

        aggregate.ShouldContain("namespace MyApp.Domain.ModuleName;");
        aggregate.ShouldContain("public sealed class AggregateName : IAggregateRoot");
        aggregate.ShouldContain("    private readonly List<IDomainEvent> _domainEvents = [];");
        aggregate.ShouldContain("    private AggregateName()");
        aggregate.ShouldContain("    public AggregateNameId Id { get; }");
        aggregate.ShouldContain("    public IReadOnlyList<IDomainEvent> DequeueDomainEvents()");
        aggregate.ShouldNotContain(line => line.Contains(" set;", StringComparison.Ordinal) || line.Contains(" init;", StringComparison.Ordinal));
        aggregate.ShouldNotContain(line => line.StartsWith("using ", StringComparison.Ordinal));
        identifier.ShouldContain("public readonly record struct AggregateNameId(Guid Value)");
        configuration.ShouldContain("namespace MyApp.Features.ModuleName;");
        configuration.ShouldContain("internal sealed class AggregateNameConfiguration : IEntityTypeConfiguration<AggregateName>");
        configuration.ShouldContain("        builder.ToTable(\"AggregateTable\");");
        configuration.ShouldContain("            .HasConversion(id => id.Value, value => new AggregateNameId(value))");
        configuration.ShouldContain("        builder.Ignore(aggregate => aggregate.DomainEvents);");
    }

    // Every refusal fails the generation, after excluding every file, and says that nothing was created; the last action prints the migration.
    [Fact]
    public void Aggregate_item_template_refuses_with_an_error_that_creates_nothing()
    {
        using var template = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(AggregateTemplateRoot(FindRepositoryRoot()), ".template.config", "template.json")));
        var postActions = template.RootElement.GetProperty("postActions").EnumerateArray().ToArray();
        var refusals = postActions.Where(action => action.GetProperty("id").GetString()!.StartsWith("refuse", StringComparison.Ordinal)).ToArray();

        refusals.Length.ShouldBe(6);
        refusals.ShouldAllBe(action => !action.GetProperty("continueOnError").GetBoolean());
        refusals.Select(action => action.GetProperty("manualInstructions")[0].GetProperty("text").GetString()!)
            .ShouldAllBe(text => text.StartsWith("error: ") && text.EndsWith(" Nothing was created."));
        template.RootElement.GetProperty("sources")[0].GetProperty("modifiers")[0].GetProperty("condition").GetString().ShouldBe("(!Accepted)");
        postActions.Last().GetProperty("condition").GetString().ShouldBe("(Accepted)");
        postActions.Last().GetProperty("manualInstructions")[0].GetProperty("text").GetString()!
            .ShouldContain("dotnet ef migrations add AddAggregateTable --project src/MyApp --output-dir Shared/Persistence/Migrations");
    }

    // In the repository, the template holds the token in place of the version, in its reference to the analyzers and in its tool entry only.
    [Fact]
    public void Template_holds_the_version_token_where_it_pins_the_analyzers_and_the_tool()
    {
        var templateRoot = TemplateRoot(FindRepositoryRoot());
        var packages = File.ReadAllText(Path.Combine(templateRoot, "Directory.Packages.props"));
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(templateRoot, ".config", "dotnet-tools.json")));

        AnalyzerReferenceVersion(packages).ShouldBe(VersionToken);
        Regex.Count(packages, VersionToken).ShouldBe(1);
        manifest.RootElement.GetProperty("tools").EnumerateObject()
            .Where(tool => tool.Value.GetProperty("version").GetString() == VersionToken)
            .Select(tool => tool.Name)
            .ShouldBe(["hardslice.tool"]);
    }

    // BookingsReferenceTests compiles the template against the repository's versions: they must not drift apart.
    [Fact]
    public void Packages_declared_by_both_the_repository_and_the_template_share_the_same_version()
    {
        var repositoryRoot = FindRepositoryRoot();
        var repositoryVersions = PackageVersions(Path.Combine(repositoryRoot, "Directory.Packages.props"));
        var templateVersions = PackageVersions(Path.Combine(TemplateRoot(repositoryRoot), "Directory.Packages.props"));

        repositoryVersions.Keys.Intersect(templateVersions.Keys).ShouldNotBeEmpty();
        repositoryVersions
            .Where(package => templateVersions.TryGetValue(package.Key, out var templateVersion) && templateVersion != package.Value)
            .Select(package => $"{package.Key}: repository {package.Value}, template {templateVersions[package.Key]}")
            .ShouldBeEmpty();
    }

    // dotnet-ef must match the EF Core design-time and runtime packages it drives, or migrations may not load.
    [Fact]
    public void Template_dotnet_ef_tool_matches_its_EF_Core_packages()
    {
        var templateRoot = TemplateRoot(FindRepositoryRoot());
        var templateVersions = PackageVersions(Path.Combine(templateRoot, "Directory.Packages.props"));
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(templateRoot, ".config", "dotnet-tools.json")));
        var toolVersion = manifest.RootElement.GetProperty("tools").GetProperty("dotnet-ef").GetProperty("version").GetString();

        templateVersions["Microsoft.EntityFrameworkCore.Design"].ShouldBe(toolVersion);
        templateVersions["Microsoft.EntityFrameworkCore.Relational"].ShouldBe(toolVersion);
    }

    // AGENTS.md as generated with and without --no-sample: test-template.sh checks the paths of each generated variant.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Agent_guidance_is_short_and_every_path_it_cites_exists_in_the_template(bool noSample)
    {
        var templateRoot = TemplateRoot(FindRepositoryRoot());
        var lines = AgentGuidance(File.ReadAllLines(Path.Combine(templateRoot, "AGENTS.md")), noSample);
        var agents = string.Join('\n', lines);

        lines.Length.ShouldBeLessThan(150, "AGENTS.md must stay short enough for an agent to keep in context.");
        agents.ShouldNotContain("<!--#", customMessage: "AGENTS.md uses a template condition other than (NoSample) or (!NoSample).");
        if (noSample)
            agents.ShouldNotContain("Booking", Case.Insensitive, "AGENTS.md generated with --no-sample must not mention the Bookings sample.");

        // Placeholder paths such as 'src/MyApp/Features/<Module>/' are conventions, not files.
        var citedPaths = Regex.Matches(agents, @"`(?<path>(?:src|tests|docs|scripts)/[^`<>\s]*|[\w.]+\.props)`")
            .Select(match => match.Groups["path"].Value)
            .Distinct()
            .ToArray();

        citedPaths.ShouldContain("scripts/check.sh");
        citedPaths.ShouldContain("scripts/check.ps1");
        citedPaths.ShouldContain("docs/rules/HS016.md");
        citedPaths
            .Where(path => !File.Exists(Path.Combine(templateRoot, path)) && !Directory.Exists(Path.Combine(templateRoot, path)))
            .ShouldBeEmpty("AGENTS.md cites paths that the generated solution does not contain.");
    }

    // The analysis mode turns the rules on: .editorconfig keeps the HS016 line and the reminder that lowering it is a bypass.
    [Fact]
    public void Template_editorconfig_declares_only_HS016_as_an_error()
    {
        var editorConfig = File.ReadAllLines(Path.Combine(TemplateRoot(FindRepositoryRoot()), ".editorconfig"));

        editorConfig.ShouldContain("root = true");
        editorConfig.ShouldContain("# Lowering a severity is a bypass: see AGENTS.md.");
        editorConfig
            .Where(line => line.Contains("dotnet_diagnostic.HS", StringComparison.Ordinal))
            .ShouldBe(["dotnet_diagnostic.HS016.severity = error"]);
    }

    [Fact]
    public void Template_turns_every_HardSlice_rule_on_with_the_All_analysis_mode()
    {
        XDocument.Load(Path.Combine(TemplateRoot(FindRepositoryRoot()), "Directory.Build.props"))
            .Descendants()
            .Where(element => element.Name.LocalName == "HardSliceAnalysisMode")
            .Select(element => element.Value)
            .ShouldBe(["All"]);
    }

    [Fact]
    public void Template_hardslice_json_allows_the_default_Shared_folders_in_the_slices_style()
    {
        using var configuration = JsonDocument.Parse(File.ReadAllText(Path.Combine(TemplateRoot(FindRepositoryRoot()), "hardslice.json")));

        configuration.RootElement.GetProperty("style").GetString().ShouldBe("slices");
        configuration.RootElement.GetProperty("shared").GetProperty("allowedFolders").EnumerateArray()
            .Select(folder => folder.GetString())
            .ShouldBe(["Messaging", "Persistence", "Behaviors"]);
    }

    // Keeps the lines dotnet new keeps for the given --no-sample value: AGENTS.md branches only on NoSample, without nesting.
    private static string[] AgentGuidance(string[] lines, bool noSample)
    {
        var kept = new List<string>();
        bool? branch = null;
        foreach (var line in lines)
        {
            switch (line)
            {
                case "<!--#if (!NoSample) -->":
                    branch = !noSample;
                    break;
                case "<!--#if (NoSample) -->":
                    branch = noSample;
                    break;
                case "<!--#else -->" when branch is not null:
                    branch = !branch;
                    break;
                case "<!--#endif -->" when branch is not null:
                    branch = null;
                    break;
                default:
                    if (branch is not false)
                        kept.Add(line);
                    break;
            }
        }

        branch.ShouldBeNull("AGENTS.md has a template condition without its <!--#endif -->.");
        return [.. kept];
    }

    private static string PackageVersion(string repositoryRoot) =>
        XDocument.Load(Path.Combine(repositoryRoot, "Directory.Build.props"))
            .Descendants()
            .Single(element => element.Name.LocalName == "Version")
            .Value;

    // The template keeps the reference in a comment that dotnet new uncomments: the repository builds the template content without the package.
    private static string AnalyzerReferenceVersion(string packagesProps)
    {
        var reference = Regex.Match(
            packagesProps,
            """<GlobalPackageReference\s+Include="HardSlice\.Analyzers"\s+Version="(?<version>[^"]+)"\s*/>""");
        reference.Success.ShouldBeTrue("The template no longer references HardSlice.Analyzers.");
        return reference.Groups["version"].Value;
    }

    private static string TemplateEntry(ZipArchive archive, string path)
    {
        var entry = archive.Entries.Single(candidate => candidate.FullName.Replace('\\', '/').EndsWith($"/hardslice-api/{path}", StringComparison.Ordinal));
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    private static readonly string[] CommandTemplateFiles =
    [
        ".template.config/dotnetcli.host.json",
        ".template.config/template.json",
        "src/MyApp/Features/ModuleName/CommandName/CommandName.cs",
        "tests/MyApp.IntegrationTests/Features/ModuleName/CommandName/CommandNameTests.cs",
        "tests/MyApp.Tests/Features/ModuleName/CommandName/CommandNameValidatorTests.cs"
    ];

    private static readonly string[] QueryTemplateFiles =
    [
        ".template.config/dotnetcli.host.json",
        ".template.config/template.json",
        "src/MyApp/Features/ModuleName/QueryName/QueryName.cs",
        "tests/MyApp.IntegrationTests/Features/ModuleName/QueryName/QueryNameTests.cs"
    ];

    private static readonly string[] ModuleTemplateFiles =
    [
        ".template.config/dotnetcli.host.json",
        ".template.config/template.json",
        "src/MyApp.Domain/ModuleName/.gitkeep",
        "src/MyApp/Features/ModuleName/ModuleNameModule.cs",
        "tests/MyApp.IntegrationTests/Features/ModuleName/.gitkeep",
        "tests/MyApp.Tests/Domain/ModuleName/.gitkeep",
        "tests/MyApp.Tests/Features/ModuleName/.gitkeep"
    ];

    private static readonly string[] AggregateTemplateFiles =
    [
        ".template.config/dotnetcli.host.json",
        ".template.config/template.json",
        "src/MyApp.Domain/ModuleName/AggregateName.cs",
        "src/MyApp.Domain/ModuleName/AggregateNameId.cs",
        "src/MyApp/Features/ModuleName/AggregateNameConfiguration.cs",
        "tests/MyApp.Tests/Domain/ModuleName/AggregateNameTests.cs"
    ];

    // The files of the item template under <folder> of the package, relative to that folder.
    private static IEnumerable<string> PackedFiles(ZipArchive archive, string folder) =>
        archive.Entries.Select(entry => entry.FullName.Replace('\\', '/'))
            .Where(entry => entry.Contains(folder, StringComparison.Ordinal))
            .Select(entry => entry[(entry.IndexOf(folder, StringComparison.Ordinal) + folder.Length)..])
            .Order(StringComparer.Ordinal);

    private static string CommandTemplateRoot(string repositoryRoot) =>
        Path.Combine(repositoryRoot, "src", "HardSlice.Templates", "content", "hardslice-command");

    private static string QueryTemplateRoot(string repositoryRoot) =>
        Path.Combine(repositoryRoot, "src", "HardSlice.Templates", "content", "hardslice-query");

    private static string ModuleTemplateRoot(string repositoryRoot) =>
        Path.Combine(repositoryRoot, "src", "HardSlice.Templates", "content", "hardslice-module");

    private static string AggregateTemplateRoot(string repositoryRoot) =>
        Path.Combine(repositoryRoot, "src", "HardSlice.Templates", "content", "hardslice-aggregate");

    private static string TemplateRoot(string repositoryRoot) =>
        Path.Combine(repositoryRoot, "src", "HardSlice.Templates", "content", "hardslice-api");

    private static Dictionary<string, string> PackageVersions(string packagesPropsPath) =>
        XDocument.Load(packagesPropsPath)
            .Descendants()
            .Where(element => element.Name.LocalName == "PackageVersion")
            .ToDictionary(element => element.Attribute("Include")!.Value, element => element.Attribute("Version")!.Value);

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HardSlice.slnx")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not find the HardSlice repository root.");
    }
}
