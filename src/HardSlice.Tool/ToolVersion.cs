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
}
