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
            var packedReference = TemplateEntry(archive, "Directory.HardSlice.props");
            var packedPackages = TemplateEntry(archive, "Directory.Packages.props");
            var packedManifest = TemplateEntry(archive, ".config/dotnet-tools.json");

            AnalyzerReferenceVersion(packedReference).ShouldBe(version);
            using var manifest = JsonDocument.Parse(packedManifest);
            var tools = manifest.RootElement.GetProperty("tools");
            tools.GetProperty("hardslice.tool").GetProperty("version").GetString().ShouldBe(version);
            tools.GetProperty("dotnet-ef").GetProperty("version").GetString()
                .ShouldBe(PackageVersions(Path.Combine(templateRoot, "Directory.Packages.props"))["Microsoft.EntityFrameworkCore.Design"]);

            // The pack replaces the token and nothing else. PB-060: Directory.Packages.props, which the application owns, ships as is.
            packedReference.TrimEnd().ShouldBe(
                File.ReadAllText(Path.Combine(templateRoot, "Directory.HardSlice.props")).Replace(VersionToken, version).TrimEnd());
            packedPackages.ShouldBe(File.ReadAllText(Path.Combine(templateRoot, "Directory.Packages.props")));
            packedManifest.TrimEnd().ShouldBe(
                File.ReadAllText(Path.Combine(templateRoot, ".config", "dotnet-tools.json")).Replace(VersionToken, version).TrimEnd());
            archive.Entries.Select(entry => entry.FullName).ShouldContain("README.md");
            // PB-059: the page of the package on NuGet.org shows its icon.
            archive.Entries.Select(entry => entry.FullName).ShouldContain("icon.png");
            // PB-055: the generated solution keeps its build output, test results and IDE files out of git.
            TemplateEntry(archive, ".gitignore").ShouldBe(File.ReadAllText(Path.Combine(templateRoot, ".gitignore")));
            // The item templates ship in the same package, beside hardslice-api.
            PackedFiles(archive, "/hardslice-command/").ShouldBe(CommandTemplateFiles.Order(StringComparer.Ordinal));
            PackedFiles(archive, "/hardslice-query/").ShouldBe(QueryTemplateFiles.Order(StringComparer.Ordinal));
            PackedFiles(archive, "/hardslice-module/").ShouldBe(ModuleTemplateFiles.Order(StringComparer.Ordinal));
            PackedFiles(archive, "/hardslice-aggregate/").ShouldBe(AggregateTemplateFiles.Order(StringComparer.Ordinal));
            PackedFiles(archive, "/hardslice-entity/").ShouldBe(EntityTemplateFiles.Order(StringComparer.Ordinal));
            PackedFiles(archive, "/hardslice-event/").ShouldBe(EventTemplateFiles.Order(StringComparer.Ordinal));
            PackedFiles(archive, "/hardslice-condition/").ShouldBe(ConditionTemplateFiles.Order(StringComparer.Ordinal));
            // docs/agents/support.md: .claude/skills/ is the only source, the pack copies it into .agents/skills/ for Codex and Gemini CLI.
            PackedFiles(archive, "/hardslice-api/.claude/skills/").ShouldBe(SkillNames.Select(skill => $"{skill}/SKILL.md").Order(StringComparer.Ordinal));
            PackedFiles(archive, "/hardslice-api/.agents/skills/").ShouldBe(SkillNames.Select(skill => $"{skill}/SKILL.md").Order(StringComparer.Ordinal));
            foreach (var skill in SkillNames)
            {
                var source = File.ReadAllText(Path.Combine(templateRoot, ".claude", "skills", skill, "SKILL.md"));
                TemplateEntry(archive, $".claude/skills/{skill}/SKILL.md").ShouldBe(source);
                TemplateEntry(archive, $".agents/skills/{skill}/SKILL.md").ShouldBe(source, $"The copy of {skill} in .agents/skills/ differs from its source.");
            }
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory))
                Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    // PB-055: git never tracks the build output, the test results the check writes, nor the files of IDEs.
    [Fact]
    public void Generated_solution_ignores_its_build_output_test_results_and_IDE_files()
    {
        var gitignore = File.ReadAllLines(Path.Combine(TemplateRoot(FindRepositoryRoot()), ".gitignore"))
            .Where(line => line.Length > 0 && !line.StartsWith('#'));

        gitignore.ShouldBe(["bin/", "obj/", "TestResults/", ".vs/", ".idea/", "*.user"], ignoreOrder: true);
    }

    // PB-055: a NuGet security alert published after this version on one of its pinned packages stays a warning: with
    // TreatWarningsAsErrors, it would break the restore of every solution the template generates.
    [Fact]
    public void Generated_solution_keeps_NuGet_security_alerts_as_warnings()
    {
        var properties = XDocument.Load(Path.Combine(TemplateRoot(FindRepositoryRoot()), "Directory.Build.props")).Descendants("PropertyGroup").Elements()
            .ToDictionary(element => element.Name.LocalName, element => element.Value);

        properties["TreatWarningsAsErrors"].ShouldBe("true");
        properties["WarningsNotAsErrors"].Split(';').ShouldBe(["$(WarningsNotAsErrors)", "NU1901", "NU1902", "NU1903", "NU1904"]);
    }

    // PB-062: dotnet user-secrets set needs a UserSecretsId, and dotnet user-secrets init writes it with a BOM on the first line of the
    // project. The web project has one, which dotnet new replaces with a new GUID in each generated solution.
    [Fact]
    public void Api_template_gives_each_generated_solution_its_own_UserSecretsId()
    {
        var root = TemplateRoot(FindRepositoryRoot());
        using var template = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".template.config", "template.json")));
        var userSecretsId = XDocument.Load(Path.Combine(root, "src", "MyApp", "MyApp.csproj")).Descendants("UserSecretsId").ShouldHaveSingleItem().Value;

        Guid.TryParseExact(userSecretsId, "D", out _).ShouldBeTrue();
        template.RootElement.GetProperty("guids").EnumerateArray().Select(guid => guid.GetString()).ShouldBe([userSecretsId]);
    }

    // PB-095: a second source writes the files of the application into .hardslice/generation/, as the template generates them, for
    // dotnet hardslice upgrade to merge from. It leaves out the files that the template owns, the list of ProtectedFiles and the rule
    // sheets, and the instruction files of the agents, with the exclusions of the variant: one list of the protected files (PB-053).
    [Fact]
    public void Api_template_records_its_generation_without_the_protected_files_nor_the_instructions_of_the_agents()
    {
        var root = TemplateRoot(FindRepositoryRoot());
        using var template = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".template.config", "template.json")));
        var sources = template.RootElement.GetProperty("sources").EnumerateArray().ToArray();

        sources.Length.ShouldBe(2);
        sources[0].TryGetProperty("target", out _).ShouldBeFalse();
        sources[0].TryGetProperty("exclude", out _).ShouldBeFalse();
        var generation = sources[1];
        generation.GetProperty("target").GetString().ShouldBe("./" + HardSlice.Tool.UpgradeCommand.GenerationFolder);
        generation.TryGetProperty("source", out _).ShouldBeFalse();
        generation.GetProperty("exclude").EnumerateArray().Select(glob => glob.GetString()).ShouldBe(HardSlice.Tool.UpgradeCommand.GenerationExclusions("MyApp"));
        JsonElement.DeepEquals(generation.GetProperty("modifiers"), sources[0].GetProperty("modifiers")).ShouldBeTrue("the generation leaves out the files of the sample as the solution does");
        HardSlice.Tool.UpgradeCommand.GenerationExclusions("MyApp").ShouldContain("**/Directory.Build.props");
        HardSlice.Tool.UpgradeCommand.GenerationExclusions("MyApp").ShouldContain("tests/MyApp.ArchitectureTests/**");
        HardSlice.Tool.UpgradeCommand.GenerationExclusions("MyApp").ShouldContain("docs/rules/**");
        HardSlice.Tool.UpgradeCommand.GenerationExclusions("MyApp").ShouldContain("GEMINI.md");
        HardSlice.Tool.UpgradeCommand.GenerationExclusions("MyApp").ShouldNotContain("AGENTS.md");
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

    // dotnet new gives sourceName one form in paths and another in contents: my-app made src/my-app/ referenced as my_app.
    // hardslice-api derives a single safe namespace from the name instead, for both, and no type is named after the application.
    [Fact]
    public void Api_template_names_folders_projects_and_namespaces_with_one_safe_form_of_the_name()
    {
        var root = FindRepositoryRoot();
        using var template = JsonDocument.Parse(File.ReadAllText(Path.Combine(TemplateRoot(root), ".template.config", "template.json")));
        var config = template.RootElement;

        config.TryGetProperty("sourceName", out _).ShouldBeFalse();
        var application = config.GetProperty("symbols").GetProperty("AppName");
        application.GetProperty("type").GetString().ShouldBe("derived");
        application.GetProperty("valueSource").GetString().ShouldBe("name");
        var form = application.GetProperty("valueTransform").GetString()!;
        config.GetProperty("forms").GetProperty(form).GetProperty("identifier").GetString().ShouldBe("safe_namespace");
        application.GetProperty("replaces").GetString().ShouldBe("MyApp");
        application.GetProperty("fileRename").GetString().ShouldBe("MyApp");

        var typesNamedAfterTheApplication =
            from file in Directory.EnumerateFiles(Path.Combine(root, "src", "HardSlice.Templates", "content"), "*", SearchOption.AllDirectories)
            let relative = Path.GetRelativePath(root, file).Replace('\\', '/')
            where !relative.Split('/').Any(segment => segment is "bin" or "obj" or "TestResults")
            where Regex.IsMatch(relative, "MyApp[A-Za-z0-9_]") || Regex.IsMatch(File.ReadAllText(file), "MyApp[A-Za-z0-9_]")
            select relative;
        typesNamedAfterTheApplication.ShouldBeEmpty();
    }

    // Every refusal fails the generation, after excluding every file, and says that nothing was created.
    [Fact]
    public void Command_item_template_refuses_with_an_error_that_creates_nothing()
    {
        using var template = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(CommandTemplateRoot(FindRepositoryRoot()), ".template.config", "template.json")));
        var postActions = template.RootElement.GetProperty("postActions").EnumerateArray().ToArray();
        var refusals = postActions.Where(action => action.GetProperty("id").GetString()!.StartsWith("refuse", StringComparison.Ordinal)).ToArray();

        refusals.Length.ShouldBe(26);
        refusals.ShouldAllBe(action => !action.GetProperty("continueOnError").GetBoolean());
        refusals.Select(action => action.GetProperty("manualInstructions")[0].GetProperty("text").GetString()!)
            .ShouldAllBe(text => text.StartsWith("error: ") && text.EndsWith(" Nothing was created."));
        template.RootElement.GetProperty("sources")[0].GetProperty("modifiers")[0].GetProperty("condition").GetString().ShouldBe("(!Accepted)");
        // The lines to add, then, with --require, what its policy asks (PB-081): nothing else is printed.
        postActions.Where(action => !action.GetProperty("id").GetString()!.StartsWith("refuse", StringComparison.Ordinal))
            .Select(action => action.GetProperty("condition").GetString()).ShouldBe(["(Accepted)", "(Accepted && RequireIsGiven)"]);
    }

    // PB-064: --parameters none generates an empty command without validator nor validator tests, and refuses --response none, whose
    // 404 answers an unknown aggregate that such a command has no identifier to look for. Without the option, the slice is unchanged.
    [Fact]
    public void Command_item_template_generates_a_command_without_parameters_and_refuses_it_a_response_of_404()
    {
        var root = CommandTemplateRoot(FindRepositoryRoot());
        using var template = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".template.config", "template.json")));
        var config = template.RootElement;
        var symbols = config.GetProperty("symbols");
        var parameters = symbols.GetProperty("Parameters");
        using var host = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".template.config", "dotnetcli.host.json")));

        parameters.GetProperty("datatype").GetString().ShouldBe("choice");
        parameters.GetProperty("choices").EnumerateArray().Select(choice => choice.GetProperty("choice").GetString()).ShouldBe(["id", "none"]);
        // No default value: --every refuses an explicit --parameters id, which generates the same slice as no option at all.
        parameters.TryGetProperty("defaultValue", out _).ShouldBeFalse();
        symbols.GetProperty("ParametersIsNone").GetProperty("value").GetString().ShouldBe("(Parameters == \"none\" || ScheduleIsGiven)");
        host.RootElement.GetProperty("symbolInfo").GetProperty("Parameters").GetProperty("longName").GetString().ShouldBe("parameters");
        symbols.GetProperty("ResponseNeedsParameters").GetProperty("value").GetString().ShouldBe("(ParametersIsNone && ResponseIsNone)");
        symbols.GetProperty("Accepted").GetProperty("value").GetString()!.ShouldContain("!ResponseNeedsParameters");
        config.GetProperty("sources")[0].GetProperty("modifiers").EnumerateArray()
            .Where(modifier => modifier.GetProperty("condition").GetString() == "(ParametersIsNone)")
            .SelectMany(modifier => modifier.GetProperty("exclude").EnumerateArray().Select(pattern => pattern.GetString()))
            .ShouldBe(["tests/MyApp.Tests/**/*"]);
        var refusal = config.GetProperty("postActions").EnumerateArray()
            .Single(action => action.GetProperty("id").GetString() == "refuseResponseWithoutIdentifier");
        refusal.GetProperty("condition").GetString().ShouldBe("(ResponseNeedsParameters)");
        var registrations = config.GetProperty("postActions").EnumerateArray()
            .Single(action => action.GetProperty("id").GetString() == "registerSlice")
            .GetProperty("manualInstructions").EnumerateArray()
            .ToDictionary(instruction => instruction.GetProperty("condition").GetString()!, instruction => instruction.GetProperty("text").GetString()!);
        registrations.Keys.ShouldBe([
            "(!EndpointIsNone && !ParametersIsNone && ResponseIsId)", "(!EndpointIsNone && !ParametersIsNone && ResponseIsNone)", "(!EndpointIsNone && !ParametersIsNone && ResponseIsDto)",
            "(!EndpointIsNone && ParametersIsNone && ResponseIsId)", "(!EndpointIsNone && ParametersIsNone && ResponseIsDto)",
            "(EndpointIsNone && !ParametersIsNone && ResponseIsId)", "(EndpointIsNone && !ParametersIsNone && ResponseIsNone)", "(EndpointIsNone && !ParametersIsNone && ResponseIsDto)",
            "(EndpointIsNone && ParametersIsNone && ResponseIsId && !ScheduleIsGiven)", "(EndpointIsNone && ParametersIsNone && ResponseIsDto && !ScheduleIsGiven)",
            "(EveryIsGiven && !RetryIsGiven && ResponseIsId && !AtStart)", "(EveryIsGiven && !RetryIsGiven && ResponseIsDto && !AtStart)",
            "(EveryIsGiven && RetryIsGiven && ResponseIsId && !AtStart)", "(EveryIsGiven && RetryIsGiven && ResponseIsDto && !AtStart)",
            "(DailyAtIsGiven && !RetryIsGiven && ResponseIsId && !AtStart)", "(DailyAtIsGiven && !RetryIsGiven && ResponseIsDto && !AtStart)",
            "(DailyAtIsGiven && RetryIsGiven && ResponseIsId && !AtStart)", "(DailyAtIsGiven && RetryIsGiven && ResponseIsDto && !AtStart)",
            "(EveryIsGiven && !RetryIsGiven && ResponseIsId && AtStart)", "(EveryIsGiven && !RetryIsGiven && ResponseIsDto && AtStart)",
            "(EveryIsGiven && RetryIsGiven && ResponseIsId && AtStart)", "(EveryIsGiven && RetryIsGiven && ResponseIsDto && AtStart)",
            "(DailyAtIsGiven && !RetryIsGiven && ResponseIsId && AtStart)", "(DailyAtIsGiven && !RetryIsGiven && ResponseIsDto && AtStart)",
            "(DailyAtIsGiven && RetryIsGiven && ResponseIsId && AtStart)", "(DailyAtIsGiven && RetryIsGiven && ResponseIsDto && AtStart)"]);
        registrations.Where(registration => registration.Key.Contains("&& ParametersIsNone", StringComparison.Ordinal)
                || registration.Key.StartsWith("(EveryIsGiven", StringComparison.Ordinal) || registration.Key.StartsWith("(DailyAtIsGiven", StringComparison.Ordinal))
            .ShouldAllBe(registration => !registration.Value.Contains("Validator") && !registration.Value.Contains("FluentValidation"));

        var slice = File.ReadAllText(Path.Combine(root, "src", "MyApp", "Features", "ModuleName", "CommandName", "CommandName.cs"));
        slice.ShouldContain("public sealed record CommandNameCommand : ICommand<Guid>;");
        slice.ShouldContain("public sealed record CommandNameCommand : ICommand<ResponseTypeName>;");
        slice.ShouldContain("async (IMediator mediator, CancellationToken cancellationToken) =>\n            TypedResults.Ok(await mediator.Send(new CommandNameCommand(), cancellationToken))");
        slice.ShouldContain("#if (!ParametersIsNone)\n\n// Checks the shape of the input only");
        var tests = File.ReadAllText(Path.Combine(root, "tests", "MyApp.IntegrationTests", "Features", "ModuleName", "CommandName", "CommandNameTests.cs"));
        tests.ShouldContain("PostAsync(Route, content: null, TestContext.Current.CancellationToken)");
    }

    // PB-067: --endpoint none generates a system command, which implements ISystemCommand<TResponse>, without endpoint: its integration
    // tests send it through IMediator, and --route or --method, which choose an endpoint, are refused. Without the option, the slice is unchanged.
    [Fact]
    public void Command_item_template_generates_a_system_command_without_endpoint_and_refuses_the_options_of_an_endpoint()
    {
        var root = CommandTemplateRoot(FindRepositoryRoot());
        using var template = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".template.config", "template.json")));
        var config = template.RootElement;
        var symbols = config.GetProperty("symbols");
        var endpoint = symbols.GetProperty("Endpoint");
        using var host = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".template.config", "dotnetcli.host.json")));

        endpoint.GetProperty("datatype").GetString().ShouldBe("choice");
        endpoint.GetProperty("choices").EnumerateArray().Select(choice => choice.GetProperty("choice").GetString()).ShouldBe(["http", "none"]);
        endpoint.TryGetProperty("defaultValue", out _).ShouldBeFalse();
        symbols.GetProperty("EndpointIsNone").GetProperty("value").GetString().ShouldBe("(Endpoint == \"none\" || ScheduleIsGiven)");
        host.RootElement.GetProperty("symbolInfo").GetProperty("Endpoint").GetProperty("longName").GetString().ShouldBe("endpoint");
        symbols.GetProperty("EndpointOptionsWithoutEndpoint").GetProperty("value").GetString().ShouldBe("(EndpointIsNone && (RouteIsGiven || MethodIsGiven))");
        symbols.GetProperty("Accepted").GetProperty("value").GetString()!.ShouldContain("!EndpointOptionsWithoutEndpoint");
        var contract = symbols.GetProperty("CommandContract");
        contract.GetProperty("replaces").GetString().ShouldBe("ICommand");
        contract.GetProperty("parameters").GetProperty("cases").EnumerateArray().Select(@case => @case.GetProperty("value").GetString()).ShouldBe(["ISystemCommand", "ICommand"]);
        foreach (var (given, value, matches) in new[] { ("RouteIsGiven", "", false), ("RouteIsGiven", "/", true), ("MethodIsGiven", "", false), ("MethodIsGiven", "POST", true) })
            Matches(symbols, given, value).ShouldBe(matches);
        var postActions = config.GetProperty("postActions").EnumerateArray().ToArray();
        postActions.Single(action => action.GetProperty("id").GetString() == "refuseEndpointOptionsWithoutEndpoint")
            .GetProperty("condition").GetString().ShouldBe("(EndpointOptionsWithoutEndpoint)");
        var registrations = postActions.Single(action => action.GetProperty("id").GetString() == "registerSlice")
            .GetProperty("manualInstructions").EnumerateArray()
            .ToDictionary(instruction => instruction.GetProperty("condition").GetString()!, instruction => instruction.GetProperty("text").GetString()!);
        registrations.Where(registration => registration.Key.StartsWith("(EndpointIsNone", StringComparison.Ordinal)).ShouldAllBe(registration =>
            !registration.Value.Contains("Endpoint.Map(group)") && registration.Value.Contains("HS004 fails the build until a type of the module sends it."));
        registrations.Where(registration => registration.Key.StartsWith("(!EndpointIsNone", StringComparison.Ordinal))
            .ShouldAllBe(registration => registration.Value.Contains("        CommandNameEndpoint.Map(group);\n"));

        var slice = File.ReadAllText(Path.Combine(root, "src", "MyApp", "Features", "ModuleName", "CommandName", "CommandName.cs"));
        slice.ShouldContain("}\n#if (!EndpointIsNone)\n\ninternal static class CommandNameEndpoint\n");
        slice.ShouldContain("#if (ResponseIsNone && !EndpointIsNone)\nusing Microsoft.AspNetCore.Http.HttpResults;");
        var tests = File.ReadAllText(Path.Combine(root, "tests", "MyApp.IntegrationTests", "Features", "ModuleName", "CommandName", "CommandNameTests.cs"));
        tests.ShouldContain("await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new CommandNameCommand(), TestContext.Current.CancellationToken);");
        tests.ShouldContain("await Should.ThrowAsync<ValidationException>(");
    }

    // PB-068: --every generates a system command without parameter that AddRecurringCommand sends at regular intervals, with an
    // integration test that runs three cycles on a FakeTimeProvider. It refuses an invalid interval, and --parameters id or
    // --endpoint http, which it contradicts.
    [Theory]
    [InlineData("00:00:30", true)]
    [InlineData("01:00:00", true)]
    [InlineData("23:59:59", true)]
    [InlineData("1.00:00:00", true)]
    [InlineData("48.23:59:59", true)]
    [InlineData("", true)]
    [InlineData("00:00:00", false)]
    [InlineData("0.00:00:00", false)]
    [InlineData("49.00:00:00", false)]
    [InlineData("24:00:00", false)]
    [InlineData("00:60:00", false)]
    [InlineData("0:0:30", false)]
    [InlineData("00:00:30.5", false)]
    [InlineData("30s", false)]
    [InlineData("-00:00:30", false)]
    public void Command_item_template_accepts_an_interval_that_a_timer_can_wait(string interval, bool accepted)
    {
        using var template = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(CommandTemplateRoot(FindRepositoryRoot()), ".template.config", "template.json")));

        Matches(template.RootElement.GetProperty("symbols"), "EveryIsValid", interval).ShouldBe(accepted);
    }

    [Fact]
    public void Command_item_template_generates_a_recurring_command_and_refuses_the_options_it_contradicts()
    {
        var root = CommandTemplateRoot(FindRepositoryRoot());
        using var template = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".template.config", "template.json")));
        var config = template.RootElement;
        var symbols = config.GetProperty("symbols");
        using var host = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".template.config", "dotnetcli.host.json")));

        symbols.GetProperty("Every").GetProperty("datatype").GetString().ShouldBe("text");
        host.RootElement.GetProperty("symbolInfo").GetProperty("Every").GetProperty("longName").GetString().ShouldBe("every");
        // The interval, in the comment of the slice and in the printed lines.
        symbols.GetProperty("EveryOrUnbound").GetProperty("replaces").GetString().ShouldBe("RecurringInterval");
        symbols.GetProperty("EveryOrUnbound").GetProperty("fileRename").GetString().ShouldBe("RecurringInterval");
        File.ReadAllText(Path.Combine(root, "src", "MyApp", "Features", "ModuleName", "CommandName", "CommandName.cs")).ShouldContain("sends a new one every RecurringInterval");
        symbols.GetProperty("EveryContradictsOptions").GetProperty("value").GetString()
            .ShouldBe("(EveryIsGiven && (Parameters == \"id\" || Endpoint == \"http\"))");
        symbols.GetProperty("Accepted").GetProperty("value").GetString()!.ShouldContain("!EveryContradictsOptions && EveryIsValid");
        symbols.GetProperty("CommandContract").GetProperty("parameters").GetProperty("cases")[0].GetProperty("condition").GetString()
            .ShouldBe("(Endpoint == \"none\" || ScheduleIsGiven)");
        var postActions = config.GetProperty("postActions").EnumerateArray().ToArray();
        postActions.Single(action => action.GetProperty("id").GetString() == "refuseInvalidInterval").GetProperty("condition").GetString().ShouldBe("(!EveryIsValid)");
        postActions.Single(action => action.GetProperty("id").GetString() == "refuseEveryWithOptions").GetProperty("condition").GetString().ShouldBe("(EveryContradictsOptions)");
        var registrations = postActions.Single(action => action.GetProperty("id").GetString() == "registerSlice")
            .GetProperty("manualInstructions").EnumerateArray()
            .Where(instruction => instruction.GetProperty("condition").GetString()!.StartsWith("(EveryIsGiven && !RetryIsGiven", StringComparison.Ordinal)
                && instruction.GetProperty("condition").GetString()!.EndsWith(" && !AtStart)", StringComparison.Ordinal))
            .Select(instruction => instruction.GetProperty("text").GetString()!)
            .ToArray();
        registrations.Length.ShouldBe(2);
        registrations[0].ShouldContain("            .AddRecurringCommand<CommandNameCommand, Guid>(\"ModuleName:CommandName\", TimeSpan.Parse(\"RecurringInterval\", CultureInfo.InvariantCulture))\n");
        registrations[1].ShouldContain("            .AddRecurringCommand<CommandNameCommand, ResponseTypeName>(\"ModuleName:CommandName\", TimeSpan.Parse(\"RecurringInterval\", CultureInfo.InvariantCulture))\n");
        registrations.ShouldAllBe(text => text.Contains("using System.Globalization;\n") && !text.Contains("Endpoint.Map(group)") && !text.Contains("Validator"));

        var tests = File.ReadAllText(Path.Combine(root, "tests", "MyApp.IntegrationTests", "Features", "ModuleName", "CommandName", "CommandNameTests.cs"));
        tests.ShouldContain("var cycles = new RecurringCommandCycles<CommandNameCommand, Guid>(app);");
        tests.ShouldContain("for (var cycle = 1; cycle <= 3; cycle++)");
    }

    // PB-082: --daily-at takes a time of day on 24 hours, --time-zone an IANA id or UTC, never a Windows name, and --retry a delay that
    // a timer can wait and a count from 1 to 99.
    [Theory]
    [InlineData("DailyAtIsValid", "04:30", true)]
    [InlineData("DailyAtIsValid", "00:00", true)]
    [InlineData("DailyAtIsValid", "23:59", true)]
    [InlineData("DailyAtIsValid", "", true)]
    [InlineData("DailyAtIsValid", "4:30", false)]
    [InlineData("DailyAtIsValid", "24:00", false)]
    [InlineData("DailyAtIsValid", "04:60", false)]
    [InlineData("DailyAtIsValid", "04:30:00", false)]
    [InlineData("TimeZoneIsValid", "America/Toronto", true)]
    [InlineData("TimeZoneIsValid", "America/Argentina/Buenos_Aires", true)]
    [InlineData("TimeZoneIsValid", "Etc/GMT+5", true)]
    [InlineData("TimeZoneIsValid", "UTC", true)]
    [InlineData("TimeZoneIsValid", "", true)]
    [InlineData("TimeZoneIsValid", "Eastern Standard Time", false)]
    [InlineData("TimeZoneIsValid", "Toronto", false)]
    [InlineData("TimeZoneIsValid", "america/toronto", false)]
    [InlineData("TimeZoneIsValid", "America/Toronto\"", false)]
    [InlineData("RetryIsValid", "01:00:00x3", true)]
    [InlineData("RetryIsValid", "00:00:05x99", true)]
    [InlineData("RetryIsValid", "1.00:00:00x1", true)]
    [InlineData("RetryIsValid", "", true)]
    [InlineData("RetryIsValid", "01:00:00", false)]
    [InlineData("RetryIsValid", "01:00:00x0", false)]
    [InlineData("RetryIsValid", "01:00:00x100", false)]
    [InlineData("RetryIsValid", "00:00:00x3", false)]
    [InlineData("RetryIsValid", "49.00:00:00x3", false)]
    [InlineData("RetryIsValid", "01:00:00:3", false)]
    public void Command_item_template_accepts_a_daily_time_a_time_zone_and_retries_it_can_write(string symbol, string value, bool accepted)
    {
        using var template = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(CommandTemplateRoot(FindRepositoryRoot()), ".template.config", "template.json")));

        Matches(template.RootElement.GetProperty("symbols"), symbol, value).ShouldBe(accepted);
    }

    // PB-082: --daily-at, --time-zone and --retry generate a system command without parameter that AddRecurringCommand sends every day
    // at a local time, again after a failed cycle, with integration tests of two days of cycles and of the retries. They refuse the
    // options they contradict. The hour and the minute of --daily-at are written as the arguments of new TimeOnly.
    [Fact]
    public void Command_item_template_generates_a_daily_command_with_retries_and_refuses_the_options_it_contradicts()
    {
        var root = CommandTemplateRoot(FindRepositoryRoot());
        using var template = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".template.config", "template.json")));
        var config = template.RootElement;
        var symbols = config.GetProperty("symbols");
        using var host = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".template.config", "dotnetcli.host.json")));

        foreach (var (symbol, option) in new[] { ("DailyAt", "daily-at"), ("TimeZone", "time-zone"), ("Retry", "retry") })
        {
            symbols.GetProperty(symbol).GetProperty("datatype").GetString().ShouldBe("text");
            host.RootElement.GetProperty("symbolInfo").GetProperty(symbol).GetProperty("longName").GetString().ShouldBe(option);
        }
        foreach (var (symbol, value, written) in new[]
        {
            ("DailyAtHour", "04:30", "4"), ("DailyAtMinute", "04:30", "30"), ("DailyAtHour", "00:05", "0"), ("DailyAtMinute", "00:05", "5"),
            ("DailyAtHour", "23:00", "23"), ("DailyAtMinute", "23:00", "0"), ("RetryDelay", "01:00:00x3", "01:00:00"), ("RetryCount", "1.00:00:00x12", "12"),
        })
        {
            var step = symbols.GetProperty(symbol).GetProperty("parameters").GetProperty("steps").EnumerateArray().ShouldHaveSingleItem();
            Regex.Replace(value, step.GetProperty("regex").GetString()!, step.GetProperty("replacement").GetString()!).ShouldBe(written);
        }
        symbols.GetProperty("TimeZoneOrDefault").GetProperty("replaces").GetString().ShouldBe("RecurringTimeZone");
        symbols.GetProperty("DefaultTimeZone").GetProperty("parameters").GetProperty("value").GetString().ShouldBe("UTC");
        symbols.GetProperty("ScheduleIsGiven").GetProperty("value").GetString().ShouldBe("(EveryIsGiven || DailyAtIsGiven)");
        symbols.GetProperty("DailyAtContradictsOptions").GetProperty("value").GetString()
            .ShouldBe("(DailyAtIsGiven && (Parameters == \"id\" || Endpoint == \"http\"))");
        symbols.GetProperty("Accepted").GetProperty("value").GetString()!.ShouldContain(
            "DailyAtIsValid && TimeZoneIsValid && RetryIsValid && !EveryWithDailyAt && !DailyAtContradictsOptions && !TimeZoneWithoutDailyAt && !RetryWithoutSchedule");
        var postActions = config.GetProperty("postActions").EnumerateArray().ToArray();
        foreach (var (refusal, condition) in new[]
        {
            ("refuseInvalidDailyTime", "(!DailyAtIsValid)"), ("refuseInvalidTimeZone", "(!TimeZoneIsValid)"), ("refuseInvalidRetry", "(!RetryIsValid)"),
            ("refuseEveryWithDailyTime", "(EveryWithDailyAt)"), ("refuseDailyTimeWithOptions", "(DailyAtContradictsOptions)"),
            ("refuseTimeZoneWithoutDailyTime", "(TimeZoneWithoutDailyAt)"), ("refuseRetryWithoutSchedule", "(RetryWithoutSchedule)"),
        })
            postActions.Single(action => action.GetProperty("id").GetString() == refusal).GetProperty("condition").GetString().ShouldBe(condition);
        var registrations = postActions.Single(action => action.GetProperty("id").GetString() == "registerSlice")
            .GetProperty("manualInstructions").EnumerateArray()
            .ToDictionary(instruction => instruction.GetProperty("condition").GetString()!, instruction => instruction.GetProperty("text").GetString()!);
        registrations["(DailyAtIsGiven && !RetryIsGiven && ResponseIsId && !AtStart)"].ShouldContain(
            "            .AddRecurringCommand<CommandNameCommand, Guid>(\"ModuleName:CommandName\", RecurringSchedule.DailyAt(new TimeOnly(RecurringHour, RecurringMinute), \"RecurringTimeZone\"))\n");
        registrations["(DailyAtIsGiven && !RetryIsGiven && ResponseIsId && !AtStart)"].ShouldNotContain("using System.Globalization;");
        registrations["(DailyAtIsGiven && RetryIsGiven && ResponseIsDto && !AtStart)"].ShouldContain(
            "            .AddRecurringCommand<CommandNameCommand, ResponseTypeName>(\"ModuleName:CommandName\", RecurringSchedule.DailyAt(new TimeOnly(RecurringHour, RecurringMinute), \"RecurringTimeZone\")"
            + ".WithRetries(TimeSpan.Parse(\"RecurringDelayOfRetries\", CultureInfo.InvariantCulture), RecurringCountOfRetries))\n");
        registrations["(EveryIsGiven && RetryIsGiven && ResponseIsId && !AtStart)"].ShouldContain(
            "            .AddRecurringCommand<CommandNameCommand, Guid>(\"ModuleName:CommandName\", RecurringSchedule.Every(TimeSpan.Parse(\"RecurringInterval\", CultureInfo.InvariantCulture))"
            + ".WithRetries(TimeSpan.Parse(\"RecurringDelayOfRetries\", CultureInfo.InvariantCulture), RecurringCountOfRetries))\n");
        registrations.Where(registration => registration.Key.Contains("RetryIsGiven && ", StringComparison.Ordinal) && !registration.Key.Contains("!RetryIsGiven", StringComparison.Ordinal))
            .ShouldAllBe(registration => registration.Value.Contains("using System.Globalization;\n"));

        var slice = File.ReadAllText(Path.Combine(root, "src", "MyApp", "Features", "ModuleName", "CommandName", "CommandName.cs"));
        slice.ShouldContain("sends a new one every day at RecurringDailyAt in\n// RecurringTimeZone");
        var tests = File.ReadAllText(Path.Combine(root, "tests", "MyApp.IntegrationTests", "Features", "ModuleName", "CommandName", "CommandNameTests.cs"));
        tests.ShouldContain("    public async Task Runs_a_cycle_every_day_at_its_local_time()");
        tests.ShouldContain("TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(due, zone).DateTime).ShouldBe(new TimeOnly(RecurringHour, RecurringMinute));");
        tests.ShouldContain("    public async Task Sends_a_failed_cycle_again_at_each_retry_then_waits_for_its_next_instant()");
        tests.ShouldContain("cycles.FailNext(1 + RecurringCountOfRetries);");
    }

    // PB-082: AddRecurringCommand sends any command without parameter on an interval or a daily schedule, with retries, and
    // RecurringCommandCycles waits for the instant that the service waits for, fails cycles and configures its instance.
    [Fact]
    public void Recurring_commands_follow_a_daily_schedule_with_retries_and_their_cycles_configure_their_instance()
    {
        var template = TemplateRoot(FindRepositoryRoot());
        var service = File.ReadAllText(Path.Combine(template, "src", "MyApp", "Shared", "Messaging", "RecurringCommand.cs"));
        var cycles = File.ReadAllText(Path.Combine(template, "tests", "MyApp.IntegrationTests", "RecurringCommandCycles.cs"));

        service.ShouldContain("public static IServiceCollection AddRecurringCommand<TCommand, TResponse>(this IServiceCollection services, string section, RecurringSchedule schedule)\n        where TCommand : ICommand<TResponse>, new()");
        service.ShouldNotContain("where TCommand : ISystemCommand<TResponse>");
        service.ShouldContain("public static RecurringSchedule DailyAt(TimeOnly time, string timeZone)");
        service.ShouldContain("public RecurringSchedule WithRetries(TimeSpan delay, int retries)");
        service.ShouldContain("Task.Delay(due - now, timeProvider, stoppingToken)");
        cycles.ShouldContain("public RecurringCommandCycles(AppFactory app, Action<IWebHostBuilder>? configure = null, DateTimeOffset? start = null)");
        cycles.ShouldContain("public async Task<DateTimeOffset> NextDueAsync()");
        cycles.ShouldContain("public void FailNext(int cycles)");
        cycles.ShouldContain("where TCommand : ICommand<TResponse>, new()");
    }

    // PB-082: the agent kit says how to send a command every day or again after a failed cycle, and that a command with its endpoint
    // is scheduled by the same line.
    [Fact]
    public void Agent_guidance_says_how_to_schedule_a_command_every_day_with_retries()
    {
        var template = TemplateRoot(FindRepositoryRoot());
        var agents = File.ReadAllText(Path.Combine(template, "AGENTS.md"));
        var addSlice = File.ReadAllText(Path.Combine(template, ".claude", "skills", "add-slice", "SKILL.md"));

        agents.ShouldContain("`--every <interval>` or `--daily-at <hh:mm> --time-zone <id>` for one sent at regular intervals or every day (`--retry <delay>x<count>` after a failed cycle)");
        agents.ShouldContain("The same line schedules a command without parameter that keeps its endpoint.");
        addSlice.ShouldContain("--name <Slice> --daily-at 04:30 --time-zone America/Toronto --retry 01:00:00x3");
        addSlice.ShouldContain("its endpoint and its schedule both send it, and its endpoint stays (HS004)");
        addSlice.ShouldContain("`FailNext(count)` fails the next cycles before the pipeline");
        addSlice.ShouldContain("Its second argument configures the instance that runs the cycles");
    }

    // PB-083: a recurring command checks the conditions that its module registers before each send, in a scope of their own, and
    // may also be sent at start. RecurringCommandCycles throws a held cycle, ProgramTests names a condition its module forgot, and
    // AppFactory logs each hosted service it removes.
    [Fact]
    public void Recurring_commands_check_their_conditions_before_each_send_and_AppFactory_logs_the_hosted_services_it_removes()
    {
        var template = TemplateRoot(FindRepositoryRoot());
        var service = File.ReadAllText(Path.Combine(template, "src", "MyApp", "Shared", "Messaging", "RecurringCommand.cs"));
        var cycles = File.ReadAllText(Path.Combine(template, "tests", "MyApp.IntegrationTests", "RecurringCommandCycles.cs"));
        var appFactory = File.ReadAllText(Path.Combine(template, "tests", "MyApp.IntegrationTests", "AppFactory.cs"));
        var programTests = File.ReadAllText(Path.Combine(template, "tests", "MyApp.IntegrationTests", "ProgramTests.cs"));

        service.ShouldContain("public interface IRecurringCommandCondition<TCommand>\n{\n");
        service.ShouldContain("    Task<string?> CheckAsync(RecurringCycle cycle, CancellationToken cancellationToken);");
        service.ShouldContain("public sealed record RecurringCycle(DateTimeOffset Due, bool AtStart, int Retry);");
        service.ShouldContain("public RecurringSchedule AlsoAtStart() => this with { AtStart = true };");
        service.ShouldContain("scope.ServiceProvider.GetServices<IRecurringCommandCondition<TCommand>>()");
        service.ShouldContain("logger.LogInformation(\"{Command} is not sent: {Reason}\", command, reason);");
        cycles.ShouldContain("public void Held(string reason) => _outcomes.Writer.TryWrite(() => throw new HeldCycleException(reason));");
        cycles.ShouldContain("public sealed class HeldCycleException(string reason) : Exception(reason);");
        appFactory.ShouldContain("\"AppFactory removed the hosted service {HostedService} of the application: a test that needs it calls KeepHostedService<{KeptService}>() on its AppFactory\"");
        programTests.ShouldContain("typeof(IDomainEventHandler<>), typeof(IRecurringCommandCondition<>)];");
    }

    // PB-083: the agent kit says how a job decides whether to run, and how a setting is checked at start.
    [Fact]
    public void Agent_guidance_says_how_a_job_decides_whether_to_run()
    {
        var template = TemplateRoot(FindRepositoryRoot());
        var agents = File.ReadAllText(Path.Combine(template, "AGENTS.md"));
        var addSlice = File.ReadAllText(Path.Combine(template, ".claude", "skills", "add-slice", "SKILL.md"));

        agents.ShouldContain("A job decides whether to run through an `IRecurringCommandCondition<TCommand>` in the folder of its slice, checked before each send, which reads options or `AppDbContext` and never writes (HS006)");
        agents.ShouldContain("The setting a job needs is checked there, never by a hosted service that only logs it nor `ValidateOnStart`.");
        agents.ShouldContain("and the logs name each one removed.");
        addSlice.ShouldContain("registered after its handler with `.AddScoped<IRecurringCommandCondition<TCommand>, <Slice>Condition>()`");
        addSlice.ShouldContain("the cycle sends nothing and is not retried, the reason is logged once until it changes");
        addSlice.ShouldContain("`AlsoAtStart()` on the `RecurringSchedule`, which `--at-start` chains, or the key `<Module>:<Slice>:AtStart`");
        addSlice.ShouldContain("its condition holds it back when `cycle.AtStart` and a version covers the day, and lets the nightly cycles go");
        addSlice.ShouldContain("A cycle that a condition holds back throws `HeldCycleException`, whose message is the reason");
    }

    // PB-084: HS006 examines the hosted services and the conditions of a slice, and the agent kit and the sheet say so.
    [Fact]
    public void Agent_guidance_says_that_HS006_covers_the_hosted_services_and_conditions_of_a_slice()
    {
        var template = TemplateRoot(FindRepositoryRoot());
        var agents = File.ReadAllText(Path.Combine(template, "AGENTS.md"));
        var addSlice = File.ReadAllText(Path.Combine(template, ".claude", "skills", "add-slice", "SKILL.md"));
        var sheet = File.ReadAllText(Path.Combine(template, "docs", "rules", "HS006.md"));

        agents.ShouldContain("Any other hosted service lives in the folder of the slice it triggers, sends each command through `IMediator` in a scope of its own, never writes nor tracks (HS006)");
        addSlice.ShouldContain("HS006 reports a write or a tracked read in a condition");
        addSlice.ShouldContain("never writes itself and reads, if it must, with `AsNoTracking()` on a query that loads an entity");
        sheet.ShouldStartWith("# HS006: a query handler, a hosted service or a condition of a slice reads without tracking and never writes");
        sheet.ShouldContain("- **A hosted service**: a class that implements `IHostedService`, directly or through `BackgroundService`");
        sheet.ShouldContain("- **A condition of a recurring command**: a class that implements `IRecurringCommandCondition<TCommand>`");
        sheet.ShouldContain("The hosted services of `Shared`, such as `RecurringCommandService<TCommand, TResponse>`, and those declared in the namespace of a module outside its slices, are not.");
    }

    // PB-090: HS006 asks AsNoTracking() only of a query that loads an entity. AGENTS.md, add-slice and the comment of
    // IRecurringCommandCondition say so for a query handler, a condition and a hosted service, as HS006.md does: AnyAsync, CountAsync
    // and a projection to values need none. The pack copies add-slice into .agents/skills/ as is, which the test of the pack checks.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Agent_guidance_says_that_a_read_that_loads_no_entity_needs_no_AsNoTracking(bool noSample)
    {
        var template = TemplateRoot(FindRepositoryRoot());
        var agents = AgentGuidance(File.ReadAllLines(Path.Combine(template, "AGENTS.md")), noSample);
        var addSlice = File.ReadAllText(Path.Combine(template, ".claude", "skills", "add-slice", "SKILL.md"));
        var service = File.ReadAllText(Path.Combine(template, "src", "MyApp", "Shared", "Messaging", "RecurringCommand.cs"));
        var sheet = File.ReadAllText(Path.Combine(template, "docs", "rules", "HS006.md"));
        const string noEntity = "such as `AnyAsync`, `CountAsync` or a projection to values";

        agents.Length.ShouldBeLessThan(150);
        agents.SkipWhile(line => line != "## Golden rules").Single(line => line.StartsWith("5. A command handler", StringComparison.Ordinal)).ShouldContain(
            "A query handler never writes, never calls `Find` nor `AsTracking`, starts each query that loads an entity with `AsNoTracking()`, "
            + $"and projects to a DTO of values: a read that loads no entity, {noEntity}, needs no `AsNoTracking()` (HS006).");
        var background = agents.Single(line => line.StartsWith("   - **Background work**", StringComparison.Ordinal));
        background.ShouldContain($"which reads options or `AppDbContext` and never writes (HS006): `AsNoTracking()` on a query that loads an entity, none on a read that loads none, {noEntity}.");
        background.ShouldNotContain("`AppDbContext` with `AsNoTracking()`");

        addSlice.ShouldContain($"A query that loads an entity starts with `AsNoTracking()`, and a read that loads none, {noEntity}, needs none: HS006 reports a write or a tracked read in a condition.");
        addSlice.ShouldContain($"with `AsNoTracking()` on a query that loads an entity, which a read that loads none, {noEntity}, does not need (HS006)");
        addSlice.ShouldContain($"HS006 requires it on each query that loads an entity, while a read that loads none, {noEntity}, needs none.");
        addSlice.ShouldNotContain("`AppDbContext` with `AsNoTracking()`");

        service.ShouldContain("// database through AppDbContext, and never writes: what the cycle does is the work of the command. A query that loads an entity\n"
            + "// starts with AsNoTracking(), and a read that loads none, such as AnyAsync, CountAsync or a projection to values, needs none (HS006).");
        sheet.ShouldContain("A query whose result holds no entity is compliant without `AsNoTracking()`");
        sheet.ShouldContain("A hosted service or a condition projects to the values it needs, or asks `AnyAsync` or `CountAsync`.");
    }

    // PB-098: a slice reads the aggregates of another module through AppDbContext, joined to its own in one SQL query, without
    // tracking, and never writes them. HS012.md shows the join and keeps the abstraction of the public contract for a service or an
    // isolated piece of data, add-slice and AGENTS.md point to it. No diagnostic changes. The pack copies add-slice into
    // .agents/skills/ as is, which the test of the pack checks.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Agent_guidance_says_that_a_slice_reads_the_aggregates_of_another_module_and_never_writes_them(bool noSample)
    {
        var template = TemplateRoot(FindRepositoryRoot());
        var agents = AgentGuidance(File.ReadAllLines(Path.Combine(template, "AGENTS.md")), noSample);
        var addSlice = File.ReadAllText(Path.Combine(template, ".claude", "skills", "add-slice", "SKILL.md"));
        var sheet = File.ReadAllText(Path.Combine(template, "docs", "rules", "HS012.md"));

        agents.Length.ShouldBeLessThan(150);
        agents.SkipWhile(line => line != "## Golden rules").Single(line => line.StartsWith("3. A module uses", StringComparison.Ordinal)).ShouldBe(
            "3. A module uses another module only through its public contract: the types directly in `MyApp.Features.<Module>` (HS012). "
            + "A slice may still read the aggregates of another module with `AsNoTracking()`, joined to its own, but never writes them (`docs/rules/HS012.md`).");

        addSlice.ShouldContain("A query that reads two modules joins the aggregates of the other module the same way, through `AppDbContext` and in one SQL query, "
            + "on keys of the same type, and never writes them: their changes go through their own module, as `docs/rules/HS012.md` shows in \"Reading the aggregates of another module\"");

        sheet.ShouldContain("This includes the aggregates of another module, which a slice reads but never writes: see [Reading the aggregates of another module](#reading-the-aggregates-of-another-module).");
        var paragraph = sheet[sheet.IndexOf("\n## Reading the aggregates of another module\n", StringComparison.Ordinal)..sheet.IndexOf("\n## Diagnostic\n", StringComparison.Ordinal)];
        paragraph.ShouldContain("A slice may read the aggregates of another module through `AppDbContext`, in a query that joins them to its own aggregates in SQL");
        paragraph.ShouldContain("`AsNoTracking()` on each query that loads an entity, and a projection with `Select` to a DTO of values, never the aggregate of the other module itself.");
        paragraph.ShouldContain("join booking in db.Set<Booking>().AsNoTracking() on invoice.BookingId equals booking.Id");
        paragraph.ShouldContain("from invoice in db.Set<Invoice>().AsNoTracking()");
        paragraph.ShouldContain("select new UnpaidInvoiceDto(");
        paragraph.ShouldContain("Join on keys of the same type");
        paragraph.ShouldContain("A slice never writes the aggregates of another module: it never adds, removes nor changes one, through `Add`, `Remove`, `ExecuteUpdate`, `ExecuteDelete` or a method of the aggregate, and never tracks one.");
        paragraph.ShouldContain("a change goes through that module: one of its own commands, or a reaction of that module to a domain event of the module that needs the change");

        sheet.ShouldContain("3. **A module needs a service of another module, or an isolated piece of its data**");
        sheet.ShouldContain("A query that joins the aggregates of the other module to its own reads them directly instead, as [Reading the aggregates of another module](#reading-the-aggregates-of-another-module) shows");
        sheet.ShouldContain("HS012: 'RoomsModule' in module 'Rooms' uses 'BookingDto' from slice 'Bookings/GetBooking' of module 'Bookings'. A module uses another module only through its public contract.");
    }

    // PB-094: hardslice-condition generates the condition of a command that AddRecurringCommand sends, in the folder of its slice, and
    // its integration tests at the same path (HS004, HS018), and nothing else. It reads the slices from Directory.Build.targets.
    [Fact]
    public void Condition_item_template_generates_the_condition_of_a_command_and_its_integration_tests_only()
    {
        var root = ConditionTemplateRoot(FindRepositoryRoot());
        using var template = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".template.config", "template.json")));
        using var host = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".template.config", "dotnetcli.host.json")));
        var config = template.RootElement;
        var symbols = config.GetProperty("symbols");

        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ShouldBe(ConditionTemplateFiles.Order(StringComparer.Ordinal));
        config.GetProperty("shortName").GetString().ShouldBe("hardslice-condition");
        config.GetProperty("tags").GetProperty("type").GetString().ShouldBe("item");
        symbols.GetProperty("AppName").GetProperty("binding").GetString().ShouldBe("msbuild:RootNamespace");
        symbols.GetProperty("ModuleFiles").GetProperty("binding").GetString().ShouldBe("msbuild:HardSliceModuleFiles");
        symbols.GetProperty("SliceFiles").GetProperty("binding").GetString().ShouldBe("msbuild:HardSliceSliceFiles");
        foreach (var (symbol, option, token) in new[] { ("Module", "module", "ModuleName"), ("Command", "command", "CommandName") })
        {
            symbols.GetProperty(symbol).GetProperty("isRequired").GetBoolean().ShouldBeTrue();
            symbols.GetProperty(symbol).GetProperty("replaces").GetString().ShouldBe(token);
            host.RootElement.GetProperty("symbolInfo").GetProperty(symbol).GetProperty("longName").GetString().ShouldBe(option);
        }
        symbols.GetProperty("Response").GetProperty("defaultValue").GetString().ShouldBe("id");
        host.RootElement.GetProperty("symbolInfo").GetProperty("Response").GetProperty("longName").GetString().ShouldBe("response");
        var steps = symbols.GetProperty("ResponseType").GetProperty("parameters").GetProperty("steps").EnumerateArray().ToArray();
        foreach (var (response, written) in new[] { ("id", "Guid"), ("none", "bool"), ("ImportedScheduleDto", "ImportedScheduleDto") })
            steps.Aggregate(response, (value, step) => Regex.Replace(value, step.GetProperty("regex").GetString()!, step.GetProperty("replacement").GetString()!)).ShouldBe(written);
        symbols.GetProperty("ResponseType").GetProperty("replaces").GetString().ShouldBe("ResponseTypeName");
        config.GetProperty("sources")[0].GetProperty("modifiers")[0].GetProperty("condition").GetString().ShouldBe("(!Accepted)");
        File.ReadAllText(Path.Combine(TemplateRoot(FindRepositoryRoot()), "Directory.Build.targets"))
            .ShouldContain("<HardSliceSliceFiles>$([System.String]::Join(';', $([System.IO.Directory]::GetFiles('$(MSBuildProjectDirectory)/Features', '*.cs', 'System.IO.SearchOption.AllDirectories'))))</HardSliceSliceFiles>");
    }

    // PB-094: the condition is internal sealed, implements IRecurringCommandCondition<TCommand> and holds a marker in place of its
    // rule. Its tests hold a cycle back with its reason and let one go, through RecurringCommandCycles, with a marker in place of
    // the state of each.
    [Fact]
    public void Condition_item_template_decides_on_each_cycle_and_tests_a_held_cycle_and_a_sent_one()
    {
        var root = ConditionTemplateRoot(FindRepositoryRoot());
        var condition = File.ReadAllLines(Path.Combine(root, "src", "MyApp", "Features", "ModuleName", "CommandName", "CommandNameCondition.cs"));
        var tests = File.ReadAllText(Path.Combine(root, "tests", "MyApp.IntegrationTests", "Features", "ModuleName", "CommandName", "CommandNameConditionTests.cs"));

        condition.ShouldContain("namespace MyApp.Features.ModuleName.CommandName;");
        condition.ShouldContain("internal sealed class CommandNameCondition : IRecurringCommandCondition<CommandNameCommand>");
        condition.ShouldContain("    public Task<string?> CheckAsync(RecurringCycle cycle, CancellationToken cancellationToken) =>");
        condition.Count(line => Regex.IsMatch(line, @"^(public|internal) ")).ShouldBe(1);
        string.Join('\n', condition).ShouldContain("\"TODO(hardslice): implement CommandNameCondition: receive what it reads by its constructor");
        string.Join('\n', condition).ShouldContain("Never write.");
        tests.ShouldContain("namespace MyApp.IntegrationTests.Features.ModuleName.CommandName;");
        tests.ShouldContain("    public async Task Holds_a_cycle_back_with_its_reason()");
        tests.ShouldContain("    public async Task Lets_a_cycle_go_and_sends_the_command()");
        Regex.Count(tests, Regex.Escape("        var cycles = new RecurringCommandCycles<CommandNameCommand, ResponseTypeName>(app);")).ShouldBe(2);
        Regex.Count(tests, Regex.Escape("        // TODO(hardslice): set up the state in which CommandNameCondition ")).ShouldBe(2);
        tests.ShouldContain("var held = await Should.ThrowAsync<HeldCycleException>(cycles.RunNextAsync());");
        tests.ShouldContain("held.Message.ShouldBe(\"TODO(hardslice): ");
        tests.ShouldContain("(await cycles.NextDueAsync()).ShouldBeGreaterThan(cycles.Clock.GetUtcNow(), ");
    }

    // PB-094: every refusal fails the generation, after excluding every file, and says that nothing was created. The last action prints
    // the registration of the condition after the handler of the command.
    [Fact]
    public void Condition_item_template_refuses_with_an_error_that_creates_nothing()
    {
        using var template = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(ConditionTemplateRoot(FindRepositoryRoot()), ".template.config", "template.json")));
        var postActions = template.RootElement.GetProperty("postActions").EnumerateArray().ToArray();
        var refusals = postActions.Where(action => action.GetProperty("id").GetString()!.StartsWith("refuse", StringComparison.Ordinal)).ToArray();

        refusals.Select(action => action.GetProperty("id").GetString()).ShouldBe([
            "refuseUnknownApplication", "refuseInvalidModule", "refuseMissingModule", "refuseInvalidCommand", "refuseCommandSuffix",
            "refuseUnknownSlices", "refuseMissingSlice", "refuseMissingCommand", "refuseExistingCondition", "refuseInvalidResponse"]);
        refusals.ShouldAllBe(action => !action.GetProperty("continueOnError").GetBoolean());
        refusals.Select(action => action.GetProperty("manualInstructions")[0].GetProperty("text").GetString()!)
            .ShouldAllBe(text => text.StartsWith("error: ") && text.EndsWith(" Nothing was created."));
        template.RootElement.GetProperty("symbols").GetProperty("Accepted").GetProperty("value").GetString().ShouldBe("(SliceIsAccepted && ResponseIsAccepted)");
        template.RootElement.GetProperty("symbols").GetProperty("SliceIsAccepted").GetProperty("value").GetString()
            .ShouldBe("(ModuleIsAccepted && CommandIsAccepted && SliceFilesAreKnown && SliceExists && CommandExists && !ConditionExists)");
        var registration = postActions.Last();
        registration.GetProperty("condition").GetString().ShouldBe("(Accepted)");
        registration.GetProperty("continueOnError").GetBoolean().ShouldBeTrue();
        registration.GetProperty("manualInstructions")[0].GetProperty("text").GetString()!.ShouldContain(
            "register the condition by its contract, after the handler of CommandNameCommand:\n            .AddScoped<IRecurringCommandCondition<CommandNameCommand>, CommandNameCondition>()\n");
    }

    // PB-094: the slice exists when its folder holds a source file, its command when it holds <Command>.cs or <Command>Command.cs, and
    // its condition when it holds <Command>Condition.cs, in the files that HardSliceSliceFiles lists, separated by ';'.
    [Theory]
    [InlineData("Schedule|ImportSchedule|/app/src/App/Features/Schedule/ScheduleModule.cs;/app/src/App/Features/Schedule/ImportSchedule/ImportSchedule.cs", true, true, false)]
    [InlineData("Schedule|ImportSchedule|/app/src/App/Features/Schedule/ImportSchedule/GtfsFeed.cs;/app/src/App/Features/Schedule/ImportSchedule/ImportScheduleCommand.cs", true, true, false)]
    [InlineData("Schedule|ImportSchedule|C:\\app\\src\\App\\Features\\Schedule\\ImportSchedule\\ImportSchedule.cs;C:\\app\\src\\App\\Features\\Schedule\\ImportSchedule\\ImportScheduleCondition.cs", true, true, true)]
    [InlineData("Schedule|ImportSchedule|/app/src/App/Features/Schedule/ImportSchedule/GtfsFeed.cs", true, false, false)]
    [InlineData("Schedule|ImportSchedule|/app/src/App/Features/Schedule/ImportScheduleNotes/ImportSchedule.cs", false, false, false)]
    [InlineData("Schedule|ImportSchedule|/app/src/App/Features/Realtime/ImportSchedule/ImportSchedule.cs", false, false, false)]
    [InlineData("Schedule|ImportSchedule|/app/src/App/Features/Schedule/ImportSchedule/Nested/ImportSchedule.cs", false, false, false)]
    [InlineData("Schedule|ImportSchedule|", false, false, false)]
    public void Condition_item_template_finds_the_slice_its_command_and_its_condition_in_the_files_of_the_slices(
        string probe, bool sliceExists, bool commandExists, bool conditionExists)
    {
        using var template = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(ConditionTemplateRoot(FindRepositoryRoot()), ".template.config", "template.json")));
        var symbols = template.RootElement.GetProperty("symbols");

        Matches(symbols, "SliceExists", probe).ShouldBe(sliceExists);
        Matches(symbols, "CommandExists", probe).ShouldBe(commandExists);
        Matches(symbols, "ConditionExists", probe).ShouldBe(conditionExists);
    }

    // PB-094: --at-start, with --every or --daily-at, chains AlsoAtStart() to the schedule that the template prints and generates the
    // test of the cycle at start, at TestInstants.Start. It is refused without a schedule.
    [Fact]
    public void Command_item_template_sends_a_recurring_command_at_start_too_and_refuses_it_without_a_schedule()
    {
        var root = CommandTemplateRoot(FindRepositoryRoot());
        using var template = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".template.config", "template.json")));
        using var host = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".template.config", "dotnetcli.host.json")));
        var config = template.RootElement;
        var symbols = config.GetProperty("symbols");

        symbols.GetProperty("AtStart").GetProperty("datatype").GetString().ShouldBe("bool");
        host.RootElement.GetProperty("symbolInfo").GetProperty("AtStart").GetProperty("longName").GetString().ShouldBe("at-start");
        symbols.GetProperty("AtStartWithoutSchedule").GetProperty("value").GetString().ShouldBe("(AtStart && !ScheduleIsGiven)");
        symbols.GetProperty("Accepted").GetProperty("value").GetString()!.ShouldEndWith(" && !AtStartWithoutSchedule)");
        var postActions = config.GetProperty("postActions").EnumerateArray().ToArray();
        postActions.Single(action => action.GetProperty("id").GetString() == "refuseAtStartWithoutSchedule")
            .GetProperty("condition").GetString().ShouldBe("(AtStartWithoutSchedule)");
        var registrations = postActions.Single(action => action.GetProperty("id").GetString() == "registerSlice")
            .GetProperty("manualInstructions").EnumerateArray()
            .ToDictionary(instruction => instruction.GetProperty("condition").GetString()!, instruction => instruction.GetProperty("text").GetString()!);
        registrations.Keys.Where(condition => condition.StartsWith("(EveryIsGiven", StringComparison.Ordinal) || condition.StartsWith("(DailyAtIsGiven", StringComparison.Ordinal))
            .ShouldAllBe(condition => condition.EndsWith(" && AtStart)", StringComparison.Ordinal) || condition.EndsWith(" && !AtStart)", StringComparison.Ordinal));
        var atStart = registrations.Where(registration => registration.Key.EndsWith(" && AtStart)", StringComparison.Ordinal)).ToArray();
        atStart.Length.ShouldBe(8);
        atStart.ShouldAllBe(registration => registration.Value.Contains(".AlsoAtStart()") && registration.Value.Contains("ModuleName:CommandName:AtStart of the configuration"));
        registrations.Where(registration => registration.Key.EndsWith(" && !AtStart)", StringComparison.Ordinal)).Count().ShouldBe(8);
        registrations.Where(registration => !registration.Key.EndsWith(" && AtStart)", StringComparison.Ordinal)).ShouldAllBe(registration => !registration.Value.Contains("AlsoAtStart"));
        registrations["(EveryIsGiven && !RetryIsGiven && ResponseIsId && AtStart)"].ShouldContain(
            "            .AddRecurringCommand<CommandNameCommand, Guid>(\"ModuleName:CommandName\", RecurringSchedule.Every(TimeSpan.Parse(\"RecurringInterval\", CultureInfo.InvariantCulture)).AlsoAtStart())\n");
        registrations["(DailyAtIsGiven && RetryIsGiven && ResponseIsDto && AtStart)"].ShouldContain(
            "            .AddRecurringCommand<CommandNameCommand, ResponseTypeName>(\"ModuleName:CommandName\", RecurringSchedule.DailyAt(new TimeOnly(RecurringHour, RecurringMinute), \"RecurringTimeZone\")"
            + ".AlsoAtStart().WithRetries(TimeSpan.Parse(\"RecurringDelayOfRetries\", CultureInfo.InvariantCulture), RecurringCountOfRetries))\n");

        var tests = File.ReadAllText(Path.Combine(root, "tests", "MyApp.IntegrationTests", "Features", "ModuleName", "CommandName", "CommandNameTests.cs"));
        tests.ShouldContain("    public async Task Runs_a_cycle_at_start_before_the_first_instant_of_its_schedule()");
        tests.ShouldContain("        var cycles = new RecurringCommandCycles<CommandNameCommand, Guid>(app, start: TestInstants.Start);");
        tests.ShouldContain("        (await cycles.NextDueAsync()).ShouldBe(TestInstants.Start);");
        Regex.Count(tests, Regex.Escape("#if (AtStart)\n\n        // The cycle at start comes first, at TestInstants.Start")).ShouldBe(2);
    }

    // PB-094: RecurringCommandCycles starts the application of its cycles at start, TestInstants.Start by default, so that a test of
    // the cycle at start never moves Clock before the first use of Services. The agent kit names hardslice-condition and --at-start.
    [Fact]
    public void Recurring_command_cycles_start_at_a_chosen_instant_and_the_agent_kit_generates_conditions_and_cycles_at_start()
    {
        var template = TemplateRoot(FindRepositoryRoot());
        var cycles = File.ReadAllText(Path.Combine(template, "tests", "MyApp.IntegrationTests", "RecurringCommandCycles.cs"));
        var agents = File.ReadAllText(Path.Combine(template, "AGENTS.md"));
        var addSlice = File.ReadAllText(Path.Combine(template, ".claude", "skills", "add-slice", "SKILL.md"));

        cycles.ShouldContain("public RecurringCommandCycles(AppFactory app, Action<IWebHostBuilder>? configure = null, DateTimeOffset? start = null)");
        cycles.ShouldContain("        Clock = new FakeTimeProvider(start ?? TestInstants.Start);");
        cycles.ShouldContain("Clock is\n    // set to it before the host starts, so a test never moves Clock with SetUtcNow before the first use of Services.");
        agents.ShouldContain("`dotnet new hardslice-condition --project src/MyApp/MyApp.csproj --module <Module> --command <Slice>` generates it with its tests, for a new or an existing command.");
        agents.ShouldContain("which `--every`, `--daily-at`, `--retry` and `--at-start` (`AlsoAtStart()`, also sent once at start) print");
        addSlice.ShouldContain("dotnet new hardslice-condition --project src/MyApp/MyApp.csproj --module <Module> --command <Slice>\n");
        addSlice.ShouldContain("With either, `--at-start` also sends the command once when the application starts, before the first instant of its schedule");
        addSlice.ShouldContain("An existing command without parameter gets its schedule the same way. No template writes that line: test its cycles with `RecurringCommandCycles`");
        addSlice.ShouldContain("`TestInstants.Start` unless the third argument of `RecurringCommandCycles`, `start`, gives another instant");
    }

    // PB-087: hardslice-aggregate maps a concurrency token on xmin, AppDbContext saves the root of a changed child so that the
    // token of the root covers it, and the agent kit tells a creation under a unique key, a concurrent change and a merge apart.
    [Fact]
    public void Aggregates_get_a_concurrency_token_and_the_agent_kit_tells_the_three_concurrent_cases_apart()
    {
        var repositoryRoot = FindRepositoryRoot();
        var template = TemplateRoot(repositoryRoot);
        var mapping = File.ReadAllText(Path.Combine(AggregateTemplateRoot(repositoryRoot), "src", "MyApp", "Features", "ModuleName", "AggregateNameConfiguration.cs"));
        var aggregate = File.ReadAllText(Path.Combine(AggregateTemplateRoot(repositoryRoot), "src", "MyApp.Domain", "ModuleName", "AggregateName.cs"));
        var context = File.ReadAllText(Path.Combine(template, "src", "MyApp", "Shared", "Persistence", "AppDbContext.cs"));
        var agents = File.ReadAllText(Path.Combine(template, "AGENTS.md"));
        var addSlice = File.ReadAllText(Path.Combine(template, ".claude", "skills", "add-slice", "SKILL.md"));
        var addAggregate = File.ReadAllText(Path.Combine(template, ".claude", "skills", "add-aggregate", "SKILL.md"));

        mapping.ShouldContain("        builder.Property<uint>(\"Version\").IsRowVersion();\n");
        mapping.ShouldContain("TransactionBehavior answers it with 409");
        aggregate.ShouldNotContain("Version");
        context.ShouldContain("SaveRootsOfChangedChildren();\n        return base.SaveChanges(acceptAllChangesOnSuccess);");
        context.ShouldContain("SaveRootsOfChangedChildren();\n        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);");
        context.ShouldContain("foreignKey.PrincipalToDependent is not null");
        context.ShouldContain("root.State = EntityState.Modified;");
        context.ShouldContain("but its mapping has no column besides its key and its concurrency token");

        agents.ShouldContain("Concurrent commands fall in three cases, as `add-slice` details: one that creates an aggregate under a unique key relies on its unique index, whose violation the pipeline answers with 409; one that changes a stored aggregate, its root or only a child, relies on the concurrency token `Version` on `xmin` that `hardslice-aggregate` maps, and a change saved by another request since the load is answered with 409 too, for the caller or the next cycle to retry; a merge that must never fail takes a transaction lock.");
        addSlice.ShouldNotContain("the index and the 409 cover the others");
        addSlice.ShouldContain("keep it for a merge that must never fail, as the next item says;");
        addSlice.ShouldContain("three cases, each with its own protection. A creation under a unique key relies on the unique index and its 409");
        addSlice.ShouldContain("A change of a stored aggregate relies on its concurrency token, the shadow property `Version` on `xmin` that `dotnet new hardslice-aggregate` maps");
        addSlice.ShouldContain("A merge that must never fail, such as a collection that enriches what a concurrent run stored and would lose an observation on a 409, takes the transaction lock");
        addSlice.ShouldContain("[Concurrent changes](../add-aggregate/SKILL.md#concurrent-changes)");
        addAggregate.ShouldContain("## Concurrent changes\n");
        addAggregate.ShouldContain("Keep the line `builder.Property<uint>(\"Version\").IsRowVersion();`");
        addAggregate.ShouldContain("Its column `xmin` is the concurrency token, which Npgsql never creates");
        addAggregate.ShouldContain("`TransactionBehavior` turns the `DbUpdateConcurrencyException` into a `ConflictException`, answered with 409");
        addAggregate.ShouldContain("The token of the root covers its children held in its collection.");
        addAggregate.ShouldContain("The token never covers a creation");
        addAggregate.ShouldContain("- Remove the concurrency token `Version` from a mapping");
    }

    // PB-093: hardslice-aggregate generates the test of the model that holds its concurrency token, which passes as generated, the
    // sample has the same test for Booking, and add-aggregate says how an aggregate mapped before its token gets the token, its
    // migration and that test. The pack copies add-aggregate into .agents/skills/ as is, which the test of the pack checks.
    [Fact]
    public void Aggregates_get_the_test_of_their_concurrency_token_and_the_agent_kit_says_how_a_mapped_aggregate_gets_it()
    {
        var repositoryRoot = FindRepositoryRoot();
        var template = TemplateRoot(repositoryRoot);
        var generated = File.ReadAllText(Path.Combine(AggregateTemplateRoot(repositoryRoot), "tests", "MyApp.IntegrationTests", "Features", "ModuleName",
            "AggregateNameConfigurationTests.cs"));
        var sample = File.ReadAllText(Path.Combine(template, "tests", "MyApp.IntegrationTests", "Features", "Bookings", "BookingConfigurationTests.cs"));
        var addAggregate = File.ReadAllText(Path.Combine(template, ".claude", "skills", "add-aggregate", "SKILL.md"));
        string[] assertions =
        [
            "        version.IsShadowProperty().ShouldBeTrue();\n",
            "        version.ClrType.ShouldBe(typeof(uint));\n",
            "        version.IsConcurrencyToken.ShouldBeTrue();\n",
            "        version.ValueGenerated.ShouldBe(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate);\n",
            "        version.GetColumnName().ShouldBe(\"xmin\");\n",
        ];

        generated.ShouldContain("namespace MyApp.IntegrationTests.Features.ModuleName;");
        generated.ShouldContain("public sealed class AggregateNameConfigurationTests(PostgresDatabase database)");
        generated.ShouldContain("    public async Task Maps_the_Version_of_AggregateName_on_xmin_as_its_concurrency_token()");
        generated.ShouldContain("db.Model.FindEntityType(typeof(AggregateName))?.FindProperty(\"Version\");");
        generated.ShouldNotContain("TODO(hardslice):");
        sample.ShouldContain("    public async Task Maps_the_Version_of_Booking_on_xmin_as_its_concurrency_token()");
        sample.ShouldContain("db.Model.FindEntityType(typeof(Booking))?.FindProperty(\"Version\");");
        foreach (var assertion in assertions)
        {
            generated.ShouldContain(assertion);
            sample.ShouldContain(assertion);
            // The method of the skill sits in an item of a list, without its class: two spaces of the item instead of four.
            addAggregate.ShouldContain("\n" + assertion[2..]);
        }

        addAggregate.ShouldContain("and the integration test of its concurrency token in `tests/MyApp.IntegrationTests/Features/<Module>/<Aggregate>ConfigurationTests.cs`");
        addAggregate.ShouldContain("and `<Aggregate>ConfigurationTests` fails without it.");
        addAggregate.ShouldContain("- An aggregate mapped before its token, by hand or by an earlier template, gets the line `builder.Property<uint>(\"Version\").IsRowVersion();` in its mapping, then a migration of its own");
        addAggregate.ShouldContain("write above it the comment `// xmin is a system column of every PostgreSQL table: Npgsql generates no SQL for it, and this migration only records the concurrency token of <Aggregate>Configuration in the model.`, and add no SQL.");
        addAggregate.ShouldContain("Then add the test that the template generates for a new aggregate");
        addAggregate.ShouldContain("  public async Task Maps_the_Version_of_<Aggregate>_on_xmin_as_its_concurrency_token()");
        addAggregate.ShouldContain("The integration test of the 409 comes with the first command that changes the aggregate, as `add-slice` says");
    }

    // PB-081: --require <policy> protects the endpoint of a command or a query: it chains .RequireAuthorization("<policy>"), the tests
    // send the client of the policy that AppFactory holds, except a test of the 401 that sends none. Without the option, the tests keep
    // app.CreateClient(), and a command refuses it with --endpoint none or --every, which have no endpoint.
    [Theory]
    [InlineData("hardslice-command", 5, 2)]
    [InlineData("hardslice-query", 3, 3)]
    public void Slice_item_templates_protect_their_endpoint_with_the_policy_of_require(string templateName, int endpoints, int unauthorizedTests)
    {
        var repository = FindRepositoryRoot();
        var root = templateName == "hardslice-command" ? CommandTemplateRoot(repository) : QueryTemplateRoot(repository);
        var name = templateName == "hardslice-command" ? "CommandName" : "QueryName";
        using var template = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".template.config", "template.json")));
        using var host = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".template.config", "dotnetcli.host.json")));
        var config = template.RootElement;
        var symbols = config.GetProperty("symbols");

        symbols.GetProperty("Require").GetProperty("datatype").GetString().ShouldBe("text");
        symbols.GetProperty("Require").TryGetProperty("defaultValue", out _).ShouldBeFalse();
        host.RootElement.GetProperty("symbolInfo").GetProperty("Require").GetProperty("longName").GetString().ShouldBe("require");
        symbols.GetProperty("RequireOrUnbound").GetProperty("replaces").GetString().ShouldBe("AuthorizationPolicyName");
        symbols.GetProperty("RequireOrUnbound").GetProperty("fileRename").GetString().ShouldBe("AuthorizationPolicyName");
        symbols.GetProperty("Accepted").GetProperty("value").GetString()!.ShouldContain("RequireIsValid");
        foreach (var (given, value, matches) in new[] { ("RequireIsGiven", "", false), ("RequireIsGiven", "AdminKey", true) })
            Matches(symbols, given, value).ShouldBe(matches);

        // The client of the tests: the policy's own with --require, the anonymous one without, at the same call sites.
        var client = symbols.GetProperty("TestClient");
        client.GetProperty("replaces").GetString().ShouldBe("app.CreateClient().");
        client.GetProperty("parameters").GetProperty("source").GetString().ShouldBe("RequireOrUnbound");
        var steps = client.GetProperty("parameters").GetProperty("steps").EnumerateArray()
            .Select(step => (Regex: step.GetProperty("regex").GetString()!, Replacement: step.GetProperty("replacement").GetString()!))
            .ToArray();
        string Client(string policy) => steps.Aggregate(policy, (value, step) => Regex.Replace(value, step.Regex, step.Replacement));
        Client("AdminKey").ShouldBe("app.CreateAuthorizedClient(\"AdminKey\").");
        Client("").ShouldBe("app.CreateClient().");

        var postActions = config.GetProperty("postActions").EnumerateArray().ToArray();
        postActions.Single(action => action.GetProperty("id").GetString() == "refuseInvalidPolicy").GetProperty("condition").GetString().ShouldBe("(!RequireIsValid)");
        var instruction = postActions.Single(action => action.GetProperty("id").GetString() == "requirePolicy");
        instruction.GetProperty("condition").GetString().ShouldBe("(Accepted && RequireIsGiven)");
        instruction.GetProperty("manualInstructions")[0].GetProperty("text").GetString()!.ShouldContain(
            "Its integration tests send their requests through app.CreateAuthorizedClient(\"AuthorizationPolicyName\"), which fails until AuthorizedClients, in tests/MyApp.IntegrationTests/AppFactory.cs, holds the client of that policy");
        Array.IndexOf(postActions, instruction).ShouldBe(postActions.Length - 1);

        var slice = File.ReadAllText(Path.Combine(root, "src", "MyApp", "Features", "ModuleName", name, $"{name}.cs"));
        Regex.Matches(slice, "\n            \\.RequireAuthorization\\(\"AuthorizationPolicyName\"\\);\n").Count.ShouldBe(endpoints);
        var tests = File.ReadAllText(Path.Combine(root, "tests", "MyApp.IntegrationTests", "Features", "ModuleName", name, $"{name}Tests.cs"));
        Regex.Matches(tests, "public async Task Rejects_a_request_without_authentication_before_its_handler\\(\\)").Count.ShouldBe(unauthorizedTests);
        Regex.Matches(tests, "var anonymous = app\\.CreateClient\\(\\);").Count.ShouldBe(unauthorizedTests);
        tests.ShouldContain("response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);");
        tests.ShouldNotContain("CreateAuthorizedClient");

        if (templateName != "hardslice-command")
            return;
        symbols.GetProperty("RequireWithoutEndpoint").GetProperty("value").GetString().ShouldBe("(RequireIsGiven && EndpointIsNone)");
        symbols.GetProperty("EndpointIsNone").GetProperty("value").GetString().ShouldBe("(Endpoint == \"none\" || ScheduleIsGiven)");
        symbols.GetProperty("Accepted").GetProperty("value").GetString()!.ShouldContain("!RequireWithoutEndpoint");
        postActions.Single(action => action.GetProperty("id").GetString() == "refuseRequireWithoutEndpoint").GetProperty("condition").GetString()
            .ShouldBe("(RequireWithoutEndpoint)");
    }

    // PB-081: the policy is written in a string of the endpoint and in the printed lines: a name the code could not hold is refused.
    [Theory]
    [InlineData("AdminKey", true)]
    [InlineData("admin-key", true)]
    [InlineData("Admin.Key", true)]
    [InlineData("scope:write", true)]
    [InlineData("Admin_Key2", true)]
    [InlineData("", true)]
    [InlineData("admin key", false)]
    [InlineData("admin\"key", false)]
    [InlineData("admin\\key", false)]
    [InlineData("2fa", false)]
    [InlineData("-admin", false)]
    [InlineData("{admin}", false)]
    public void Slice_item_templates_accept_a_policy_name_that_a_string_of_the_endpoint_can_hold(string policy, bool accepted)
    {
        var repository = FindRepositoryRoot();
        foreach (var root in new[] { CommandTemplateRoot(repository), QueryTemplateRoot(repository) })
        {
            using var template = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".template.config", "template.json")));

            Matches(template.RootElement.GetProperty("symbols"), "RequireIsValid", policy).ShouldBe(accepted);
        }
    }

    // PB-081: AppFactory, which the application owns, holds the client of each authorization policy, filled once, and names where to
    // add a missing one.
    [Fact]
    public void AppFactory_holds_the_client_of_each_authorization_policy()
    {
        var appFactory = File.ReadAllText(Path.Combine(TemplateRoot(FindRepositoryRoot()), "tests", "MyApp.IntegrationTests", "AppFactory.cs"));

        appFactory.ShouldContain("internal static readonly Dictionary<string, Action<HttpClient>> AuthorizedClients = new(StringComparer.OrdinalIgnoreCase)\n    {\n    };");
        // An extension of WebApplicationFactory<Program>: an instance derived with WithWebHostBuilder, as Ghostline's tests use, has it too.
        appFactory.ShouldContain("public static HttpClient CreateAuthorizedClient(this WebApplicationFactory<Program> app, string policy)");
        appFactory.ShouldContain("$\"AppFactory has no client for the authorization policy '{policy}'. Add one to AuthorizedClients, in AppFactory.cs of the \"");
        appFactory.ShouldNotContain("#if");
    }

    // PB-081: add-slice says how to protect an endpoint, where the scheme and the policy go, and how its tests authenticate.
    [Fact]
    public void Add_slice_says_how_to_protect_an_endpoint()
    {
        var addSlice = File.ReadAllText(Path.Combine(TemplateRoot(FindRepositoryRoot()), ".claude", "skills", "add-slice", "SKILL.md"));

        addSlice.ShouldContain("its endpoint protected by an authorization policy when only some callers may call it");
        addSlice.ShouldContain("see [Protected endpoints](#protected-endpoints)");
        var start = addSlice.IndexOf("\n## Protected endpoints\n", StringComparison.Ordinal);
        start.ShouldBeGreaterThan(0);
        var section = addSlice[start..addSlice.IndexOf("\n## Never\n", start, StringComparison.Ordinal)];
        section.ShouldContain("they live in `src/MyApp/Shared/Http/`, registered from `Program.cs` (HS017)");
        section.ShouldContain("on the group of the module, in `<Module>Module.cs`, when it covers every endpoint of the module, and on the endpoint, in its slice");
        section.ShouldContain("dotnet new hardslice-command --project src/MyApp/MyApp.csproj --module <Module> --name <Slice> --route <route> --require <policy>");
        section.ShouldContain("`--require` is refused with `--endpoint none`, `--every` and `--daily-at`");
        section.ShouldContain("an entry of `AuthorizedClients` named after the policy");
        // PB-092: the generated literal gives way to the constant of the application, so that a renamed policy keeps one name. The pack
        // copies add-slice into .agents/skills/ as is, which the test of the pack checks.
        section.ShouldContain(
            "`--require` writes the name of the policy as a literal, in the endpoint and in the tests: when the application names its policy in a "
            + "constant, such as `AdminKey.Policy` in `Shared/Http`, replace each literal with that constant, `.RequireAuthorization(AdminKey.Policy)` "
            + "and `app.CreateAuthorizedClient(AdminKey.Policy)`, with the `using` of its namespace, so that the policy keeps one name.");
        section.ShouldContain("`docs/rules/HS015.md`");
        addSlice.ShouldNotContain("add-authorization");
    }

    // PB-056: a DTO named Guid, or after a type of the slice, would hide that type and break the build of the slice (CS0118, CS0102).
    [Theory]
    [InlineData("Rename", "Guid", false)]
    [InlineData("Rename", "Rename", false)]
    [InlineData("Rename", "RenameCommand", false)]
    [InlineData("Rename", "RenameHandler", false)]
    [InlineData("Rename", "RenameValidator", false)]
    [InlineData("Rename", "RenameEndpoint", false)]
    [InlineData("Rename", "RenameResult", true)]
    [InlineData("Rename", "ProductName", true)]
    public void Command_item_template_refuses_a_response_that_hides_a_type_of_the_slice(string name, string response, bool accepted)
    {
        using var template = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(CommandTemplateRoot(FindRepositoryRoot()), ".template.config", "template.json")));
        var symbols = template.RootElement.GetProperty("symbols");

        var hidden = Matches(symbols, "ResponseIsReserved", response) || Matches(symbols, "ResponseIsSliceType", $"{name}|{response}");

        hidden.ShouldBe(!accepted);
        symbols.GetProperty("ResponseIsAccepted").GetProperty("value").GetString()!.ShouldContain("!ResponseIsReserved && !ResponseIsSliceType");
        symbols.GetProperty("Accepted").GetProperty("value").GetString()!.ShouldContain("ResponseIsAccepted");
    }

    // PB-065: --route takes the words and the route parameters of ASP.NET Core, relative to the group of the module, and / for the group
    // itself; the slice templates refuse the rest, as hardslice-module does for its group.
    [Theory]
    [InlineData("versions", true)]
    [InlineData("schedule/versions", true)]
    [InlineData("meeting-rooms", true)]
    [InlineData("", true)]
    [InlineData("{id:guid}/extensions", true)]
    [InlineData("{roomId:guid}/bookings/{day:datetime}", true)]
    [InlineData("items/{page:int:min(1)}", true)]
    [InlineData("items/{slug?}", true)]
    [InlineData("Versions", false)]
    [InlineData("versions/", false)]
    [InlineData("schedule//versions", false)]
    [InlineData("meeting rooms", false)]
    [InlineData("-versions", false)]
    [InlineData("{id", false)]
    [InlineData("{1id}", false)]
    [InlineData("{id:Guid}", false)]
    [InlineData("versions?date=today", false)]
    public void Slice_item_templates_accept_a_route_of_words_and_route_parameters(string route, bool accepted)
    {
        var repository = FindRepositoryRoot();
        foreach (var root in new[] { CommandTemplateRoot(repository), QueryTemplateRoot(repository) })
        {
            using var template = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".template.config", "template.json")));
            var symbols = template.RootElement.GetProperty("symbols");

            Matches(symbols, "RouteIsValid", route).ShouldBe(accepted);
            symbols.GetProperty("SliceRoute").GetProperty("parameters").GetProperty("source").GetString().ShouldBe("RouteOrDefault");
            symbols.GetProperty("Accepted").GetProperty("value").GetString()!.ShouldContain("RouteIsValid && !RouteHidesId");
        }
    }

    // PB-065: a single result and a DELETE append /{id:guid} to --route, which must not name id already: ASP.NET Core refuses a route
    // parameter twice when it maps the endpoint.
    [Theory]
    [InlineData("{id}/history", true)]
    [InlineData("{Id:guid}", true)]
    [InlineData("{id?}", true)]
    [InlineData("{identifier}/history", false)]
    [InlineData("{roomId:guid}", false)]
    [InlineData("ids", false)]
    public void Slice_item_templates_refuse_a_route_that_names_the_id_they_append(string route, bool named)
    {
        var repository = FindRepositoryRoot();
        using var command = JsonDocument.Parse(File.ReadAllText(Path.Combine(CommandTemplateRoot(repository), ".template.config", "template.json")));
        using var query = JsonDocument.Parse(File.ReadAllText(Path.Combine(QueryTemplateRoot(repository), ".template.config", "template.json")));

        Matches(command.RootElement.GetProperty("symbols"), "RouteNamesId", route).ShouldBe(named);
        Matches(query.RootElement.GetProperty("symbols"), "RouteNamesId", route).ShouldBe(named);
        command.RootElement.GetProperty("symbols").GetProperty("RouteHidesId").GetProperty("value").GetString().ShouldBe("(ReadsIdFromRoute && RouteNamesId)");
        command.RootElement.GetProperty("symbols").GetProperty("ReadsIdFromRoute").GetProperty("value").GetString().ShouldBe("(MethodIsDelete && !ParametersIsNone)");
        query.RootElement.GetProperty("symbols").GetProperty("RouteHidesId").GetProperty("value").GetString().ShouldBe("(ResultIsSingle && RouteNamesId)");
    }

    // PB-065: a command answers POST by default, PUT, PATCH or DELETE, a query GET only, in any case; the endpoint and the test follow.
    [Fact]
    public void Slice_item_templates_map_and_call_the_method_of_their_use_case()
    {
        var repository = FindRepositoryRoot();
        using var command = JsonDocument.Parse(File.ReadAllText(Path.Combine(CommandTemplateRoot(repository), ".template.config", "template.json")));
        using var query = JsonDocument.Parse(File.ReadAllText(Path.Combine(QueryTemplateRoot(repository), ".template.config", "template.json")));
        var commandSymbols = command.RootElement.GetProperty("symbols");
        var querySymbols = query.RootElement.GetProperty("symbols");

        foreach (var symbols in new[] { commandSymbols, querySymbols })
        {
            symbols.GetProperty("Route").TryGetProperty("defaultValue", out _).ShouldBeFalse();
            symbols.GetProperty("HttpMethod").GetProperty("generator").GetString().ShouldBe("casing");
            symbols.GetProperty("HttpMethod").GetProperty("parameters").GetProperty("toLower").GetBoolean().ShouldBeFalse();
        }
        // POST by default, without defaultValue: --endpoint none refuses a --method that was passed (PB-067).
        commandSymbols.GetProperty("Method").TryGetProperty("defaultValue", out _).ShouldBeFalse();
        commandSymbols.GetProperty("DefaultMethod").GetProperty("parameters").GetProperty("value").GetString().ShouldBe("POST");
        commandSymbols.GetProperty("HttpMethod").GetProperty("parameters").GetProperty("source").GetString().ShouldBe("MethodOrDefault");
        commandSymbols.GetProperty("MethodIsAccepted").GetProperty("value").GetString().ShouldBe("(MethodIsPost || MethodIsPut || MethodIsPatch || MethodIsDelete)");
        commandSymbols.GetProperty("Accepted").GetProperty("value").GetString()!.ShouldContain("MethodIsAccepted");
        querySymbols.GetProperty("Method").GetProperty("defaultValue").GetString().ShouldBe("GET");
        querySymbols.GetProperty("Accepted").GetProperty("value").GetString()!.ShouldContain("MethodIsGet");
        foreach (var (symbol, replaced, values) in new[]
        {
            ("MapMethod", "MapPost", new[] { "MapPut", "MapPatch", "MapDelete", "MapPost" }),
            ("SendJson", "PostAsJsonAsync", new[] { "PutAsJsonAsync", "PatchAsJsonAsync", "PostAsJsonAsync" }),
            ("SendWithoutBody", "PostAsync", new[] { "PutAsync", "PatchAsync", "PostAsync" }),
        })
        {
            var generated = commandSymbols.GetProperty(symbol);
            generated.GetProperty("replaces").GetString().ShouldBe(replaced);
            generated.GetProperty("parameters").GetProperty("cases").EnumerateArray().Select(@case => @case.GetProperty("value").GetString()).ShouldBe(values);
        }

        var slice = File.ReadAllText(Path.Combine(CommandTemplateRoot(repository), "src", "MyApp", "Features", "ModuleName", "CommandName", "CommandName.cs"));
        slice.ShouldContain("group.MapPost(\"/command-id-route\", async (Guid id, IMediator mediator, CancellationToken cancellationToken) =>");
        var commandTests = File.ReadAllText(Path.Combine(CommandTemplateRoot(repository), "tests", "MyApp.IntegrationTests", "Features", "ModuleName", "CommandName", "CommandNameTests.cs"));
        commandTests.ShouldContain("DeleteAsync($\"{Route}/{command.Id}\", TestContext.Current.CancellationToken)");
        commandTests.ShouldContain("DeleteAsync(Route, TestContext.Current.CancellationToken)");
        var querySlice = File.ReadAllText(Path.Combine(QueryTemplateRoot(repository), "src", "MyApp", "Features", "ModuleName", "QueryName", "QueryName.cs"));
        querySlice.ShouldContain("group.MapGet(\"/query-id-route\", ");
        querySlice.ShouldContain("group.MapGet(\"/query-route\", ");
    }

    // PB-065: the generated tests call the route of the slice under the constant Route of its module, the group of --route included,
    // and a route parameter left between braces is a marker to replace.
    [Theory]
    [InlineData("hardslice-command", "CommandName", "/command-route")]
    [InlineData("hardslice-query", "QueryName", "/query-route")]
    public void Slice_item_templates_test_the_route_of_the_slice_under_the_group_of_its_module(string template, string name, string token)
    {
        var root = Path.Combine(FindRepositoryRoot(), "src", "HardSlice.Templates", "content", template);
        var tests = File.ReadAllText(Path.Combine(root, "tests", "MyApp.IntegrationTests", "Features", "ModuleName", name, $"{name}Tests.cs"));
        using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".template.config", "template.json")));
        var symbols = config.RootElement.GetProperty("symbols");

        tests.ShouldContain("using MyApp.Features.ModuleName;\n");
        tests.ShouldContain("#if (RouteIsGroup)\n    private const string Route = ModuleNameModule.Route;\n#else\n"
            + $"    private const string Route = ModuleNameModule.Route + \"{token}\";\n#endif\n");
        tests.ShouldContain("#if (RouteHasParameters)\n    // TODO(hardslice): replace each route parameter between braces in Route with a value of the test");
        tests.ShouldNotContain("module-route");
        symbols.GetProperty("EndpointRoute").GetProperty("replaces").GetString().ShouldBe(token);
        symbols.TryGetProperty("ModuleRoute", out _).ShouldBeFalse();
    }

    // hardslice-query generates the two files of a query slice, at the paths of HS004 and HS018, and the tests of the validator of a
    // filtered query (PB-070), excluded from the other results, which have no validator.
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
            .ShouldBe(["single", "many", "filtered"]);
        symbols.GetProperty("Result").GetProperty("defaultValue").GetString().ShouldBe("single");
        var modifiers = config.GetProperty("sources")[0].GetProperty("modifiers");
        modifiers[1].GetProperty("condition").GetString().ShouldBe("(!ResultIsFiltered)");
        modifiers[1].GetProperty("exclude").EnumerateArray().Select(glob => glob.GetString()).ShouldBe(["tests/MyApp.Tests/**/*"]);
    }

    // PB-070: the DTO is named after the slice without its first word, the verb of the use case, for every result. A name of one word,
    // or whose first word is a single capital, is kept whole.
    [Theory]
    [InlineData("GetBooking", "BookingDto")]
    [InlineData("ListRoomBookings", "RoomBookingsDto")]
    [InlineData("GetScheduledTrips", "ScheduledTripsDto")]
    [InlineData("CountBookings", "BookingsDto")]
    [InlineData("Bookings", "BookingsDto")]
    [InlineData("URLStatus", "URLStatusDto")]
    public void Query_item_template_names_its_dto_without_the_verb_of_the_slice(string name, string dto)
    {
        var root = QueryTemplateRoot(FindRepositoryRoot());
        using var template = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".template.config", "template.json")));
        var symbol = template.RootElement.GetProperty("symbols").GetProperty("ResultDto");

        symbol.GetProperty("parameters").GetProperty("source").GetString().ShouldBe("name");
        symbol.GetProperty("parameters").GetProperty("steps").EnumerateArray()
            .Aggregate(name, (value, step) => Regex.Replace(
                value, step.GetProperty("regex").GetString()!, step.GetProperty("replacement").GetString()!))
            .ShouldBe(dto);
        symbol.GetProperty("replaces").GetString().ShouldBe("ResultDtoName");
        foreach (var file in QueryTemplateFiles.Where(file => file.EndsWith(".cs", StringComparison.Ordinal)))
            File.ReadAllText(Path.Combine(root, file)).ShouldNotContain("QueryNameDto");
    }

    // PB-070: --parameter of --result filtered is <Name>:<Type>, a PascalCase name and one of the listed types, written as in C#.
    [Theory]
    [InlineData("Date:DateOnly", true, true)]
    [InlineData("ServiceDate:DateOnly", true, true)]
    [InlineData("Name:string", true, true)]
    [InlineData("At:DateTimeOffset", true, true)]
    [InlineData("Count:int", true, true)]
    [InlineData("Id:Guid", true, true)]
    [InlineData("Date:DateTime", true, false)]
    [InlineData("Name:String", true, false)]
    [InlineData("Status:BookingStatus", true, false)]
    [InlineData("date:DateOnly", false, false)]
    [InlineData("Date", false, false)]
    [InlineData("Date:", false, false)]
    [InlineData("Date:DateOnly:Extra", false, false)]
    public void Query_item_template_accepts_a_filter_of_a_listed_type_only(string parameter, bool wellFormed, bool generated)
    {
        using var template = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(QueryTemplateRoot(FindRepositoryRoot()), ".template.config", "template.json")));
        var symbols = template.RootElement.GetProperty("symbols");
        var type = Regex.Replace(parameter, @"^[^:]*:", "");

        Matches(symbols, "ParameterIsWellFormed", parameter).ShouldBe(wellFormed);
        (wellFormed && Matches(symbols, "FilterTypeIsKnown", type)).ShouldBe(generated);
        symbols.GetProperty("Accepted").GetProperty("value").GetString()!
            .ShouldContain("!ParameterIsMissing && !ParameterIsUnused && !ParameterIsMalformed && !ParameterTypeIsUnknown");
    }

    // PB-070: each listed type has a value of its own in the tests, a C# expression and the text of the query string. The template
    // engine reads a value shaped as a date and time as a date: the one of DateTimeOffset is encoded so that it stays text.
    [Fact]
    public void Query_item_template_gives_every_listed_filter_type_a_test_value()
    {
        using var template = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(QueryTemplateRoot(FindRepositoryRoot()), ".template.config", "template.json")));
        var symbols = template.RootElement.GetProperty("symbols");
        string[] types = ["string", "bool", "int", "long", "decimal", "double", "Guid", "DateOnly", "TimeOnly", "DateTimeOffset"];

        symbols.GetProperty("FilterTypeIsKnown").GetProperty("parameters").GetProperty("pattern").GetString()
            .ShouldBe($"^(?:{string.Join('|', types)})$");
        foreach (var symbol in new[] { "FilterValue", "FilterQueryValue" })
        {
            var cases = symbols.GetProperty(symbol).GetProperty("parameters").GetProperty("cases").EnumerateArray().ToArray();
            cases.Take(types.Length).Select(entry => entry.GetProperty("condition").GetString())
                .ShouldBe(types.Select(type => $"(FilterType == \"{type}\")"));
            cases.Take(types.Length).ShouldAllBe(entry => entry.GetProperty("value").GetString()!.Length > 0);
        }
        symbols.GetProperty("FilterQueryValue").GetProperty("parameters").GetProperty("cases")[types.Length - 1]
            .GetProperty("value").GetString().ShouldBe("2026-11-04T08:30:00%2B00:00");
    }

    // Both results read the aggregate without tracking and project it to the DTO of the slice (HS006).
    [Fact]
    public void Query_item_template_reads_its_aggregate_without_tracking_in_both_results()
    {
        var slice = File.ReadAllText(
            Path.Combine(QueryTemplateRoot(FindRepositoryRoot()), "src", "MyApp", "Features", "ModuleName", "QueryName", "QueryName.cs"));

        Regex.Matches(slice, @"db\.Set<AggregateName>\(\)\s+\.AsNoTracking\(\)").Count.ShouldBe(2);
        Regex.Matches(slice, @"\.Select\(aggregate => new ResultDtoName\(").Count.ShouldBe(2);
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

        refusals.Length.ShouldBe(15);
        refusals.ShouldAllBe(action => !action.GetProperty("continueOnError").GetBoolean());
        refusals.Select(action => action.GetProperty("manualInstructions")[0].GetProperty("text").GetString()!)
            .ShouldAllBe(text => text.StartsWith("error: ") && text.EndsWith(" Nothing was created."));
        template.RootElement.GetProperty("sources")[0].GetProperty("modifiers")[0].GetProperty("condition").GetString().ShouldBe("(!Accepted)");
        // The lines to add, then, with --require, what its policy asks (PB-081): nothing else is printed.
        postActions.Where(action => !action.GetProperty("id").GetString()!.StartsWith("refuse", StringComparison.Ordinal))
            .Select(action => action.GetProperty("condition").GetString()).ShouldBe(["(Accepted)", "(Accepted && RequireIsGiven)"]);
    }

    // hardslice-module generates the contract of the module alone. dotnet new creates no empty folder and never deletes a file: a
    // .gitkeep in the folder of its domain or in a test folder would stay next to the files of the templates that fill it (PB-073).
    [Fact]
    public void Module_item_template_generates_the_contract_of_the_module_only()
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
        // PB-065: the route of the group is a constant of the contract, which the integration tests of the slice templates call under.
        contract.ShouldContain("    public const string Route = \"/module-route\";");
        contract.ShouldContain("        var group = endpoints.MapGroup(Route);");
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

        refusals.Length.ShouldBe(8);
        refusals.ShouldAllBe(action => !action.GetProperty("continueOnError").GetBoolean());
        refusals.Select(action => action.GetProperty("manualInstructions")[0].GetProperty("text").GetString()!)
            .ShouldAllBe(text => text.StartsWith("error: ") && text.EndsWith(" Nothing was created."));
        template.RootElement.GetProperty("sources")[0].GetProperty("modifiers")[0].GetProperty("condition").GetString().ShouldBe("(!Accepted)");
        postActions.Last().GetProperty("condition").GetString().ShouldBe("(Accepted)");
        postActions.Last().GetProperty("manualInstructions")[0].GetProperty("text").GetString()!
            .ShouldContain("dotnet ef migrations add AddAggregateTable --project src/MyApp --output-dir Shared/Persistence/Migrations");
    }

    // hardslice-event generates the event and, with --reaction, the reaction slice and its integration tests, at the paths of HS004 and HS018.
    [Fact]
    public void Event_item_template_generates_the_event_and_with_a_reaction_its_slice_and_its_integration_tests_only()
    {
        var root = EventTemplateRoot(FindRepositoryRoot());
        using var template = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".template.config", "template.json")));
        var config = template.RootElement;

        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ShouldBe(EventTemplateFiles.Order(StringComparer.Ordinal));
        config.GetProperty("shortName").GetString().ShouldBe("hardslice-event");
        config.GetProperty("tags").GetProperty("type").GetString().ShouldBe("item");
        config.GetProperty("sourceName").GetString().ShouldBe("EventName");
        var symbols = config.GetProperty("symbols");
        var application = symbols.GetProperty("AppName");
        application.GetProperty("type").GetString().ShouldBe("bind");
        application.GetProperty("binding").GetString().ShouldBe("msbuild:RootNamespace");
        application.GetProperty("replaces").GetString().ShouldBe("MyApp");
        symbols.GetProperty("Module").GetProperty("isRequired").GetBoolean().ShouldBeTrue();
        // The reaction is optional: without it, only the event is created.
        symbols.GetProperty("Reaction").TryGetProperty("isRequired", out _).ShouldBeFalse();
        symbols.GetProperty("ReactionModuleName").GetProperty("replaces").GetString().ShouldBe("ReactionModule");
        symbols.GetProperty("ReactionSliceName").GetProperty("replaces").GetString().ShouldBe("ReactionSlice");
        var modifiers = config.GetProperty("sources")[0].GetProperty("modifiers");
        modifiers[1].GetProperty("condition").GetString().ShouldBe("(!HasReaction)");
        modifiers[1].GetProperty("exclude").EnumerateArray().Select(glob => glob.GetString()).ShouldBe(["src/MyApp/**/*", "tests/**/*"]);
    }

    // The event is a sealed record (HS009); the reaction holds its handler alone (HS004), internal (HS016), which reaches neither
    // IMediator nor SaveChanges, and its test calls it through the container that must register it.
    [Fact]
    public void Event_item_template_declares_a_record_and_a_reaction_that_stays_in_the_transaction()
    {
        var root = EventTemplateRoot(FindRepositoryRoot());
        var domainEvent = File.ReadAllLines(Path.Combine(root, "src", "MyApp.Domain", "ModuleName", "Events", "EventName.cs"));
        var reaction = File.ReadAllLines(Path.Combine(root, "src", "MyApp", "Features", "ReactionModule", "ReactionSlice", "ReactionSlice.cs"));
        var test = File.ReadAllText(
            Path.Combine(root, "tests", "MyApp.IntegrationTests", "Features", "ReactionModule", "ReactionSlice", "ReactionSliceTests.cs"));

        domainEvent.ShouldContain("namespace MyApp.Domain.ModuleName.Events;");
        domainEvent.ShouldContain("public sealed record EventName(Guid Id) : IDomainEvent;");
        reaction.ShouldContain("namespace MyApp.Features.ReactionModule.ReactionSlice;");
        reaction.ShouldContain("internal sealed class ReactionSliceHandler : IDomainEventHandler<EventName>");
        reaction.Count(line => Regex.IsMatch(line, @"^(public|internal) ")).ShouldBe(1);
        reaction.ShouldNotContain(line => line.Contains("Endpoint", StringComparison.Ordinal) || line.Contains("IMediator ", StringComparison.Ordinal));
        string.Join('\n', reaction).ShouldContain("TODO(hardslice): implement ReactionSliceHandler through AppDbContext");
        test.ShouldContain("GetServices<IDomainEventHandler<EventName>>().OfType<ReactionSliceHandler>()");
        test.ShouldContain("Assert.Fail(\"TODO(hardslice): ");
    }

    // Every refusal fails the generation, after excluding every file, and says that nothing was created. A name that is not a past
    // fact only warns: HS019 is a suggestion. The last action prints the registration of the reaction, or how to raise the event.
    [Fact]
    public void Event_item_template_refuses_with_an_error_that_creates_nothing_and_warns_about_a_name_that_is_not_a_past_fact()
    {
        using var template = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(EventTemplateRoot(FindRepositoryRoot()), ".template.config", "template.json")));
        var postActions = template.RootElement.GetProperty("postActions").EnumerateArray().ToArray();
        var refusals = postActions.Where(action => action.GetProperty("id").GetString()!.StartsWith("refuse", StringComparison.Ordinal)).ToArray();

        refusals.Length.ShouldBe(10);
        refusals.ShouldAllBe(action => !action.GetProperty("continueOnError").GetBoolean());
        refusals.Select(action => action.GetProperty("manualInstructions")[0].GetProperty("text").GetString()!)
            .ShouldAllBe(text => text.StartsWith("error: ") && text.EndsWith(" Nothing was created."));
        template.RootElement.GetProperty("sources")[0].GetProperty("modifiers")[0].GetProperty("condition").GetString().ShouldBe("(!Accepted)");
        var warning = postActions.Single(action => action.GetProperty("id").GetString() == "warnNotPastFact");
        warning.GetProperty("condition").GetString().ShouldBe("(Accepted && !NameIsPastFact && !ReactionOnly)");
        warning.GetProperty("continueOnError").GetBoolean().ShouldBeTrue();
        warning.GetProperty("manualInstructions")[0].GetProperty("text").GetString()!.ShouldStartWith("warning: EventName is not named as a past fact: HS019 ");
        postActions.Last().GetProperty("condition").GetString().ShouldBe("(Accepted)");
        postActions.Last().GetProperty("manualInstructions").EnumerateArray()
            .Select(instruction => instruction.GetProperty("text").GetString()!)
            .ShouldContain(text => text.Contains("\n            .AddScoped<IDomainEventHandler<EventName>, ReactionSliceHandler>()\n", StringComparison.Ordinal));
    }

    // PB-056: an aggregate named after its module, a namespace of the application or a type that its files import would not build
    // (CS0118, CS0104): the template refuses it. dotnet new evaluates regexMatch with System.Text.RegularExpressions, as here.
    [Theory]
    [InlineData("Rooms", "Acme.Booking", "/app/src/Acme.Booking/Features/Rooms/RoomsModule.cs", false)]
    [InlineData("Room", "Acme.Booking", "/app/src/Acme.Booking/Features/Rooms/RoomsModule.cs", true)]
    [InlineData("Domain", "Acme.Booking", "", false)]
    [InlineData("Shared", "Acme.Booking", "", false)]
    [InlineData("Acme", "Acme.Booking", "", false)]
    [InlineData("Booking", "Acme.Booking", "", false)]
    [InlineData("Task", "Acme.Booking", "", false)]
    [InlineData("Guid", "Acme.Booking", "", false)]
    [InlineData("WorkItem", "Acme.Booking", "", true)]
    [InlineData("Product", "Acme.Booking", "/app/src/Acme.Booking/Features/Catalog/CatalogModule.cs", true)]
    public void Aggregate_item_template_refuses_a_name_that_hides_a_module_a_namespace_or_an_imported_type(
        string name, string application, string moduleFiles, bool accepted)
    {
        using var template = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(AggregateTemplateRoot(FindRepositoryRoot()), ".template.config", "template.json")));
        var symbols = template.RootElement.GetProperty("symbols");

        var hidden = Matches(symbols, "NameIsModule", $"{name}|{moduleFiles}")
            || Matches(symbols, "NameIsNamespace", $"{name}|{application}")
            || Matches(symbols, "NameIsImportedType", name);

        hidden.ShouldBe(!accepted);
        symbols.GetProperty("NameIsFree").GetProperty("value").GetString().ShouldBe("(!NameIsModule && !NameIsNamespace && !NameIsImportedType)");
        symbols.GetProperty("Accepted").GetProperty("value").GetString()!.ShouldContain("NameIsFree");
    }

    // PB-066: hardslice-entity generates a child entity, its identifier, its mapping next to the mapping of its root, the converter
    // of the identifier of the root and its unit tests. It reads the mappings of the web project, and creates the converter only
    // while the module has none for that root.
    [Fact]
    public void Entity_item_template_generates_the_child_its_identifier_its_mapping_the_converter_of_its_root_and_its_unit_tests_only()
    {
        var root = EntityTemplateRoot(FindRepositoryRoot());
        using var template = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".template.config", "template.json")));
        var config = template.RootElement;

        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ShouldBe(EntityTemplateFiles.Order(StringComparer.Ordinal));
        config.GetProperty("shortName").GetString().ShouldBe("hardslice-entity");
        config.GetProperty("tags").GetProperty("type").GetString().ShouldBe("item");
        config.GetProperty("sourceName").GetString().ShouldBe("EntityName");
        var symbols = config.GetProperty("symbols");
        symbols.GetProperty("AppName").GetProperty("binding").GetString().ShouldBe("msbuild:RootNamespace");
        symbols.GetProperty("MappingFiles").GetProperty("binding").GetString().ShouldBe("msbuild:HardSliceMappingFiles");
        symbols.GetProperty("Module").GetProperty("isRequired").GetBoolean().ShouldBeTrue();
        symbols.GetProperty("Aggregate").GetProperty("isRequired").GetBoolean().ShouldBeTrue();
        symbols.GetProperty("Aggregate").GetProperty("replaces").GetString().ShouldBe("AggregateName");
        var link = symbols.GetProperty("Link");
        link.GetProperty("defaultValue").GetString().ShouldBe("collection");
        link.GetProperty("choices").EnumerateArray().Select(choice => choice.GetProperty("choice").GetString()).ShouldBe(["collection", "id"]);
        symbols.GetProperty("TableOrDefault").GetProperty("replaces").GetString().ShouldBe("EntityTable");
        symbols.GetProperty("Collection").GetProperty("replaces").GetString().ShouldBe("EntityCollection");
        var modifiers = config.GetProperty("sources")[0].GetProperty("modifiers");
        modifiers[0].GetProperty("condition").GetString().ShouldBe("(!Accepted)");
        modifiers[1].GetProperty("condition").GetString().ShouldBe("(ConverterExists)");
        modifiers[1].GetProperty("exclude").EnumerateArray().Select(glob => glob.GetString())
            .ShouldBe(["src/MyApp/Features/ModuleName/AggregateNameIdConverter.cs"]);
        File.ReadAllText(Path.Combine(TemplateRoot(FindRepositoryRoot()), "Directory.Build.targets"))
            .ShouldContain("<HardSliceMappingFiles>");
    }

    // A child held by its root maps the collection of the root and the identifier of the root as a shadow property. A child of a
    // large collection holds that identifier, without navigation (HS008 knows no owner for it). Both store it with the converter.
    [Fact]
    public void Entity_item_template_maps_a_child_held_by_its_root_or_holding_the_identifier_of_its_root()
    {
        var root = EntityTemplateRoot(FindRepositoryRoot());
        var entity = File.ReadAllText(Path.Combine(root, "src", "MyApp.Domain", "ModuleName", "EntityName.cs"));
        var configuration = File.ReadAllText(Path.Combine(root, "src", "MyApp", "Features", "ModuleName", "EntityNameConfiguration.cs"));
        var converter = File.ReadAllText(Path.Combine(root, "src", "MyApp", "Features", "ModuleName", "AggregateNameIdConverter.cs"));

        entity.ShouldContain("public sealed class EntityName : IEntity\n");
        entity.ShouldNotContain(" set;");
        entity.ShouldNotContain(" init;");
        entity.ShouldNotContain("AggregateName AggregateName");
        Branch(entity, linkIsId: true).ShouldContain("    public AggregateNameId AggregateNameId { get; }");
        Branch(entity, linkIsId: true).ShouldContain("    public static EntityName Create(AggregateNameId aggregateNameId) => new(EntityNameId.New(), aggregateNameId);");
        Branch(entity, linkIsId: false).ShouldContain("    public static EntityName Create() => new(EntityNameId.New());");
        Branch(entity, linkIsId: false).ShouldNotContain("AggregateNameId");
        configuration.ShouldContain("internal sealed class EntityNameConfiguration : IEntityTypeConfiguration<EntityName>");
        configuration.ShouldContain("        builder.ToTable(\"EntityTable\");");
        Branch(configuration, linkIsId: true).ShouldContain("builder.Property(entity => entity.AggregateNameId).HasConversion<AggregateNameIdConverter>();");
        Branch(configuration, linkIsId: true).ShouldContain("            .WithMany()\n            .HasForeignKey(entity => entity.AggregateNameId)");
        Branch(configuration, linkIsId: false).ShouldContain("builder.Property<AggregateNameId>(\"AggregateNameId\").HasConversion<AggregateNameIdConverter>();");
        Branch(configuration, linkIsId: false).ShouldContain("            .WithMany(\"EntityCollection\")\n            .HasForeignKey(\"AggregateNameId\")");
        converter.ShouldContain(
            "internal sealed class AggregateNameIdConverter() : ValueConverter<AggregateNameId, Guid>(id => id.Value, value => new AggregateNameId(value));");
    }

    // Every refusal fails the generation, after excluding every file, and says that nothing was created. The last two actions print
    // what to complete for each link, the collection to add to the root for a held child, and the migration.
    [Fact]
    public void Entity_item_template_refuses_with_an_error_that_creates_nothing()
    {
        using var template = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(EntityTemplateRoot(FindRepositoryRoot()), ".template.config", "template.json")));
        var postActions = template.RootElement.GetProperty("postActions").EnumerateArray().ToArray();
        var refusals = postActions.Where(action => action.GetProperty("id").GetString()!.StartsWith("refuse", StringComparison.Ordinal)).ToArray();

        refusals.Length.ShouldBe(11);
        refusals.ShouldAllBe(action => !action.GetProperty("continueOnError").GetBoolean());
        refusals.Select(action => action.GetProperty("manualInstructions")[0].GetProperty("text").GetString()!)
            .ShouldAllBe(text => text.StartsWith("error: ") && text.EndsWith(" Nothing was created."));
        var completions = postActions[^2..];
        completions.Select(action => action.GetProperty("condition").GetString()).ShouldBe(["(Accepted && !LinkIsId)", "(Accepted && LinkIsId)"]);
        var held = completions[0].GetProperty("manualInstructions")[0].GetProperty("text").GetString()!;
        held.ShouldContain("\nprivate readonly List<EntityName> _entityCollection = [];\npublic IReadOnlyList<EntityName> EntityCollection => _entityCollection.AsReadOnly();\n");
        completions[1].GetProperty("manualInstructions")[0].GetProperty("text").GetString()!.ShouldNotContain("_entityCollection");
        completions.ShouldAllBe(action => action.GetProperty("manualInstructions")[0].GetProperty("text").GetString()!
            .Contains("dotnet ef migrations add AddEntityTable --project src/MyApp --output-dir Shared/Persistence/Migrations", StringComparison.Ordinal));
    }

    // The aggregate is known by its mapping in the folder of its module, a name already mapped there is refused, and the converter
    // of the root is created only while the module has none. dotnet new evaluates regexMatch with System.Text.RegularExpressions.
    [Theory]
    [InlineData("Schedule|ScheduleVersion|", false, false)]
    [InlineData("Schedule|ScheduleVersion|/app/src/A/Features/Schedule/ScheduleVersionConfiguration.cs;", true, false)]
    [InlineData("Schedule|ScheduleVersion|/app/src/A/Features/Schedule/StopConfiguration.cs;/app/src/A/Features/Schedule/ScheduleVersionConfiguration.cs;/app/src/A/Features/Schedule/ScheduleVersionIdConverter.cs", true, true)]
    [InlineData("Schedule|ScheduleVersion|C:\\app\\src\\A\\Features\\Schedule\\ScheduleVersionConfiguration.cs;C:\\app\\src\\A\\Features\\Schedule\\ScheduleVersionIdConverter.cs", true, true)]
    [InlineData("Schedule|ScheduleVersion|/app/src/A/Features/Realtime/ScheduleVersionConfiguration.cs;/app/src/A/Features/Realtime/ScheduleVersionIdConverter.cs", false, false)]
    [InlineData("Schedule|Version|/app/src/A/Features/Schedule/ScheduleVersionConfiguration.cs;/app/src/A/Features/Schedule/ScheduleVersionIdConverter.cs", false, false)]
    [InlineData("Schedule|ScheduleVersion|/app/src/A/Features/Schedule/Import/ScheduleVersionConfiguration.cs", false, false)]
    public void Entity_item_template_finds_the_aggregate_and_its_converter_in_the_mappings_of_its_module(string probe, bool aggregateExists, bool converterExists)
    {
        using var template = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(EntityTemplateRoot(FindRepositoryRoot()), ".template.config", "template.json")));
        var symbols = template.RootElement.GetProperty("symbols");

        Matches(symbols, "AggregateExists", probe).ShouldBe(aggregateExists);
        Matches(symbols, "ConverterExists", probe).ShouldBe(converterExists);
        Matches(symbols, "NameIsMapped", probe).ShouldBe(aggregateExists);
        symbols.GetProperty("Accepted").GetProperty("value").GetString()!.ShouldContain("AggregateExists && TableIsValid && NameIsFree && !NameIsMapped");
    }

    // The lines of <source> that the template keeps for --link id (<linkIsId>) or --link collection, the only condition of its files.
    private static string Branch(string source, bool linkIsId)
    {
        var kept = new List<string>();
        bool? inIdBranch = null;
        foreach (var line in source.Split('\n'))
        {
            switch (line.Trim())
            {
                case "#if (LinkIsId)":
                    inIdBranch = true;
                    break;
                case "#else":
                    inIdBranch = false;
                    break;
                case "#endif":
                    inIdBranch = null;
                    break;
                default:
                    if (inIdBranch is null || inIdBranch == linkIsId)
                        kept.Add(line);
                    break;
            }
        }
        return string.Join('\n', kept);
    }

    // PB-056: --reaction-only adds the reaction slice of an event that exists, which --force would overwrite with a blank event.
    [Fact]
    public void Event_item_template_adds_a_reaction_alone_to_an_event_that_exists()
    {
        var root = EventTemplateRoot(FindRepositoryRoot());
        using var template = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".template.config", "template.json")));
        using var host = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".template.config", "dotnetcli.host.json")));
        var config = template.RootElement;

        host.RootElement.GetProperty("symbolInfo").GetProperty("ReactionOnly").GetProperty("longName").GetString().ShouldBe("reaction-only");
        config.GetProperty("symbols").GetProperty("ReactionOnly").GetProperty("datatype").GetString().ShouldBe("bool");
        config.GetProperty("symbols").GetProperty("Accepted").GetProperty("value").GetString()!.ShouldContain("(HasReaction || !ReactionOnly)");
        var modifier = config.GetProperty("sources")[0].GetProperty("modifiers").EnumerateArray()
            .Single(candidate => candidate.GetProperty("condition").GetString() == "(ReactionOnly)");
        modifier.GetProperty("exclude").EnumerateArray().Select(glob => glob.GetString()).ShouldBe(["src/MyApp.Domain/**/*"]);
        var postActions = config.GetProperty("postActions").EnumerateArray().ToArray();
        postActions.ShouldContain(action => action.GetProperty("id").GetString() == "refuseReactionOnlyWithoutReaction");
        postActions.Single(action => action.GetProperty("id").GetString() == "warnNotPastFact")
            .GetProperty("condition").GetString()!.ShouldContain("!ReactionOnly");
        File.ReadAllText(Path.Combine(root, "tests", "MyApp.IntegrationTests", "Features", "ReactionModule", "ReactionSlice", "ReactionSliceTests.cs"))
            .ShouldContain("#if (ReactionOnly)");
        File.ReadAllText(Path.Combine(TemplateRoot(FindRepositoryRoot()), ".claude", "skills", "add-domain-event", "SKILL.md"))
            .ShouldContain("--reaction-only");
    }

    private static bool Matches(JsonElement symbols, string symbol, string source) =>
        Regex.IsMatch(source, symbols.GetProperty(symbol).GetProperty("parameters").GetProperty("pattern").GetString()!);

    // In the repository, the template holds the token in place of the version, in its reference to the analyzers and in its tool entry only.
    [Fact]
    public void Template_holds_the_version_token_where_it_pins_the_analyzers_and_the_tool()
    {
        var templateRoot = TemplateRoot(FindRepositoryRoot());
        var reference = File.ReadAllText(Path.Combine(templateRoot, "Directory.HardSlice.props"));
        var packages = File.ReadAllText(Path.Combine(templateRoot, "Directory.Packages.props"));
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(templateRoot, ".config", "dotnet-tools.json")));

        AnalyzerReferenceVersion(reference).ShouldBe(VersionToken);
        Regex.Count(reference, VersionToken).ShouldBe(1);
        packages.ShouldNotContain(VersionToken);
        XDocument.Parse(packages).Descendants().Select(element => (string?)element.Attribute("Include")).ShouldNotContain("HardSlice.Analyzers");
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

    // PB-060: the application owns Directory.Packages.props, which brings HardSlice.Analyzers from the protected Directory.HardSlice.props
    // by an unconditional import, and the tests of a generated solution can set and advance a FakeTimeProvider (docs/rules/HS010.md).
    [Fact]
    public void Template_imports_the_analyzers_from_a_protected_file_and_gives_its_tests_a_fake_clock()
    {
        var templateRoot = TemplateRoot(FindRepositoryRoot());
        var packages = XDocument.Load(Path.Combine(templateRoot, "Directory.Packages.props"));

        packages.Root!.Elements("Import").Select(import => ((string?)import.Attribute("Project"), (string?)import.Attribute("Condition")))
            .ShouldBe([("$(MSBuildThisFileDirectory)Directory.HardSlice.props", null)]);
        PackageVersions(Path.Combine(templateRoot, "Directory.Packages.props")).ShouldContainKey("Microsoft.Extensions.TimeProvider.Testing");
        foreach (var project in new[] { "MyApp.Tests", "MyApp.IntegrationTests" })
        {
            XDocument.Load(Path.Combine(templateRoot, "tests", project, $"{project}.csproj")).Descendants("PackageReference")
                .Select(reference => (string?)reference.Attribute("Include"))
                .ShouldContain("Microsoft.Extensions.TimeProvider.Testing", $"{project} does not reference the package of FakeTimeProvider.");
        }
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
        // Forbidden names protected files that the template does not ship, so that an agent never creates one.
        citedPaths
            .Except(["Directory.Solution.props"])
            .Where(path => !File.Exists(Path.Combine(templateRoot, path)) && !Directory.Exists(Path.Combine(templateRoot, path)))
            .ShouldBeEmpty("AGENTS.md cites paths that the generated solution does not contain.");
    }

    // PB-048: the pre-commit hook and the CI of the generated solution run the check script without argument, and the CI uses the
    // actions of the CI of this repository at the same pins, which its generated-workflow job reproduces on the generated solution.
    [Fact]
    public void Pre_commit_hook_and_CI_run_the_check_script_without_argument_with_the_pinned_actions_of_the_repository()
    {
        var repositoryRoot = FindRepositoryRoot();
        var templateRoot = TemplateRoot(repositoryRoot);
        var preCommit = File.ReadAllText(Path.Combine(templateRoot, ".githooks", "pre-commit"));
        var workflow = File.ReadAllText(Path.Combine(templateRoot, ".github", "workflows", "hardslice.yml"));
        var repositoryPins = ActionPins(File.ReadAllText(Path.Combine(repositoryRoot, ".github", "workflows", "ci.yml")));

        preCommit.ShouldStartWith("#!/usr/bin/env bash\n");
        preCommit.ShouldContain("check=\"$(dirname \"${BASH_SOURCE[0]}\")/../scripts/check.sh\"");
        preCommit.ShouldContain("if ! bash \"$check\"; then");
        Regex.Matches(workflow, @"^\s+run: (?<command>.+)$", RegexOptions.Multiline).Select(match => match.Groups["command"].Value.TrimEnd())
            .ShouldBe(["bash scripts/check.sh", "|"]);
        // PB-053: without shell: bash, a runner runs bash -e without pipefail, and a failing git diff would feed protected nothing.
        workflow.ShouldContain("\ndefaults:\n  run:\n    shell: bash\n");
        Regex.Matches(workflow, @"^\s+shell:", RegexOptions.Multiline).Count.ShouldBe(1, "Every run step of the CI of the template runs with shell: bash.");
        var templatePins = ActionPins(workflow);
        templatePins.Length.ShouldBe(Regex.Matches(workflow, "uses:").Count, "Every action of the CI of the template is pinned by its full commit SHA.");
        templatePins.Select(pin => pin.Action).Distinct().ShouldBe(["actions/checkout", "actions/setup-dotnet"]);
        templatePins.ShouldAllBe(pin => repositoryPins.Contains(pin), "The CI of the template must use the actions of the CI of the repository at the same pins.");
    }

    private static (string Action, string Sha, string Version)[] ActionPins(string workflow) =>
        Regex.Matches(workflow, @"uses: (?<action>[\w-]+/[\w-]+)@(?<sha>[0-9a-f]{40}) # (?<version>v[\d.]+)$", RegexOptions.Multiline)
            .Select(match => (match.Groups["action"].Value, match.Groups["sha"].Value, match.Groups["version"].Value))
            .ToArray();

    // docs/agents/support.md: Claude Code reads AGENTS.md only through an import once a CLAUDE.md exists, Gemini CLI through
    // @./AGENTS.md, Copilot reads it natively. CLAUDE.md lives in .claude/, which Copilot does not read: at the root, Copilot would
    // expand its import and load AGENTS.md twice. Copilot merges GEMINI.md too: each file names the CLI it is for before anything else.
    [Theory]
    [InlineData(".claude/CLAUDE.md", "For Claude Code only.", "@../AGENTS.md")]
    [InlineData("GEMINI.md", "For Gemini CLI only.", "@./AGENTS.md")]
    [InlineData(".github/copilot-instructions.md", "For GitHub Copilot only.", null)]
    public void Agent_instruction_file_names_its_CLI_first_and_imports_AGENTS_md_by_the_mechanism_of_that_CLI(
        string file, string firstSentence, string? import)
    {
        var templateRoot = TemplateRoot(FindRepositoryRoot());
        var lines = File.ReadAllLines(Path.Combine(templateRoot, file));
        var text = lines.Where(line => line.Length > 0 && !line.StartsWith('#')).ToArray();

        File.Exists(Path.Combine(templateRoot, "CLAUDE.md")).ShouldBeFalse("A CLAUDE.md at the root makes Copilot load AGENTS.md twice: it lives in .claude/.");

        text[0].ShouldStartWith(firstSentence);
        var imports = lines.Where(line => line.TrimStart().StartsWith('@')).ToArray();
        if (import is null)
            imports.ShouldBeEmpty($"{file} must not import AGENTS.md, which its CLI already reads.");
        else
            imports.ShouldBe([import]);
    }

    // The instruction files hold only what is specific to their CLI: a golden rule or a rule identifier belongs to AGENTS.md alone.
    [Theory]
    [MemberData(nameof(AgentInstructionFiles))]
    public void Agent_instruction_file_copies_no_golden_rule_of_AGENTS_md(string file)
    {
        var templateRoot = TemplateRoot(FindRepositoryRoot());
        var instructions = File.ReadAllText(Path.Combine(templateRoot, file));
        var goldenRules = File.ReadAllLines(Path.Combine(templateRoot, "AGENTS.md"))
            .SkipWhile(line => line != "## Golden rules")
            .Skip(1)
            .TakeWhile(line => !line.StartsWith("## ", StringComparison.Ordinal) && !line.StartsWith("<!--#", StringComparison.Ordinal))
            .Where(line => Regex.IsMatch(line, @"^\d+\. "))
            .ToArray();

        goldenRules.Length.ShouldBeGreaterThan(5);
        Regex.Matches(instructions, @"HS\d{3}").Select(match => match.Value).ShouldBeEmpty($"{file} names a HardSlice rule: rules belong to AGENTS.md.");
        goldenRules
            .SelectMany(rule => Regex.Split(Regex.Replace(rule, @"^\d+\. ", ""), @"(?<=[.:;])\s+"))
            .Select(sentence => sentence.Trim().TrimEnd('.', ':', ';'))
            .Where(sentence => sentence.Length >= 20)
            .Where(sentence => instructions.Contains(sentence, StringComparison.OrdinalIgnoreCase))
            .ShouldBeEmpty($"{file} copies golden rules of AGENTS.md.");
    }

    // PB-056: the Agent kit of AGENTS.md lists the skills once; an instruction file links to it instead of keeping a copy that drifts.
    [Theory]
    [MemberData(nameof(AgentInstructionFiles))]
    public void Agent_instruction_file_links_to_the_agent_kit_instead_of_listing_the_skills(string file)
    {
        var templateRoot = TemplateRoot(FindRepositoryRoot());
        var instructions = File.ReadAllText(Path.Combine(templateRoot, file));

        instructions.ShouldContain("AGENTS.md#agent-kit)");
        SkillNames.Where(skill => instructions.Contains(skill, StringComparison.Ordinal))
            .ShouldBeEmpty($"{file} lists skills that the Agent kit of AGENTS.md already lists.");
        if (file != "GEMINI.md")
            instructions.ShouldContain("Until it is restored, the hooks do nothing.", Case.Sensitive, $"{file} does not say that the hooks wait for the tool.");
    }

    // PB-065: the slice templates take the route of the slice from --route and call it under <Module>Module.Route, the group of a
    // module created with its own --route included: the agent kit asks for --route every time and no longer to change the tests.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Agent_guidance_asks_for_the_route_of_every_slice_and_no_change_of_the_route_of_its_tests(bool noSample)
    {
        var templateRoot = TemplateRoot(FindRepositoryRoot());
        var agents = string.Join('\n', AgentGuidance(File.ReadAllLines(Path.Combine(templateRoot, "AGENTS.md")), noSample));
        var addSlice = File.ReadAllText(Path.Combine(templateRoot, ".claude", "skills", "add-slice", "SKILL.md"));
        var addModule = File.ReadAllText(Path.Combine(templateRoot, ".claude", "skills", "add-module", "SKILL.md"));

        agents.ShouldContain("Always pass `--route`, named after the resource rather than the use case");
        addSlice.ShouldContain("Always pass `--route` to a slice with an endpoint, the route of the endpoint under the group of the module, named after the resource");
        addSlice.ShouldContain("--module <Module> --name <Slice> --route <route>");
        addSlice.ShouldContain("--aggregate <Aggregate> --route <route>");
        addSlice.ShouldContain("The generated tests call the route of the slice under `<Module>Module.Route`, the group of the module.");
        addModule.ShouldContain("The contract holds it in its constant `Route`");
        foreach (var text in new[] { agents, addSlice, addModule })
            text.ShouldNotContain("change `Route`");
    }

    // PB-073: no template ships a placeholder for an empty folder. The templates that fill a folder create it, and none deletes a
    // .gitkeep that an earlier template would have left there.
    [Fact]
    public void Templates_ship_no_placeholder_that_a_later_template_would_leave_behind()
    {
        var content = Path.Combine(FindRepositoryRoot(), "src", "HardSlice.Templates", "content");

        Directory.EnumerateFiles(content, ".gitkeep", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(content, file).Replace('\\', '/'))
            .ShouldBeEmpty();
    }

    // PB-073: the agent kit says that hardslice-module creates the contract alone, and that the templates of the aggregates and
    // slices create the other folders of the module: an agent never keeps an empty folder with a .gitkeep.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Agent_guidance_says_that_the_templates_of_the_module_create_its_folders(bool noSample)
    {
        var templateRoot = TemplateRoot(FindRepositoryRoot());
        var agents = string.Join('\n', AgentGuidance(File.ReadAllLines(Path.Combine(templateRoot, "AGENTS.md")), noSample));
        var addModule = File.ReadAllText(Path.Combine(templateRoot, ".claude", "skills", "add-module", "SKILL.md"));

        if (noSample)
            agents.ShouldContain("the public contract `src/MyApp/Features/<Module>/<Module>Module.cs` alone: the templates below create the folder of its domain and its test folders.");
        addModule.ShouldContain("`hardslice-module` creates the contract alone, and the templates of its aggregates and slices create the folders of its domain and of its tests as they fill them");
        addModule.ShouldContain("create an empty folder of the module ahead of its files, with a `.gitkeep` or otherwise");
    }

    // PB-063: a command without parameters has no validator (HS013) nor invalid request, and a command handler may read from outside
    // through the services of its module, but an effect that a rollback must cancel has no mechanism in the solution yet.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Agent_guidance_says_how_a_command_without_parameters_is_tested_and_what_a_command_handler_may_do_outside(bool noSample)
    {
        var templateRoot = TemplateRoot(FindRepositoryRoot());
        var agents = string.Join('\n', AgentGuidance(File.ReadAllLines(Path.Combine(templateRoot, "AGENTS.md")), noSample));
        var addSlice = File.ReadAllText(Path.Combine(templateRoot, ".claude", "skills", "add-slice", "SKILL.md"));

        agents.ShouldContain("an invalid request included for a command with parameters.");
        agents.ShouldContain("The tests of a command without parameters cover a failure of the domain, or its success, instead");
        agents.ShouldContain("`--parameters none` for a command without parameters");
        agents.ShouldContain("A command without parameters, an empty record, has none: never an empty validator;");
        agents.ShouldContain("A command handler may read from outside, such as a download or a file, through services its module registers, such as `IHttpClientFactory`");
        agents.ShouldContain("An effect it has outside survives a rollback, so it must stay harmless then");
        agents.ShouldContain("has no mechanism in this solution yet: tell the user instead of writing it.");
        addSlice.ShouldContain("- command without parameters, such as an import that reads its source from the configuration: generated with `--parameters none`, it is an empty record");
        addSlice.ShouldNotContain("delete the validator");
        addSlice.ShouldContain("A command without parameters has no invalid request: its integration tests assert its success, and a failure of the domain");
        addSlice.ShouldContain("- command that reads from outside, such as a download or a file: the handler injects what it needs, registered in `Add<Module>Module`");
        addSlice.ShouldContain("has no mechanism in this solution yet: stop and tell the user instead of writing it");
    }

    // PB-072: a command may save a root early to write its children in bulk after it, through the connection and the transaction of
    // AppDbContext, under immediate foreign keys and with a longer timeout for that command only. No bulk helper is generated.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Agent_guidance_says_when_a_command_writes_children_in_bulk(bool noSample)
    {
        var templateRoot = TemplateRoot(FindRepositoryRoot());
        var agents = string.Join('\n', AgentGuidance(File.ReadAllLines(Path.Combine(templateRoot, "AGENTS.md")), noSample));
        var skills = Path.Combine(templateRoot, ".claude", "skills");
        var addAggregate = File.ReadAllText(Path.Combine(skills, "add-aggregate", "SKILL.md"));
        var addSlice = File.ReadAllText(Path.Combine(skills, "add-slice", "SKILL.md"));

        agents.ShouldContain("Save early only to translate a database error into a domain exception, or to save a root before a bulk write of its "
            + "children in the same transaction, as `add-aggregate` details.");
        agents.ShouldContain("A command handler never calls `SaveChangesAsync`: each command runs in one transaction that the pipeline saves and commits");
        addAggregate.ShouldContain("\n## Children written in bulk\n");
        addAggregate.ShouldContain("The solution generates no helper for it");
        addAggregate.ShouldContain("2. Add the root, then save it early, before the first child");
        addAggregate.ShouldContain("through the connection of `AppDbContext`, which already holds the transaction of the command");
        addAggregate.ShouldContain("Never open a connection or a transaction of your own");
        addAggregate.ShouldContain("4. Keep the foreign key to the root immediate");
        addAggregate.ShouldContain("Never make it `DEFERRABLE INITIALLY DEFERRED` nor remove it to write the children first");
        addAggregate.ShouldContain("gets a longer timeout in that command only: the `Timeout` of the importer for a `COPY`, `db.Database.SetCommandTimeout(...)`");
        addAggregate.ShouldContain("- Write children in bulk before their root, through a connection or a transaction of your own, or under a deferred or a missing foreign key.");
        addSlice.ShouldContain("It never calls `SaveChangesAsync`, except to save early as the cases below say");
        addSlice.ShouldContain("as [Children written in bulk](../add-aggregate/SKILL.md#children-written-in-bulk) of `add-aggregate` details;");
        Directory.EnumerateFiles(Path.Combine(templateRoot, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => File.ReadAllText(file).Contains("BeginBinaryImport", StringComparison.Ordinal))
            .ShouldBeEmpty("Bulk writes are documented in add-aggregate, never generated.");
    }

    // PB-085: a migration scaffolded on a table that holds rows loses them. add-aggregate says how to rewrite it as a conversion in
    // both directions, under a longer timeout for that run only and tried on a copy of the data, and how to map private fields
    // behind the read-only view that HS007 expects. AGENTS.md sends there from "Changing the database". The pack copies the skill
    // into .agents/skills/ as is, which the test of the pack checks.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Agent_guidance_says_how_to_change_the_shape_of_stored_rows_and_to_map_private_fields(bool noSample)
    {
        var templateRoot = TemplateRoot(FindRepositoryRoot());
        var agents = AgentGuidance(File.ReadAllLines(Path.Combine(templateRoot, "AGENTS.md")), noSample);
        var addAggregate = File.ReadAllText(Path.Combine(templateRoot, ".claude", "skills", "add-aggregate", "SKILL.md"));

        var database = agents.SkipWhile(line => line != "## Changing the database").TakeWhile(line => line != "## Running the API").ToArray();
        database.Single(line => line.StartsWith("2. After any mapping change, add a migration.", StringComparison.Ordinal))
            .ShouldContain("Read the migration before applying it: the change of a table that holds rows comes as drops and adds that lose them, "
                + "to rewrite as a conversion in both directions, and a state stored in another shape than the one the entity shows maps "
                + "private fields, as `add-aggregate` details.");
        agents.Length.ShouldBeLessThan(150);

        addAggregate.ShouldContain("Also use when a mapping changes a table that already holds rows, when an entity stores its state in another shape");
        addAggregate.ShouldContain("A change of a table that already holds rows follows [Changing the shape of stored rows](#changing-the-shape-of-stored-rows).");
        addAggregate.ShouldContain("lives in private fields, as [Mapping private fields](#mapping-private-fields) says.");

        var reshape = Section(addAggregate, "## Changing the shape of stored rows");
        reshape.ShouldContain("1. Read `Up` and `Down` of the generated migration before applying it anywhere.");
        reshape.ShouldContain("An operation was scaffolded that may result in the loss of data.");
        reshape.ShouldContain("2. Rewrite the loss as a conversion, in SQL through `migrationBuilder.Sql`:");
        reshape.ShouldContain("fill it from the old one with `INSERT INTO ... SELECT ...`");
        reshape.ShouldContain("3. Write the `Down` that converts back");
        reshape.ShouldContain("the loss is a decision: ask the user before writing it");
        reshape.ShouldContain("Apply it with a longer timeout for that run only, in the connection string that `dotnet ef database update` receives: "
            + "`dotnet ef database update --project src/MyApp --connection \"<connection string>;Command Timeout=600\"`. Never raise it in "
            + "the configuration nor in the code of the application");
        reshape.ShouldContain("5. Try it on a copy of the data, in both directions, before a database that matters.");
        reshape.ShouldContain("which runs `Down`");

        var privateFields = Section(addAggregate, "## Mapping private fields");
        privateFields.ShouldContain("HS007 accepts private fields, and EF Core reads and writes them by their names.");
        privateFields.ShouldContain("builder.PrimitiveCollection<string[]>(\"_stopIds\").HasColumnName(\"StopIds\").IsRequired();");
        privateFields.ShouldContain("builder.Property<DateTimeOffset?>(\"_plannedAt\").HasColumnName(\"PlannedAt\");");
        privateFields.ShouldContain("public IReadOnlyList<TripStop> Stops => [..");
        privateFields.ShouldContain("builder.Ignore(trip => trip.Stops);");
        privateFields.ShouldContain("never the array itself, which a caller could change: HS007 forbids a public mutable object.");

        addAggregate.ShouldContain("- Apply a migration that drops or changes a table holding rows without converting them, in both directions, or raise the "
            + "command timeout in the connection string of the application.");
        addAggregate.ShouldContain("- Make a mapped private field public, or show the array it holds, so that a slice or a test reaches it.");
        addAggregate.IndexOf("\n## Changing the shape of stored rows\n", StringComparison.Ordinal)
            .ShouldBeLessThan(addAggregate.IndexOf("\n## Never\n", StringComparison.Ordinal));
        addAggregate.IndexOf("\n## Mapping private fields\n", StringComparison.Ordinal)
            .ShouldBeLessThan(addAggregate.IndexOf("\n## Never\n", StringComparison.Ordinal));

        static string Section(string markdown, string heading)
        {
            var start = markdown.IndexOf($"\n{heading}\n", StringComparison.Ordinal);
            start.ShouldBeGreaterThan(0, $"add-aggregate has no section '{heading}'.");
            var end = markdown.IndexOf("\n## ", start + heading.Length + 1, StringComparison.Ordinal);
            return markdown[start..(end < 0 ? markdown.Length : end)];
        }
    }

    // PB-063: outside the code blocks that quote a diagnostic, every instruction of the agent kit that asks for an invalid request or
    // for validator tests says which commands it concerns. HS013 is about commands with parameters only, and says so once.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Agent_kit_never_asks_a_command_without_parameters_for_a_validator_or_an_invalid_request(bool noSample)
    {
        var templateRoot = TemplateRoot(FindRepositoryRoot());
        var request = new Regex(@"invalid (request|command|one)|validator tests|tests of its validator", RegexOptions.IgnoreCase);
        var guidance = new List<(string File, string[] Lines)>
        {
            ("AGENTS.md", AgentGuidance(File.ReadAllLines(Path.Combine(templateRoot, "AGENTS.md")), noSample)),
            ("docs/rules/HS014.md", File.ReadAllLines(Path.Combine(templateRoot, "docs", "rules", "HS014.md"))),
            ("docs/rules/HS018.md", File.ReadAllLines(Path.Combine(templateRoot, "docs", "rules", "HS018.md"))),
        };
        guidance.AddRange(Directory.EnumerateFiles(Path.Combine(templateRoot, ".claude", "skills"), "SKILL.md", SearchOption.AllDirectories)
            .Select(skill => (Path.GetRelativePath(templateRoot, skill), File.ReadAllLines(skill))));

        guidance
            .SelectMany(file => OutsideCodeBlocks(file.Lines).Where(line => request.IsMatch(line) && !line.Contains("parameters", StringComparison.Ordinal))
                .Select(line => $"{file.File}: {line.Trim()}"))
            .ShouldBeEmpty("These instructions ask every command for a validator or an invalid request, which a command without parameters has not.");

        var hs013 = File.ReadAllText(Path.Combine(templateRoot, "docs", "rules", "HS013.md"));
        hs013.ShouldContain("has no input to check and needs no validator: an empty one is a forbidden workaround. "
            + "Its tests cover a failure of the domain, or its success, instead of an invalid request.");
        hs013.ShouldNotContain("required by the slice tests");
    }

    private static IEnumerable<string> OutsideCodeBlocks(IEnumerable<string> lines)
    {
        var inBlock = false;
        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
                inBlock = !inBlock;
            else if (!inBlock)
                yield return line;
        }
    }

    // PB-056: dotnet new does not keep the executable bit of a packed file: the template sets it back on the scripts it ships,
    // except on Windows, where Git for Windows runs the hook without it.
    [Fact]
    public void Api_template_makes_the_pre_commit_hook_and_the_check_script_executable()
    {
        var templateRoot = TemplateRoot(FindRepositoryRoot());
        using var template = JsonDocument.Parse(File.ReadAllText(Path.Combine(templateRoot, ".template.config", "template.json")));
        var action = template.RootElement.GetProperty("postActions").EnumerateArray()
            .Single(candidate => candidate.GetProperty("id").GetString() == "makeScriptsExecutable");

        action.GetProperty("actionId").GetString()!.ToLowerInvariant().ShouldBe("cb9a6cf3-4f5c-4860-b9d2-03a574959774");
        action.GetProperty("condition").GetString().ShouldBe("(HostOS != \"Windows_NT\")");
        action.GetProperty("continueOnError").GetBoolean().ShouldBeTrue();
        var scripts = action.GetProperty("args").GetProperty("+x").EnumerateArray().Select(script => script.GetString()!).ToArray();
        scripts.ShouldBe([".githooks/pre-commit", "scripts/check.sh"]);
        scripts.ShouldAllBe(script => File.Exists(Path.Combine(templateRoot, script)));
        template.RootElement.GetProperty("symbols").GetProperty("HostOS").GetProperty("binding").GetString().ShouldBe("env:OS");
    }

    // PB-056: --no-sample leaves out the Bookings module only; the tests of the foundation (transaction, domain events) stay.
    [Fact]
    public void Api_template_without_the_sample_leaves_out_the_Bookings_module_and_its_migrations_only()
    {
        var templateRoot = TemplateRoot(FindRepositoryRoot());
        using var template = JsonDocument.Parse(File.ReadAllText(Path.Combine(templateRoot, ".template.config", "template.json")));
        var modifier = template.RootElement.GetProperty("sources")[0].GetProperty("modifiers").EnumerateArray()
            .Single(candidate => candidate.GetProperty("condition").GetString() == "(NoSample)");

        modifier.GetProperty("exclude").EnumerateArray().Select(glob => glob.GetString()!)
            .ShouldAllBe(glob => glob.EndsWith("/Bookings/**", StringComparison.Ordinal) || glob == "src/MyApp/Shared/Persistence/Migrations/**");
        foreach (var test in new[] { "TransactionBehaviorTests.cs", "DomainEventPublisherTests.cs" })
        {
            var text = File.ReadAllText(Path.Combine(templateRoot, "tests", "MyApp.IntegrationTests", "Shared", "Behaviors", test));
            Regex.Split(text, @"#if \(!NoSample\)[\s\S]*?#endif").ShouldContain(
                part => part.Contains("[Fact]", StringComparison.Ordinal), $"{test} keeps no test of the foundation without the sample.");
        }
    }

    // PB-056: a rule sheet may illustrate a rule with the sample, but never sends the reader to one of its files, which --no-sample removes.
    [Fact]
    public void Rule_sheets_send_the_reader_to_what_the_templates_generate_not_to_the_files_of_the_sample()
    {
        var rules = Path.Combine(TemplateRoot(FindRepositoryRoot()), "docs", "rules");
        var reference = new Regex(
            @"[Ff]ollow `[^`]*Booking[^`]*`|as `[^`]*Booking[^`]*` does|`Booking(Configuration|sModule|ConfigurationTests)`|`(Confirm|Cancel|Get)Booking(Validator)?Tests`");

        Directory.EnumerateFiles(rules, "*.md")
            .Where(sheet => reference.IsMatch(File.ReadAllText(sheet)))
            .Select(Path.GetFileName)
            .ShouldBeEmpty("These rule sheets send the reader to a file of the Bookings sample.");
    }

    // Every Markdown link of the agent guidance resolves to a file of the template, and its anchor to a heading of that file.
    [Theory]
    [InlineData("AGENTS.md")]
    [MemberData(nameof(AgentInstructionFiles))]
    [MemberData(nameof(SkillFiles))]
    public void Agent_guidance_links_resolve_to_files_and_headings_of_the_template(string file)
    {
        var templateRoot = TemplateRoot(FindRepositoryRoot());
        var source = Path.Combine(templateRoot, file);
        var links = Regex.Matches(File.ReadAllText(source), @"\]\((?<target>[^)\s]+)\)")
            .Select(match => match.Groups["target"].Value)
            .ToArray();

        links.ShouldNotBeEmpty();
        var broken = new List<string>();
        foreach (var link in links)
        {
            var parts = link.Split('#', 2);
            var target = parts[0].Length == 0 ? source : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, parts[0]));
            if (!target.StartsWith(templateRoot, StringComparison.Ordinal) || !File.Exists(target))
                broken.Add($"{link}: no such file in the template");
            else if (parts.Length == 2 && !Headings(target).Contains(parts[1]))
                broken.Add($"{link}: no such heading");
        }

        broken.ShouldBeEmpty($"{file} has links that resolve to nothing the generated solution contains.");
    }

    public static TheoryData<string> AgentInstructionFiles() => new() { ".claude/CLAUDE.md", "GEMINI.md", ".github/copilot-instructions.md" };

    // The skills of the template: four generate their element with the item template they name (PB-043), two fix the code that a
    // HardSlice failure names after Skill: (PB-044).
    private static readonly string[] GeneratingSkills = ["add-aggregate", "add-domain-event", "add-module", "add-slice"];
    private static readonly string[] FixingSkills = ["extract-to-domain", "fix-architecture-violation"];
    private static readonly string[] SkillNames = [.. GeneratingSkills.Concat(FixingSkills).Order(StringComparer.Ordinal)];

    public static TheoryData<string> SkillFiles()
    {
        var files = new TheoryData<string>();
        foreach (var skill in SkillNames)
            files.Add($".claude/skills/{skill}/SKILL.md");
        return files;
    }

    // The files that an agent never edits (PB-046, PB-048, PB-053), as the list of "Forbidden" in AGENTS.md names them, the only
    // one written for agents: ClaudeHookTests of HardSlice.Tool checks that it is exactly what the hooks and the CI protect.
    private static string[] ProtectedPaths()
    {
        var agents = File.ReadAllLines(Path.Combine(TemplateRoot(FindRepositoryRoot()), "AGENTS.md"));
        var list = agents.Where(line => line.StartsWith("Protected files: ", StringComparison.Ordinal)).ShouldHaveSingleItem();
        return [.. Regex.Matches(list.Split(". ")[0], "`(?<path>[^`]+)`").Select(match => match.Groups["path"].Value)];
    }

    // The Agent Skills format: a header with the name of the folder (lowercase words and hyphens, at most 64 characters) and a
    // description of at most 1024 characters, which says what the skill does and when to use it. The template holds the source
    // only: the pack writes .agents/skills/.
    [Fact]
    public void Template_ships_each_skill_once_with_a_header_named_after_its_folder()
    {
        var templateRoot = TemplateRoot(FindRepositoryRoot());
        var skillsRoot = Path.Combine(templateRoot, ".claude", "skills");

        Directory.Exists(Path.Combine(templateRoot, ".agents")).ShouldBeFalse(".agents/skills/ is written by the pack from .claude/skills/, never by hand.");
        Directory.EnumerateFiles(skillsRoot, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(skillsRoot, file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ShouldBe(SkillNames.Select(skill => $"{skill}/SKILL.md"));

        foreach (var skill in SkillNames)
        {
            var lines = File.ReadAllLines(Path.Combine(skillsRoot, skill, "SKILL.md"));
            lines[0].ShouldBe("---", $"{skill} does not start with its header.");
            var end = Array.IndexOf(lines, "---", 1);
            end.ShouldBeGreaterThan(0, $"{skill} has no end to its header.");
            var header = lines[1..end]
                .Select(line => line.Split(':', 2))
                .ToDictionary(field => field[0], field => field[1].Trim(), StringComparer.Ordinal);

            header.Keys.Order(StringComparer.Ordinal).ShouldBe(["description", "name"]);
            header["name"].ShouldBe(skill);
            header["name"].ShouldMatch("^[a-z0-9]+(-[a-z0-9]+)*$");
            header["name"].Length.ShouldBeLessThanOrEqualTo(64);
            header["description"].Length.ShouldBeInRange(100, 1024);
            header["description"].ShouldContain("Use when", Case.Sensitive, $"The description of {skill} does not say when to use it.");
            if (GeneratingSkills.Contains(skill))
                header["description"].ShouldContain("dotnet new hardslice-", Case.Sensitive, $"The description of {skill} does not name its template.");
            else
                header["description"].ShouldContain($"`Skill: {skill}`", Case.Sensitive, $"The description of {skill} does not quote the words of the messages that name it.");
            lines.Skip(end + 1).ShouldContain(line => line.StartsWith("# ", StringComparison.Ordinal));
        }
    }

    // Each dotnet new command of a skill names an item template of the package, options that the template declares, and a
    // value among its choices. The skill generates its element with its own template and runs the check after.
    [Theory]
    [InlineData("add-module", "hardslice-module")]
    [InlineData("add-aggregate", "hardslice-aggregate")]
    [InlineData("add-aggregate", "hardslice-entity")]
    [InlineData("add-slice", "hardslice-command")]
    [InlineData("add-slice", "hardslice-query")]
    [InlineData("add-slice", "hardslice-condition")]
    [InlineData("add-domain-event", "hardslice-event")]
    public void Skill_generates_its_element_with_dotnet_new_commands_that_exist_in_the_package(string skill, string template)
    {
        var repositoryRoot = FindRepositoryRoot();
        var text = File.ReadAllText(Path.Combine(TemplateRoot(repositoryRoot), ".claude", "skills", skill, "SKILL.md"));
        var commands = Regex.Matches(text, @"dotnet new (?<template>[\w-]+)(?<arguments>(?:[ ]+(?:--[a-z-]+|[^\s`'""-][^\s`'""]*))*)")
            .Select(match => (Template: match.Groups["template"].Value, Arguments: match.Groups["arguments"].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)))
            .ToArray();

        commands.ShouldContain(command => command.Template == template && command.Arguments.Contains("--project"), $"{skill} does not run {template} on the web project.");
        commands.SelectMany(command => command.Arguments.SkipWhile(argument => argument != "--project").Skip(1).Take(1))
            .ShouldAllBe(project => project == "src/MyApp/MyApp.csproj" && File.Exists(Path.Combine(TemplateRoot(repositoryRoot), project)));
        foreach (var (name, arguments) in commands)
        {
            var options = TemplateOptions(repositoryRoot, name, skill);
            for (var index = 0; index < arguments.Length; index++)
            {
                if (arguments[index].StartsWith("--", StringComparison.Ordinal))
                    AcceptsOption(options, arguments[index], index + 1 < arguments.Length ? arguments[index + 1] : null)
                        .ShouldBeTrue($"{skill} runs dotnet new {name} with {arguments[index]} {(index + 1 < arguments.Length ? arguments[index + 1] : "")}, which it does not declare among its options and choices.");
            }
        }

        // The options that the text quotes, such as `--result many`, belong to one of the templates the skill runs.
        var templates = commands.Select(command => command.Template).Distinct().Select(name => TemplateOptions(repositoryRoot, name, skill)).ToArray();
        foreach (Match quoted in Regex.Matches(text, @"`(?<option>--[a-z-]+)(?: (?<value>[^`\s]+))?`"))
        {
            var value = quoted.Groups["value"].Success ? quoted.Groups["value"].Value : null;
            templates.Any(options => AcceptsOption(options, quoted.Groups["option"].Value, value))
                .ShouldBeTrue($"{skill} quotes {quoted.Value}, which no template it runs declares among its options and choices.");
        }

        text.ShouldContain("bash scripts/check.sh");
        text.ShouldContain("pwsh scripts/check.ps1");
    }

    // A fixing skill starts from the message, follows the sheet of its rule, stops on a conflict, and runs the check after (PB-044).
    [Theory]
    [InlineData("extract-to-domain")]
    [InlineData("fix-architecture-violation")]
    public void Fixing_skill_reads_the_message_and_runs_the_check(string skill)
    {
        var text = File.ReadAllText(Path.Combine(TemplateRoot(FindRepositoryRoot()), ".claude", "skills", skill, "SKILL.md"));

        Regex.Matches(text, @"dotnet new ").ShouldBeEmpty($"{skill} generates no element: the skills that do are named instead.");
        text.ShouldContain("Read the message");
        text.ShouldContain("bash scripts/check.sh");
        text.ShouldContain("pwsh scripts/check.ps1");
        if (skill == "fix-architecture-violation")
        {
            text.ShouldContain("dotnet hardslice explain <rule>");
            // The skills that the messages of the other rules name (RuleSkillTests).
            foreach (var other in new[] { "add-slice", "extract-to-domain", "add-aggregate", "add-domain-event" })
                text.ShouldContain($"`{other}`", Case.Sensitive, $"{skill} does not hand the rules of {other} over to it.");
        }
    }

    // The options of an item template, by long name, with the values of a choice: those of dotnet new itself that a skill may use
    // accept any value.
    private static Dictionary<string, string[]?> TemplateOptions(string repositoryRoot, string template, string skill)
    {
        var root = Path.Combine(repositoryRoot, "src", "HardSlice.Templates", "content", template);
        Directory.Exists(root).ShouldBeTrue($"{skill} runs dotnet new {template}, which the package does not ship.");
        using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".template.config", "template.json")));
        using var host = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".template.config", "dotnetcli.host.json")));
        var symbols = config.RootElement.GetProperty("symbols");
        var options = host.RootElement.GetProperty("symbolInfo").EnumerateObject().ToDictionary(
            symbol => $"--{symbol.Value.GetProperty("longName").GetString()}",
            symbol => symbols.GetProperty(symbol.Name).GetProperty("datatype").GetString() == "choice"
                ? symbols.GetProperty(symbol.Name).GetProperty("choices").EnumerateArray().Select(choice => choice.GetProperty("choice").GetString()!).ToArray()
                : null,
            StringComparer.Ordinal);
        options["--project"] = null;
        options["--name"] = null;
        options["--force"] = null;
        return options;
    }

    // A placeholder such as <Dto>, or no value, stands for any value.
    private static bool AcceptsOption(Dictionary<string, string[]?> options, string option, string? value) =>
        options.TryGetValue(option, out var choices)
        && (choices is null || value is null || value.StartsWith('<') || value.StartsWith("--", StringComparison.Ordinal) || choices.Contains(value));

    // A skill cites only paths of a solution generated with or without --no-sample: never the Bookings sample.
    [Theory]
    [MemberData(nameof(SkillFiles))]
    public void Every_path_a_skill_cites_exists_in_a_solution_generated_without_the_sample(string file)
    {
        var templateRoot = TemplateRoot(FindRepositoryRoot());
        var text = File.ReadAllText(Path.Combine(templateRoot, file));
        using var template = JsonDocument.Parse(File.ReadAllText(Path.Combine(templateRoot, ".template.config", "template.json")));
        var sampleOnly = template.RootElement.GetProperty("sources")[0].GetProperty("modifiers")[0].GetProperty("exclude").EnumerateArray()
            .Select(exclude => exclude.GetString()!.Replace("/**", "/", StringComparison.Ordinal))
            .ToArray();

        text.ShouldNotContain("Booking", Case.Insensitive, $"{file} mentions the Bookings sample, which --no-sample leaves out.");
        text.ShouldNotContain("<!--#", customMessage: $"{file} holds a template condition: both variants share the same skill.");
        var citedPaths = Regex.Matches(text, @"`(?<path>(?:src|tests|docs|scripts)/[^`<>\s]*|[\w.]+\.(?:md|slnx|json))`")
            .Select(match => match.Groups["path"].Value)
            .Distinct()
            .ToArray();

        citedPaths.ShouldContain("AGENTS.md");
        citedPaths
            .Where(path => !File.Exists(Path.Combine(templateRoot, path)) && !Directory.Exists(Path.Combine(templateRoot, path)))
            .ShouldBeEmpty($"{file} cites paths that the generated solution does not contain.");
        citedPaths
            .Where(path => sampleOnly.Any(excluded => path.StartsWith(excluded, StringComparison.Ordinal)))
            .ShouldBeEmpty($"{file} cites paths that only the Bookings sample holds.");
    }

    // A skill recalls the forbidden workarounds in its own Never section, which links to Forbidden for the list of the protected
    // files instead of copying it (PB-053: six copies had drifted from what the hooks protect). It never names a protected file,
    // except to run the check scripts.
    [Theory]
    [MemberData(nameof(SkillFiles))]
    public void Skill_recalls_the_forbidden_workarounds_and_never_proposes_to_edit_a_protected_file(string file)
    {
        var lines = File.ReadAllLines(Path.Combine(TemplateRoot(FindRepositoryRoot()), file));
        var never = lines.SkipWhile(line => line != "## Never").ToArray();
        var steps = string.Join('\n', lines)
            .Replace("bash scripts/check.sh", "", StringComparison.Ordinal)
            .Replace("pwsh scripts/check.ps1", "", StringComparison.Ordinal);
        var neverText = string.Join('\n', never);

        never.ShouldNotBeEmpty($"{file} has no Never section.");
        neverText.ShouldContain("](../../../AGENTS.md#forbidden)");
        neverText.ShouldContain("stop and explain the conflict");
        neverText.ShouldContain("- Create, edit or delete a protected file: the list of [Forbidden](../../../AGENTS.md#forbidden) names them all.");
        var protectedPaths = ProtectedPaths();
        protectedPaths.ShouldContain(".agents/");
        foreach (var path in protectedPaths)
            steps.ShouldNotContain($"`{path}", Case.Sensitive, $"{file} names the protected {path}: link to the list of Forbidden instead.");
    }

    // GitHub anchors of the headings of a Markdown file: lowercase, punctuation removed, spaces as hyphens.
    private static HashSet<string> Headings(string markdown) =>
        File.ReadAllLines(markdown)
            .Where(line => Regex.IsMatch(line, @"^#{1,6} "))
            .Select(line => Regex.Replace(line.TrimStart('#').Trim().ToLowerInvariant(), @"[^\w\- ]", "").Replace(' ', '-'))
            .ToHashSet(StringComparer.Ordinal);

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
        configuration.RootElement.GetProperty("contracts").GetProperty("systemCommand").GetString().ShouldBe("MyApp.Shared.Messaging.ISystemCommand`1, MyApp");
        configuration.RootElement.GetProperty("shared").GetProperty("allowedFolders").EnumerateArray()
            .Select(folder => folder.GetString())
            .ShouldBe(["Messaging", "Persistence", "Behaviors", "Http"]);
    }

    // PB-079: each folder that hardslice.json allows is in Shared, with its role in HS017.md and its row in AGENTS.md, with and without
    // the sample. Behaviors keeps the mediator pipeline only: the answers of the HTTP pipeline live in Http.
    [Fact]
    public void Template_Shared_holds_the_allowed_folders_each_described_by_HS017_and_AGENTS_md()
    {
        var templateRoot = TemplateRoot(FindRepositoryRoot());
        using var configuration = JsonDocument.Parse(File.ReadAllText(Path.Combine(templateRoot, "hardslice.json")));
        var folders = configuration.RootElement.GetProperty("shared").GetProperty("allowedFolders").EnumerateArray()
            .Select(folder => folder.GetString()!)
            .ToArray();
        var shared = Path.Combine(templateRoot, "src", "MyApp", "Shared");
        var sheet = File.ReadAllText(Path.Combine(templateRoot, "docs", "rules", "HS017.md"));
        var agents = File.ReadAllLines(Path.Combine(templateRoot, "AGENTS.md"));

        Directory.GetDirectories(shared).Select(Path.GetFileName).Order().ShouldBe(folders.Order());
        sheet.ShouldContain($"by default {string.Join(", ", folders[..^1].Select(folder => $"`{folder}`"))} and `{folders[^1]}`.");
        sheet.ShouldContain($"\"allowedFolders\": [{string.Join(", ", folders.Select(folder => $"\"{folder}\""))}]");
        sheet.ShouldContain($"in the folders allowed by hardslice.json: {string.Join(", ", folders)}. Fix:");
        foreach (var folder in folders)
        {
            sheet.ShouldContain($"| `{folder}` | ", customMessage: $"HS017.md gives no role to Shared/{folder}.");
            foreach (var noSample in new[] { false, true })
                AgentGuidance(agents, noSample).ShouldContain(
                    line => line.StartsWith($"| `src/MyApp/Shared/{folder}/` | ", StringComparison.Ordinal),
                    $"AGENTS.md (noSample: {noSample}) maps no Shared/{folder}.");
        }

        sheet.ShouldContain("An authentication scheme that protects the endpoints of several modules, its options, its policy and the extension that registers them go in `Http`");
        Directory.GetFiles(Path.Combine(shared, "Http")).Select(Path.GetFileName).Order()
            .ShouldBe(["BadRequestExceptionHandler.cs", "DomainExceptionHandler.cs", "ExplicitOffsetJsonConverter.cs", "ValidationExceptionHandler.cs"]);
        Directory.GetFiles(Path.Combine(shared, "Behaviors")).Select(File.ReadAllText)
            .ShouldAllBe(text => !text.Contains("IExceptionHandler") && !text.Contains("JsonConverter<"), "Behaviors holds an answer of the HTTP pipeline.");
    }

    // PB-079: a 401, a 403 or a 429 is written by the middleware before the delegate of the endpoint runs. HS015.md says that it is
    // not in the signature, where the policy goes and which test covers it, instead of claiming that the signature holds every response.
    [Fact]
    public void HS015_sheet_says_what_it_expects_of_the_responses_of_the_middleware()
    {
        var sheet = File.ReadAllText(Path.Combine(TemplateRoot(FindRepositoryRoot()), "docs", "rules", "HS015.md"));
        var section = sheet.Split("## Responses of the middleware\n")[1].Split("\n## ")[0];

        sheet.ShouldNotContain("`TypedResults` put every response of the endpoint in its signature");
        sheet.ShouldContain("`TypedResults` put every response that the delegate of the endpoint returns in its signature");
        foreach (var expected in new[]
        {
            "the 401 or the 403 of an authorization policy or the 429 of rate limiting, is not in the signature of the endpoint",
            "`src/MyApp/Shared/Http/` (HS017)",
            "goes on its group, in `<Module>Module.cs`",
            "goes on the endpoint, in its slice",
            "`FallbackPolicy`",
            "`AllowAnonymous`",
            "An integration test of the 401 stands for the signature",
        })
            section.ShouldContain(expected);
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
        "tests/MyApp.IntegrationTests/Features/ModuleName/QueryName/QueryNameTests.cs",
        "tests/MyApp.Tests/Features/ModuleName/QueryName/QueryNameValidatorTests.cs"
    ];

    private static readonly string[] ModuleTemplateFiles =
    [
        ".template.config/dotnetcli.host.json",
        ".template.config/template.json",
        "src/MyApp/Features/ModuleName/ModuleNameModule.cs"
    ];

    private static readonly string[] AggregateTemplateFiles =
    [
        ".template.config/dotnetcli.host.json",
        ".template.config/template.json",
        "src/MyApp.Domain/ModuleName/AggregateName.cs",
        "src/MyApp.Domain/ModuleName/AggregateNameId.cs",
        "src/MyApp/Features/ModuleName/AggregateNameConfiguration.cs",
        "tests/MyApp.IntegrationTests/Features/ModuleName/AggregateNameConfigurationTests.cs",
        "tests/MyApp.Tests/Domain/ModuleName/AggregateNameTests.cs"
    ];

    private static readonly string[] EntityTemplateFiles =
    [
        ".template.config/dotnetcli.host.json",
        ".template.config/template.json",
        "src/MyApp.Domain/ModuleName/EntityName.cs",
        "src/MyApp.Domain/ModuleName/EntityNameId.cs",
        "src/MyApp/Features/ModuleName/AggregateNameIdConverter.cs",
        "src/MyApp/Features/ModuleName/EntityNameConfiguration.cs",
        "tests/MyApp.Tests/Domain/ModuleName/EntityNameTests.cs"
    ];

    private static readonly string[] EventTemplateFiles =
    [
        ".template.config/dotnetcli.host.json",
        ".template.config/template.json",
        "src/MyApp.Domain/ModuleName/Events/EventName.cs",
        "src/MyApp/Features/ReactionModule/ReactionSlice/ReactionSlice.cs",
        "tests/MyApp.IntegrationTests/Features/ReactionModule/ReactionSlice/ReactionSliceTests.cs"
    ];

    private static readonly string[] ConditionTemplateFiles =
    [
        ".template.config/dotnetcli.host.json",
        ".template.config/template.json",
        "src/MyApp/Features/ModuleName/CommandName/CommandNameCondition.cs",
        "tests/MyApp.IntegrationTests/Features/ModuleName/CommandName/CommandNameConditionTests.cs"
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

    private static string EntityTemplateRoot(string repositoryRoot) =>
        Path.Combine(repositoryRoot, "src", "HardSlice.Templates", "content", "hardslice-entity");

    private static string EventTemplateRoot(string repositoryRoot) =>
        Path.Combine(repositoryRoot, "src", "HardSlice.Templates", "content", "hardslice-event");

    private static string ConditionTemplateRoot(string repositoryRoot) =>
        Path.Combine(repositoryRoot, "src", "HardSlice.Templates", "content", "hardslice-condition");

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
