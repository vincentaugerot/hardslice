using System.Net;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Shared.Messaging;
using MyApp.Shared.Persistence;

namespace MyApp.IntegrationTests;

// The application as Program.cs composes it, modules included. AppFactory validates every service registration when the host
// starts, as Development does.
public sealed class ProgramTests(PostgresDatabase database)
{
    // The contracts by which a module registers the handlers and validators of its slices, reactions included.
    private static readonly Type[] RegisteredContracts = [typeof(IRequestHandler<,>), typeof(IValidator<>), typeof(IDomainEventHandler<>)];

    [Fact]
    public async Task Starts_and_answers_HTTP_requests_on_its_migrated_database()
    {
        await using var app = new AppFactory(database);

        var response = await app.CreateClient().GetAsync($"/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Database.CanConnectAsync(TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await db.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
    }

    // A slice its module forgot to register compiles, but its endpoint fails at the first request, and a forgotten reaction
    // never runs: every handler and validator declared in the application must be resolved from the container, by its contract.
    [Fact]
    public async Task Resolves_every_handler_and_validator_of_the_application_by_its_contract()
    {
        await using var app = new AppFactory(database);
        await using var scope = app.Services.CreateAsyncScope();

        var unregistered = typeof(Program).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false })
            .SelectMany(type => type.GetInterfaces()
                .Where(contract => contract.IsGenericType && RegisteredContracts.Contains(contract.GetGenericTypeDefinition()))
                .Select(contract => (Type: type, Contract: contract)))
            .Where(registration => !scope.ServiceProvider.GetServices(registration.Contract)
                .Any(service => service?.GetType() == registration.Type))
            .Select(registration => $"'{registration.Type.Name}' as '{Describe(registration.Contract)}'")
            .ToArray();

        unregistered.ShouldBeEmpty(
            "These types are not registered: add each to the Add<Module>Module method of its module, by its contract, "
            + "such as .AddScoped<IRequestHandler<TRequest, TResponse>, THandler>(), .AddScoped<IValidator<TRequest>, TValidator>() "
            + "or .AddScoped<IDomainEventHandler<TEvent>, THandler>().");
    }

    private static string Describe(Type type) =>
        type.IsGenericType
            ? $"{type.Name[..type.Name.IndexOf('`')]}<{string.Join(", ", type.GetGenericArguments().Select(Describe))}>"
            : type.Name;
}
