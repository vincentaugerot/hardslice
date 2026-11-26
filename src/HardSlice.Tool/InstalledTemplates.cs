using System.Text.RegularExpressions;

namespace HardSlice.Tool;

// The item templates that dotnet new serves on the machine: those of the version of HardSlice.Templates installed in its hive,
// which dotnet hardslice upgrade never changes, since it generates in a hive of its own. After an upgrade, dotnet new would keep
// generating the modules, aggregates and slices of the previous version, without a word for an option that already existed.
internal static partial class InstalledTemplates
{
    // dotnet new answers in under a second: a hive that it cannot read quickly never delays the check for long.
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public static readonly string[] ListCommand = ["new", "uninstall"];

    // The command that brings the item templates to the version, run from the root of the solution, whose nuget.config files
    // choose the source as for the upgrade. dotnet new replaces an installed version with it, without --force.
    public static string InstallCommand(string version) => $"dotnet new install {DotnetNewGenerator.PackageId}@{version}";

    // The last line of an upgrade to the version.
    public static string AfterUpgrade(string version) =>
        $"==> Before generating anything with dotnet new, run '{InstallCommand(version)}' from the root of the solution: "
        + "otherwise dotnet new serves the item templates of the version installed before, and an option that already existed generates the code of that version without a word.";

    // The warning of the check when the item templates installed are not at the version of the tool.
    public static string Warning(string installed, string version) =>
        $"warning: the item templates of dotnet new are those of {DotnetNewGenerator.PackageId} {installed}, not {version} as dotnet hardslice: "
        + $"dotnet new hardslice-module, hardslice-command and the other item templates generate the code of {installed}. "
        + $"Run '{InstallCommand(version)}' from the root of the solution before generating anything. The check does not fail on it.";

    // The version of HardSlice.Templates that dotnet new serves, read from the list of 'dotnet new uninstall', or null when it is
    // not installed, when dotnet new fails or does not answer within the timeout, or when its list cannot be read.
    public static string? Version(ICommandRunner runner, string workingDirectory)
    {
        var result = runner.Capture("dotnet", ListCommand, workingDirectory, Timeout);
        return result.ExitCode == 0 ? Parse(result.Output) : null;
    }

    // The list names each package on a line of its own, indented, followed by its version on the next line, in the language of
    // the CLI: '      Version: 0.3.0-preview.2', or '      Version : 0.3.0-preview.2' in French. The uninstall command that
    // closes each entry names the package after other words, and is not taken for its header.
    public static string? Parse(string listing)
    {
        var lines = listing.Split(["\r\n", "\n"], StringSplitOptions.None);
        for (var index = 0; index + 1 < lines.Length; index++)
        {
            var line = lines[index];
            if (line.Length == line.TrimStart().Length || !line.Trim().Equals(DotnetNewGenerator.PackageId, StringComparison.OrdinalIgnoreCase))
                continue;

            var version = VersionLine().Match(lines[index + 1]);
            return version.Success ? version.Groups["version"].Value : null;
        }

        return null;
    }

    public static bool Matches(string installed, string version) => installed.Equals(version, StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"^\s+[^:\s][^:]*:\s*(?<version>\d+\.\d+\.\d+(?:[-.][0-9A-Za-z.-]+)?)\s*$")]
    private static partial Regex VersionLine();
}
