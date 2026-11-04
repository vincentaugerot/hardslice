namespace HardSlice.Tool;

// Generates the hardslice-api template of a version of HardSlice.Templates, as dotnet hardslice upgrade compares it with a solution.
internal interface ITemplateGenerator
{
    // Generates the template at the version, with or without the sample, under the name of the solution, into target, a folder
    // that does not exist yet inside workDirectory. Returns false after writing why it could not.
    bool Generate(Solution solution, string version, bool sample, string workDirectory, string target, TextWriter error);
}

// dotnet new, in a template hive of its own inside the work folder: the templates installed on the machine, of any version,
// are neither read nor changed. The package is installed from the root of the solution, so that the nuget.config files of the
// solution and of its parent folders choose where it comes from, as for its analyzers and its tool.
internal sealed class DotnetNewGenerator(ICommandRunner runner) : ITemplateGenerator
{
    public const string PackageId = "HardSlice.Templates";

    public bool Generate(Solution solution, string version, bool sample, string workDirectory, string target, TextWriter error)
    {
        string[] hive = ["--debug:custom-hive", Path.Combine(workDirectory, "template-hive")];
        string[] install = ["new", "install", $"{PackageId}@{version}", .. hive];
        string[] generate = ["new", "hardslice-api", "--name", solution.Name, "--output", target, .. (sample ? Array.Empty<string>() : ["--no-sample"]), .. hive];
        return Succeeds(install, solution.Root, $"{PackageId} {version} could not be installed. Check that a package source of the nuget.config files of the solution serves it", error)
            && Succeeds(generate, workDirectory, $"the template hardslice-api of {PackageId} {version} could not be generated", error);
    }

    private bool Succeeds(string[] arguments, string workingDirectory, string failure, TextWriter error)
    {
        var result = runner.Capture("dotnet", arguments, workingDirectory);
        if (result.ExitCode == 0)
            return true;

        error.WriteLine($"error: 'dotnet {string.Join(' ', arguments)}' exited with code {result.ExitCode}: {failure}. Nothing was written to the solution.");
        foreach (var text in new[] { result.Output, result.Error }.Where(text => text.Trim().Length > 0))
            error.WriteLine(text.TrimEnd());
        return false;
    }
}
