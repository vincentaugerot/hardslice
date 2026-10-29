using System.Reflection;
using System.Xml.Linq;

namespace MyApp.ArchitectureTests;

// HS001: the domain depends on the base class library only, never on another project, EF Core, ASP.NET Core or the DI.
public sealed class DomainTests
{
    private const string Rule = "The domain depends on the base class library only: no other project, EF Core, ASP.NET Core or dependency injection.";
    private const string Doc = "Doc: 'docs/rules/HS001.md'.";

    private static readonly string[] DependencyItems = ["FrameworkReference", "PackageReference", "ProjectReference", "Reference"];

    [Fact]
    public void Project_declares_no_dependencies()
    {
        var projectPath = $"src/{Application.DomainName}/{Application.DomainName}.csproj";
        var project = XDocument.Load(Path.Combine(Application.SolutionRoot, projectPath));

        Violations.ShouldBeNone(project.Descendants()
            .Where(element => DependencyItems.Contains(element.Name.LocalName))
            .Select(element =>
                $"HS001: '{projectPath}' declares {element.Name.LocalName} '{element.Attribute("Include")?.Value}'. {Rule} "
                + $"Fix: remove the reference and move the code that needs it to 'src/{Application.WebName}', in the module or slice that uses it. {Doc}"));
    }

    // Assembly/module attributes also introduce references, even if no domain type uses their types.
    [Fact]
    public void Assembly_references_only_the_base_class_library()
    {
        Violations.ShouldBeNone(
            from reference in Application.DomainAssembly.GetReferencedAssemblies()
            where !IsBaseClassLibrary(reference.Name!)
            select $"HS001: assembly '{Application.DomainName}' references '{reference.Name}'. {Rule} "
                + $"Fix: remove the reference and move the code or assembly attribute that needs it to 'src/{Application.WebName}'. {Doc}");
    }

    // Read in the compiled assembly: whatever the project file says, a domain type never uses a type outside the base class library.
    [Fact]
    public void Types_use_only_the_base_class_library()
    {
        Violations.ShouldBeNone(
            from type in Application.TypesOf(Application.DomainAssembly)
            from used in Application.TypesUsedBy(type)
            where !Application.IsDeclaredIn(used, Application.DomainAssembly) && !IsBaseClassLibrary(AssemblyOf(used))
            select $"HS001: '{Application.DisplayName(type)}' in the domain uses '{Application.DisplayName(used)}' from '{AssemblyOf(used)}'. {Rule} "
                + $"Fix: keep the business rule in the domain and move the code that needs '{Application.DisplayName(used)}' to 'src/{Application.WebName}', "
                + $"in the module or slice that uses it. {Doc}");
    }

    private static bool IsBaseClassLibrary(string assemblyName) =>
        assemblyName is "System" or "netstandard" or "mscorlib" || assemblyName.StartsWith("System.", StringComparison.Ordinal);

    private static string AssemblyOf(ReferencedType type) => new AssemblyName(type.AssemblyFullName).Name!;
}
