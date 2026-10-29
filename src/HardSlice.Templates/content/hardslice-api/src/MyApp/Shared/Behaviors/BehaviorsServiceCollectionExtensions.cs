using MyApp.Shared.Messaging;

namespace MyApp.Shared.Behaviors;

public static class BehaviorsServiceCollectionExtensions
{
    // Registration order is execution order, outermost first: the logs see every outcome, validation rejects
    // an invalid request before any transaction opens, and the transaction wraps the handler alone.
    public static IServiceCollection AddPipelineBehaviors(this IServiceCollection services) =>
        services
            .AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>))
            .AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>))
            .AddScoped(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
}
