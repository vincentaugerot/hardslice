using System.Xml.Linq;
using MyApp.Domain.Bookings;

namespace MyApp.ArchitectureTests;

public sealed class DomainTests
{
    private static readonly string[] RuntimeDependencyItems =
    [
        "FrameworkReference",
        "PackageReference",
        "ProjectReference",
        "Reference"
    ];

    [Fact]
    public void Project_declares_no_runtime_dependencies()
    {
        var project = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory, "ArchitectureInputs", "DomainProject.csproj"));

        var dependencies = project.Descendants()
            .Where(element => RuntimeDependencyItems.Contains(element.Name.LocalName))
            .Select(element => $"{element.Name.LocalName}: {element.Attribute("Include")?.Value}")
            .ToArray();

        dependencies.ShouldBeEmpty();
    }

    // Anything outside the base class library (ASP.NET Core, EF Core, DI, HardSlice) would leak infrastructure into the domain.
    [Fact]
    public void References_only_the_base_class_library()
    {
        typeof(Booking).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .ShouldAllBe(name => name == "netstandard" || name == "System" || name.StartsWith("System."));
    }
}
