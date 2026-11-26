using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Shared.Persistence;

namespace MyApp.IntegrationTests.Shared.Persistence;

// A service registered with AddSingletonBuiltAtStart is built when the application starts, under AppFactory too, which starts no
// hosted service of the application: a factory that throws fails the start, before any request, and app.StartFailure<TException>()
// reads its exception.
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

        app.StartFailure<InvalidDataException>().Message.ShouldBe("data/2026/rates.json: $.rate is missing.");
    }

    [Fact]
    public void Tells_a_test_of_a_failed_start_that_the_application_started()
    {
        using var app = new AppFactory(database);

        Should.Throw<InvalidOperationException>(() => app.StartFailure<InvalidDataException>())
            .Message.ShouldStartWith("StartFailure: the application started, without InvalidDataException.");
    }

    [Fact]
    public void Tells_a_test_of_a_failed_start_the_exceptions_of_another_type_that_stopped_it()
    {
        using var app = new AppFactory(database).WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingletonBuiltAtStart<ReferenceData>(_ => throw new FormatException("data/2026/rates.json: $.rate is no number."))));

        Should.Throw<InvalidOperationException>(() => app.StartFailure<InvalidDataException>())
            .Message.ShouldContain("StartFailure: the start of the application failed with no InvalidDataException, but with FormatException: data/2026/rates.json: $.rate is no number.");
    }

    private sealed record ReferenceData(string Edition);
}
