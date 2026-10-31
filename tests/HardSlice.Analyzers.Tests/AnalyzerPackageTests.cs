using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;

namespace HardSlice.Analyzers.Tests;

public sealed class AnalyzerPackageTests
{
    // The package's build assets give the compiler the nearest hardslice.json above the project and IsTestProject.
    [Fact]
    public async Task Package_is_analyzer_only_and_enforces_HS016_and_its_contracts_for_a_consumer()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var repositoryRoot = FindRepositoryRoot();
        var analyzerProject = Path.Combine(repositoryRoot, "src", "HardSlice.Analyzers", "HardSlice.Analyzers.csproj");
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), $"hardslice-analyzer-package-{Guid.NewGuid():N}");
        var packageVersion = $"0.0.0-test.{Guid.NewGuid():N}";
        var feedDirectory = Path.Combine(temporaryDirectory, "feed");
        var consumerDirectory = Path.Combine(temporaryDirectory, "consumer");
        var consumerPackages = Path.Combine(temporaryDirectory, "consumer-packages");
        var packOutput = Path.Combine(temporaryDirectory, "pack-bin") + Path.DirectorySeparatorChar;
        var packIntermediate = Path.Combine(temporaryDirectory, "pack-obj") + Path.DirectorySeparatorChar;

        Directory.CreateDirectory(feedDirectory);
        Directory.CreateDirectory(consumerDirectory);

        try
        {
            var packRestore = await Dotnet.RunAsync(
                repositoryRoot,
                nugetPackagesDirectory: null,
                cancellationToken,
                "restore",
                analyzerProject,
                $"-p:BaseOutputPath={packOutput}",
                $"-p:BaseIntermediateOutputPath={packIntermediate}",
                "-p:DefaultItemExcludes=obj/**");
            packRestore.ExitCode.ShouldBe(0, packRestore.Output);

            var pack = await Dotnet.RunAsync(
                repositoryRoot,
                nugetPackagesDirectory: null,
                cancellationToken,
                "pack",
                analyzerProject,
                "--configuration", "Release",
                "--no-restore",
                "--output", feedDirectory,
                $"-p:PackageVersion={packageVersion}",
                $"-p:BaseOutputPath={packOutput}",
                $"-p:BaseIntermediateOutputPath={packIntermediate}",
                "-p:DefaultItemExcludes=obj/**");
            pack.ExitCode.ShouldBe(0, pack.Output);

            var packagePath = Directory
                .EnumerateFiles(feedDirectory, $"HardSlice.Analyzers.{packageVersion}.nupkg")
                .ShouldHaveSingleItem();
            AssertAnalyzerOnlyPackage(packagePath);

            var consumerProject = Path.Combine(consumerDirectory, "MyApp.csproj");
            // Each slice in its folder, as HS004 requires: the CancelBooking slice is the one the test makes violate HS016.
            var confirmBookingSource = Path.Combine(consumerDirectory, "Features", "Bookings", "ConfirmBooking", "ConfirmBooking.cs");
            var consumerSource = Path.Combine(consumerDirectory, "Features", "Bookings", "CancelBooking", "CancelBooking.cs");
            var domainSource = Path.Combine(consumerDirectory, "Domain.cs");
            var configuration = Path.Combine(temporaryDirectory, "hardslice.json");
            Directory.CreateDirectory(Path.GetDirectoryName(confirmBookingSource)!);
            Directory.CreateDirectory(Path.GetDirectoryName(consumerSource)!);
            await File.WriteAllTextAsync(consumerProject, ConsumerProject(packageVersion), cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(consumerDirectory, "Contracts.cs"), ContractsSource, cancellationToken);
            await File.WriteAllTextAsync(configuration, Configuration, cancellationToken);
            await File.WriteAllTextAsync(confirmBookingSource, ConfirmBookingSource, cancellationToken);
            await File.WriteAllTextAsync(consumerSource, CompliantSource, cancellationToken);

            var restore = await Dotnet.RunAsync(
                consumerDirectory,
                consumerPackages,
                cancellationToken,
                "restore",
                consumerProject,
                "--source", feedDirectory,
                "--no-cache");
            restore.ExitCode.ShouldBe(0, restore.Output);

            var publishDirectory = Path.Combine(temporaryDirectory, "publish");
            var publish = await Dotnet.RunAsync(
                consumerDirectory,
                consumerPackages,
                cancellationToken,
                "publish",
                consumerProject,
                "--configuration", "Release",
                "--no-restore",
                "--output", publishDirectory);
            publish.ExitCode.ShouldBe(0, publish.Output);

            Directory
                .EnumerateFiles(publishDirectory, "HardSlice.Analyzers.dll", SearchOption.AllDirectories)
                .ShouldBeEmpty();
            var dependencies = await File.ReadAllTextAsync(
                Path.Combine(publishDirectory, "MyApp.deps.json"),
                cancellationToken);
            dependencies.ShouldNotContain("HardSlice.Analyzers");

            await File.WriteAllTextAsync(consumerSource, ViolatingSource, cancellationToken);

            var violatingBuild = await Dotnet.RunAsync(
                consumerDirectory,
                consumerPackages,
                cancellationToken,
                "build",
                consumerProject,
                "--configuration", "Release",
                "--no-restore",
                "--no-incremental");
            violatingBuild.ExitCode.ShouldNotBe(0, violatingBuild.Output);
            violatingBuild.Output.ShouldContain(SliceReferenceAnalyzer.DiagnosticId);

            // HardSliceAnalysisMode gives the compiler the global config of its mode, Recommended when it is not set.
            foreach (var (properties, mode) in new[] { (Array.Empty<string>(), "Recommended"), (["-p:HardSliceAnalysisMode=Minimum"], "Minimum"), (["-p:HardSliceAnalysisMode=All"], "All") })
            {
                var evaluation = await Dotnet.RunAsync(
                    consumerDirectory,
                    consumerPackages,
                    cancellationToken,
                    ["msbuild", consumerProject, "-getProperty:HardSliceAnalysisMode", "-getItem:EditorConfigFiles", .. properties]);
                evaluation.ExitCode.ShouldBe(0, evaluation.Output);
                AnalysisModeFiles(evaluation.StandardOutput).ShouldBe([$"HardSlice.{mode}.globalconfig"], evaluation.Output);
            }

            var unknownModeBuild = await BuildConsumerAsync("-p:HardSliceAnalysisMode=Strict");
            unknownModeBuild.ExitCode.ShouldNotBe(0, unknownModeBuild.Output);
            unknownModeBuild.Output.ShouldContain(
                "error : HardSliceAnalysisMode is 'Strict', which is not a HardSlice analysis mode. "
                + "The mode selects the active HardSlice rules: Minimum, Recommended or All.");
            unknownModeBuild.Output.ShouldNotContain(SliceReferenceAnalyzer.DiagnosticId);

            // A setting of .editorconfig takes precedence over the mode.
            var editorConfig = Path.Combine(consumerDirectory, ".editorconfig");
            await File.WriteAllTextAsync(editorConfig, "root = true\n\n[*.cs]\ndotnet_diagnostic.HS016.severity = none\n", cancellationToken);
            var editorConfigBuild = await BuildConsumerAsync("-p:HardSliceAnalysisMode=All");
            editorConfigBuild.ExitCode.ShouldBe(0, editorConfigBuild.Output);
            File.Delete(editorConfig);

            await File.WriteAllTextAsync(consumerSource, CompliantSource, cancellationToken);
            await File.WriteAllTextAsync(domainSource, AmbiguousSource, cancellationToken);
            var ambiguousBuild = await BuildConsumerAsync();
            ambiguousBuild.ExitCode.ShouldNotBe(0, ambiguousBuild.Output);
            ambiguousBuild.Output.ShouldContain("error HS901: 'BookingSnapshot' is both an entity and a domain event");

            // HS900 to HS902 do not apply to test projects.
            var testProjectBuild = await BuildConsumerAsync("-p:IsTestProject=true");
            testProjectBuild.ExitCode.ShouldBe(0, testProjectBuild.Output);

            File.Delete(domainSource);
            File.Delete(configuration);
            var unconfiguredBuild = await BuildConsumerAsync();
            unconfiguredBuild.ExitCode.ShouldNotBe(0, unconfiguredBuild.Output);
            unconfiguredBuild.Output.ShouldContain($"error HS900: hardslice.json was not found in '{consumerDirectory}' or above it.");

            Task<DotnetResult> BuildConsumerAsync(params string[] properties) =>
                Dotnet.RunAsync(
                    consumerDirectory,
                    consumerPackages,
                    cancellationToken,
                    ["build", consumerProject, "--configuration", "Release", "--no-restore", "--no-incremental", .. properties]);
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory))
                Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private static void AssertAnalyzerOnlyPackage(string packagePath)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        var entries = archive.Entries.Select(entry => entry.FullName.Replace('\\', '/')).ToArray();

        entries.ShouldContain("analyzers/dotnet/cs/HardSlice.Analyzers.dll");
        entries.ShouldContain("build/HardSlice.Analyzers.props");
        entries.ShouldContain("build/HardSlice.Analyzers.targets");
        entries.ShouldContain("build/HardSlice.Minimum.globalconfig");
        entries.ShouldContain("build/HardSlice.Recommended.globalconfig");
        entries.ShouldContain("build/HardSlice.All.globalconfig");
        entries.Any(entry => entry.StartsWith("lib/", StringComparison.Ordinal)).ShouldBeFalse();
        entries.Any(entry => entry.StartsWith("ref/", StringComparison.Ordinal)).ShouldBeFalse();
        entries.Any(entry => entry.StartsWith("runtimes/", StringComparison.Ordinal)).ShouldBeFalse();

        var nuspecEntry = archive.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        using var nuspec = nuspecEntry.Open();
        var document = XDocument.Load(nuspec);
        document.Descendants().Where(element => element.Name.LocalName == "dependency").ShouldBeEmpty();
    }

    // The analysis mode files among the EditorConfigFiles that 'dotnet msbuild -getItem' prints as JSON.
    private static string[] AnalysisModeFiles(string evaluation)
    {
        using var document = JsonDocument.Parse(evaluation);
        return document.RootElement.GetProperty("Items").GetProperty("EditorConfigFiles").EnumerateArray()
            .Select(item => Path.GetFileName(item.GetProperty("FullPath").GetString()!))
            .Where(file => file.StartsWith("HardSlice.", StringComparison.Ordinal))
            .Distinct()
            .ToArray();
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HardSlice.slnx")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not find the HardSlice repository root.");
    }

    private static string ConsumerProject(string packageVersion) =>
        $$"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <Nullable>enable</Nullable>
            <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
            <NuGetAudit>false</NuGetAudit>
          </PropertyGroup>

          <ItemGroup>
            <PackageReference Include="HardSlice.Analyzers" Version="{{packageVersion}}" PrivateAssets="all" />
          </ItemGroup>
        </Project>
        """;

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
            "requestHandler": "MyApp.Shared.Messaging.IRequestHandler`2, MyApp",
            "domainEventHandler": "MyApp.Shared.Messaging.IDomainEventHandler`1, MyApp"
          },
          "shared": {
            "allowedFolders": ["Messaging"]
          }
        }
        """;

    private const string ContractsSource =
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

            public interface IDomainEventHandler<in TEvent>;
        }
        """;

    private const string AmbiguousSource =
        """
        namespace MyApp.Domain.Bookings
        {
            public sealed class BookingSnapshot : IAggregateRoot, IDomainEvent;
        }
        """;

    private const string ConfirmBookingSource =
        """
        using MyApp.Shared.Messaging;

        namespace MyApp.Features.Bookings.ConfirmBooking;

        public sealed class ConfirmBookingCommand : ICommand<bool>;

        internal sealed class ConfirmBookingHandler : IRequestHandler<ConfirmBookingCommand, bool>;

        internal static class ConfirmBookingEndpoint;
        """;

    private const string CompliantSource =
        """
        using MyApp.Shared.Messaging;

        namespace MyApp.Features.Bookings.CancelBooking;

        public sealed class CancelBookingCommand : ICommand<bool>;

        internal sealed class CancelBookingHandler : IRequestHandler<CancelBookingCommand, bool>;

        internal static class CancelBookingEndpoint;
        """;

    private const string ViolatingSource =
        """
        using MyApp.Shared.Messaging;

        namespace MyApp.Features.Bookings.CancelBooking;

        public sealed class CancelBookingCommand : ICommand<bool>;

        internal sealed class CancelBookingHandler(
            MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingCommand command) : IRequestHandler<CancelBookingCommand, bool>
        {
            public object Command { get; } = command;
        }

        internal static class CancelBookingEndpoint;
        """;
}
