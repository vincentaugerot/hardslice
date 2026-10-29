using System.Reflection;
using System.Runtime.Versioning;

namespace HardSlice.Analyzers.Tests;

public sealed class AnalyzerAssemblyTests
{
    private static readonly Assembly AnalyzerAssembly = Assembly.Load("HardSlice.Analyzers");

    [Fact]
    public void Targets_netstandard20_so_every_compiler_host_can_load_it()
    {
        var targetFramework = AnalyzerAssembly.GetCustomAttribute<TargetFrameworkAttribute>();

        targetFramework.ShouldNotBeNull();
        targetFramework.FrameworkName.ShouldBe(".NETStandard,Version=v2.0");
    }

    // The compiler host provides Microsoft.CodeAnalysis; any other dependency would fail to load at build time.
    [Fact]
    public void References_only_netstandard_and_the_compiler_apis()
    {
        string[] allowed = ["netstandard", "Microsoft.CodeAnalysis", "Microsoft.CodeAnalysis.CSharp"];

        AnalyzerAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ShouldAllBe(name => allowed.Contains(name));
    }
}
