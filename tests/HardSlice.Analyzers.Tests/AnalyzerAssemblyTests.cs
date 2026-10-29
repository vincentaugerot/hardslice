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

    // Analyzer dependencies must be provided by the compiler host. Add host assemblies explicitly when required.
    [Fact]
    public void References_only_framework_and_compiler_host_assemblies()
    {
        string[] allowed =
        [
            "netstandard",
            "Microsoft.CodeAnalysis",
            "Microsoft.CodeAnalysis.CSharp",
            "System.Collections.Immutable"
        ];

        AnalyzerAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ShouldAllBe(name => allowed.Contains(name));
    }
}
