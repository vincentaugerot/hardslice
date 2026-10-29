using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Shared.Behaviors;
using MyApp.Shared.Messaging;
using MyApp.Shared.Persistence;

namespace MyApp.Tests.Shared.Behaviors;

// The order the host registers is the order every request goes through.
public sealed class BehaviorsServiceCollectionExtensionsTests
{
    [Fact]
    public void Commands_go_through_logging_then_validation_then_the_transaction()
    {
        using var services = Services();

        BehaviorsOf<SampleCommand, int>(services).ShouldBe(
            [typeof(LoggingBehavior<,>), typeof(ValidationBehavior<,>), typeof(TransactionBehavior<,>)]);
    }

    [Fact]
    public void Queries_go_through_logging_then_validation_without_a_transaction()
    {
        using var services = Services();

        BehaviorsOf<SampleQuery, int>(services).ShouldBe([typeof(LoggingBehavior<,>), typeof(ValidationBehavior<,>)]);
    }

    private static Type[] BehaviorsOf<TRequest, TResponse>(IServiceProvider services) where TRequest : notnull =>
        services.GetServices<IPipelineBehavior<TRequest, TResponse>>()
            .Select(behavior => behavior.GetType().GetGenericTypeDefinition())
            .ToArray();

    // The transaction behavior needs an AppDbContext, which never connects here.
    private static ServiceProvider Services() =>
        new ServiceCollection()
            .AddLogging()
            .AddPersistence(new ConfigurationBuilder()
                .AddInMemoryCollection([new($"ConnectionStrings:{PersistenceServiceCollectionExtensions.ConnectionStringName}", "Host=localhost")])
                .Build())
            .AddPipelineBehaviors()
            .BuildServiceProvider();

    public sealed record SampleCommand : ICommand<int>;

    public sealed record SampleQuery : IQuery<int>;
}
