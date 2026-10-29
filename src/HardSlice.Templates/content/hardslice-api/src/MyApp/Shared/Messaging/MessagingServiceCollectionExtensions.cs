namespace MyApp.Shared.Messaging;

public static class MessagingServiceCollectionExtensions
{
    // Handlers and validators are registered by their module; the pipeline behaviors by AddPipelineBehaviors.
    public static IServiceCollection AddMediator(this IServiceCollection services) =>
        services.AddScoped<IMediator, Mediator>();
}
