using System.Diagnostics;
using System.IO.Compression;
using System.Xml.Linq;

namespace HardSlice.Analyzers.Tests;

public sealed class AnalyzerPackageTests
{
    [Fact]
    public async Task Package_is_analyzer_only_and_enforces_HS016_for_a_consumer()
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
            var packRestore = await RunDotnetAsync(
                repositoryRoot,
                nugetPackagesDirectory: null,
                cancellationToken,
                "restore",
                analyzerProject,
                $"-p:BaseOutputPath={packOutput}",
                $"-p:BaseIntermediateOutputPath={packIntermediate}",
                "-p:DefaultItemExcludes=obj/**");
            packRestore.ExitCode.ShouldBe(0, packRestore.Output);

            var pack = await RunDotnetAsync(
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
            var consumerSource = Path.Combine(consumerDirectory, "Program.cs");
            await File.WriteAllTextAsync(consumerProject, ConsumerProject(packageVersion), cancellationToken);
            await File.WriteAllTextAsync(consumerSource, CompliantSource, cancellationToken);

            var restore = await RunDotnetAsync(
                consumerDirectory,
                consumerPackages,
                cancellationToken,
                "restore",
                consumerProject,
                "--source", feedDirectory,
                "--no-cache");
            restore.ExitCode.ShouldBe(0, restore.Output);

            var publishDirectory = Path.Combine(temporaryDirectory, "publish");
            var publish = await RunDotnetAsync(
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

            var violatingBuild = await RunDotnetAsync(
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
        entries.Any(entry => entry.StartsWith("lib/", StringComparison.Ordinal)).ShouldBeFalse();
        entries.Any(entry => entry.StartsWith("ref/", StringComparison.Ordinal)).ShouldBeFalse();
        entries.Any(entry => entry.StartsWith("runtimes/", StringComparison.Ordinal)).ShouldBeFalse();

        var nuspecEntry = archive.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        using var nuspec = nuspecEntry.Open();
        var document = XDocument.Load(nuspec);
        document.Descendants().Where(element => element.Name.LocalName == "dependency").ShouldBeEmpty();
    }

    private static async Task<DotnetResult> RunDotnetAsync(
        string workingDirectory,
        string? nugetPackagesDirectory,
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        if (nugetPackagesDirectory is not null)
            startInfo.Environment["NUGET_PACKAGES"] = nugetPackagesDirectory;
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
            throw new InvalidOperationException("The dotnet process could not be started.");

        try
        {
            var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            return new DotnetResult(process.ExitCode, await standardOutput, await standardError);
        }
        finally
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) when (process.HasExited)
            {
                // The process exited between the state check and Kill.
            }

            await process.WaitForExitAsync(CancellationToken.None);
        }
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

    private const string CompliantSource =
        """
        namespace MyApp.Features.Bookings.ConfirmBooking
        {
            public sealed class ConfirmBookingCommand;
        }

        namespace MyApp.Features.Bookings.CancelBooking
        {
            internal sealed class CancelBookingHandler;
        }
        """;

    private const string ViolatingSource =
        """
        namespace MyApp.Features.Bookings.ConfirmBooking
        {
            public sealed class ConfirmBookingCommand;
        }

        namespace MyApp.Features.Bookings.CancelBooking
        {
            internal sealed class CancelBookingHandler(
                MyApp.Features.Bookings.ConfirmBooking.ConfirmBookingCommand command);
        }
        """;

    private sealed record DotnetResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public string Output => $"{StandardOutput}{Environment.NewLine}{StandardError}";
    }
}
