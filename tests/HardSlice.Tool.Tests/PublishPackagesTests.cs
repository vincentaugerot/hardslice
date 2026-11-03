using System.Diagnostics;
using System.IO.Compression;

namespace HardSlice.Tool.Tests;

// PB-059: the publish job of the release pushes the packages of its own run only. It used to push with --skip-duplicate: a rerun
// after a partial push, or a run approved after its tag moved, published a version that mixed two commits.
public sealed class PublishPackagesTests : IDisposable
{
    private const string Version = "9.9.9-preview.1";

    private static readonly string[] Packages = ["HardSlice.Analyzers", "HardSlice.Tool", "HardSlice.Templates"];

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"hardslice-publish-{Guid.NewGuid():N}");

    private readonly string _built;

    private readonly string _served;

    private readonly string _remote;

    private readonly string _commit;

    public PublishPackagesTests()
    {
        _built = Directory.CreateDirectory(Path.Combine(_directory, "built")).FullName;
        _served = Directory.CreateDirectory(Path.Combine(_directory, "served")).FullName;
        _remote = Directory.CreateDirectory(Path.Combine(_directory, "remote")).FullName;
        foreach (var package in Packages)
            WritePackage(Path.Combine(_built, $"{package}.{Version}.nupkg"), $"{package} of this run");

        Run("git", _remote, "init", "--quiet");
        Run("git", _remote, "-c", "user.name=HardSlice", "-c", "user.email=release@example.invalid", "commit", "--quiet", "--allow-empty", "-m", "release");
        _commit = Run("git", _remote, "rev-parse", "HEAD").Trim();
        Run("git", _remote, "-c", "user.name=HardSlice", "-c", "user.email=release@example.invalid", "tag", "-a", $"v{Version}", "-m", "release");
    }

    [Fact]
    public void Skips_a_package_served_with_the_content_of_the_run_and_pushes_the_others_in_order()
    {
        // NuGet.org serves the package with its repository signature added: the bytes differ, the content does not.
        WritePackage(Path.Combine(_served, $"HardSlice.Analyzers.{Version}.nupkg"), "HardSlice.Analyzers of this run", signed: true);

        var (exitCode, output) = Publish(_commit);

        exitCode.ShouldBe(0, output);
        output.ShouldContain($"The tag v{Version} still points to {_commit}.");
        output.ShouldContain($"HardSlice.Analyzers {Version} is already served with the content of this run: skipped.");
        output.Split('\n').Where(line => line.StartsWith("Would push", StringComparison.Ordinal))
            .ShouldBe([$"Would push HardSlice.Tool {Version}.", $"Would push HardSlice.Templates {Version}."]);
    }

    [Fact]
    public void Pushes_nothing_when_a_served_package_has_the_content_of_another_commit()
    {
        WritePackage(Path.Combine(_served, $"HardSlice.Tool.{Version}.nupkg"), "HardSlice.Tool of an earlier run", signed: true);

        var (exitCode, output) = Publish(_commit);

        exitCode.ShouldBe(1, output);
        output.ShouldContain($"NuGet.org already serves a HardSlice.Tool {Version} whose content differs");
        output.ShouldNotContain("Would push");
    }

    [Fact]
    public void Pushes_nothing_when_the_tag_no_longer_points_to_the_commit_of_the_run()
    {
        var (exitCode, output) = Publish(new string('0', 40));

        exitCode.ShouldBe(1, output);
        output.ShouldContain($"the tag v{Version} of {_remote} points to {_commit}, not to {new string('0', 40)}");
        output.ShouldNotContain("Would push");
    }

    [Fact]
    public void Package_metadata_require_the_readme_the_icon_and_the_release_notes_at_the_tag()
    {
        var complete = Path.Combine(_directory, "complete.nupkg");
        var withoutIcon = Path.Combine(_directory, "without-icon.nupkg");
        WritePackage(complete, "content");
        WritePackage(withoutIcon, "content", icon: false);

        Run("python3", Repository.Root, "-I", "scripts/nupkg.py", "metadata", complete, Version);
        var (exitCode, output) = Execute("python3", Repository.Root, "-I", "scripts/nupkg.py", "metadata", withoutIcon, Version);

        exitCode.ShouldBe(1, output);
        output.ShouldContain("does not ship icon.png at its root");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private (int ExitCode, string Output) Publish(string commit) =>
        Execute("bash", Repository.Root, "scripts/publish-packages.sh", Version, _built, "--commit", commit, "--remote", _remote, "--feed", _served, "--dry-run");

    private static void WritePackage(string path, string content, bool signed = false, bool icon = true)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        Write(archive, "Package.nuspec", $"""
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>Package</id>
                <version>{Version}</version>
                <readme>README.md</readme>
                <icon>icon.png</icon>
                <releaseNotes>https://github.com/vincentaugerot/hardslice/blob/v{Version}/CHANGELOG.md</releaseNotes>
              </metadata>
            </package>
            """);
        Write(archive, "README.md", content);
        if (icon)
            Write(archive, "icon.png", "png");
        if (signed)
            Write(archive, ".signature.p7s", "signature of NuGet.org");
    }

    private static void Write(ZipArchive archive, string name, string content)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open());
        writer.Write(content);
    }

    private static string Run(string command, string directory, params string[] arguments)
    {
        var (exitCode, output) = Execute(command, directory, arguments);
        exitCode.ShouldBe(0, output);
        return output;
    }

    private static (int ExitCode, string Output) Execute(string command, string directory, params string[] arguments)
    {
        var start = new ProcessStartInfo(command)
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start) ?? throw new InvalidOperationException($"{command} did not start.");
        var error = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output + error.Result);
    }
}
