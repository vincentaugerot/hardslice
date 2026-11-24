using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Shared.Persistence;

namespace MyApp.IntegrationTests.Shared.Persistence;

// A service registered with AddSingletonBuiltAtStart is built when the application starts, under AppFactory too, which starts no
// hosted service of the application: a factory that throws fails the start, before any request.
public sealed class BuiltAtStartTests(PostgresDatabase database)
{
    [Fact]
    public void Builds_the_service_when_the_application_starts_before_any_request()
    {
        var builds = 0;
        using var app = new AppFactory(database).WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingletonBuiltAtStart(_ =>
            {
                builds++;
                return new ReferenceData("2026");
            })));

        _ = app.Services;

        builds.ShouldBe(1);
        app.Services.GetRequiredService<ReferenceData>().Edition.ShouldBe("2026");
        builds.ShouldBe(1);
    }

    [Fact]
    public void Fails_the_start_of_the_application_when_its_factory_throws()
    {
        using var app = new AppFactory(database).WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingletonBuiltAtStart<ReferenceData>(_ => throw new InvalidDataException("data/2026/rates.json: $.rate is missing."))));

        Should.Throw<InvalidDataException>(() => app.CreateClient())
            .Message.ShouldBe("data/2026/rates.json: $.rate is missing.");
    }

    private sealed record ReferenceData(string Edition);
}
