using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Shared.Persistence;

namespace MyApp.Tests.Shared.Persistence;

// AddSingletonBuiltAtStart registers a factory, which reads the services registered after it, and builds its service when the
// server resolves its startup filters, once.
public sealed class BuiltAtStartServiceCollectionExtensionsTests
{
    [Fact]
    public void Builds_the_service_once_when_the_server_starts_from_a_service_registered_after_it()
    {
        var builds = 0;
        var services = new ServiceCollection();
        services.AddSingletonBuiltAtStart(provider =>
        {
            builds++;
            return new Rates(provider.GetRequiredService<Catalog>().Rate);
        });
        services.AddSingleton(new Catalog(0.15m));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        builds.ShouldBe(0);
        provider.GetServices<IStartupFilter>().ShouldHaveSingleItem();
        builds.ShouldBe(1);
        provider.GetRequiredService<Rates>().Rate.ShouldBe(0.15m);
        provider.GetServices<IStartupFilter>().ShouldHaveSingleItem();
        builds.ShouldBe(1);
    }

    [Fact]
    public void Throws_the_exception_of_its_factory_when_the_server_starts()
    {
        var services = new ServiceCollection();
        services.AddSingletonBuiltAtStart<Rates>(_ => throw new InvalidOperationException("data/2026/rates.json: $.rate is missing."));
        using var provider = services.BuildServiceProvider();

        Should.Throw<InvalidOperationException>(() => provider.GetServices<IStartupFilter>().ToList())
            .Message.ShouldBe("data/2026/rates.json: $.rate is missing.");
    }

    [Fact]
    public void Refuses_a_factory_that_builds_null()
    {
        var services = new ServiceCollection();
        services.AddSingletonBuiltAtStart<Rates>(_ => null!);
        using var provider = services.BuildServiceProvider();

        Should.Throw<ArgumentNullException>(() => provider.GetServices<IStartupFilter>().ToList());
    }

    private sealed record Catalog(decimal Rate);

    private sealed record Rates(decimal Rate);
}
