using System.Reflection;

namespace HardSlice.Tool;

internal static class ToolVersion
{
    public static string Current { get; } = Of(typeof(ToolVersion).Assembly);

    // The package version of the assembly, without the source revision the SDK appends after '+'.
    public static string Of(Assembly assembly)
    {
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? throw new InvalidOperationException($"{assembly.GetName().Name} has no informational version.");
        var revision = version.IndexOf('+', StringComparison.Ordinal);
        return revision < 0 ? version : version[..revision];
    }

    // The version of the release that names the tag of the documentation: <version> of a local build <version>.dev.<n>
    // (scripts/pack-local.sh), whose tag does not exist, or the version itself.
    public static string Release(string version)
    {
        var local = version.LastIndexOf(".dev.", StringComparison.Ordinal);
        return local > 0 && local + 5 < version.Length && version[(local + 5)..].All(char.IsAsciiDigit) ? version[..local] : version;
    }
}
