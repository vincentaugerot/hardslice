using System.Reflection;
using MyApp.Domain.Bookings;
using MyApp.Features.Bookings;

namespace MyApp.ArchitectureTests;

public sealed class RuntimeDependencyTests
{
    public static TheoryData<string> ApplicationAssemblies =>
    [
        typeof(Booking).Assembly.GetName().Name!,
        typeof(BookingsModule).Assembly.GetName().Name!
    ];

    // HardSlice is build-time tooling only: the application must run without any HardSlice assembly.
    [Theory]
    [MemberData(nameof(ApplicationAssemblies))]
    public void Application_assemblies_do_not_reference_HardSlice(string assemblyName)
    {
        Assembly.Load(assemblyName).GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .ShouldNotContain(name => name.StartsWith("HardSlice"));
    }
}
