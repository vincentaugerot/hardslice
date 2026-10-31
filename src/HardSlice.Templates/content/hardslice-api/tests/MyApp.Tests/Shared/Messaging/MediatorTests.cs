using Microsoft.Extensions.DependencyInjection;
using MyApp.Domain;
using MyApp.Shared.Messaging;

namespace MyApp.Tests.Shared.Messaging;

public sealed class MediatorTests
{
    [Fact]
    public async Task Sends_a_request_to_its_handler_and_returns_its_response()
    {
        using var services = Services().BuildServiceProvider();

        var response = await services.GetRequiredService<IMediator>().Send(new Ping("hello"), TestContext.Current.CancellationToken);

        response.ShouldBe("pong hello");
    }

    [Fact]
    public async Task Runs_the_behaviors_in_registration_order_around_the_handler()
    {
        using var services = Services()
            .AddScoped<IPipelineBehavior<Ping, string>>(provider => new RecordingBehavior("outer", provider.GetRequiredService<Calls>()))
            .AddScoped<IPipelineBehavior<Ping, string>>(provider => new RecordingBehavior("inner", provider.GetRequiredService<Calls>()))
            .BuildServiceProvider();

        await services.GetRequiredService<IMediator>().Send(new Ping("hello"), TestContext.Current.CancellationToken);

        services.GetRequiredService<Calls>().ShouldBe(["outer before", "inner before", "handler", "inner after", "outer after"]);
    }

    [Fact]
    public async Task Never_reaches_the_handler_when_a_behavior_does_not_call_next()
    {
        using var services = Services()
            .AddScoped<IPipelineBehavior<Ping, string>, RejectingBehavior>()
            .BuildServiceProvider();

        await Should.ThrowAsync<InvalidOperationException>(() =>
            services.GetRequiredService<IMediator>().Send(new Ping("hello"), TestContext.Current.CancellationToken));

        services.GetRequiredService<Calls>().ShouldBeEmpty();
    }

    [Fact]
    public async Task Fails_when_the_request_has_no_handler()
    {
        using var services = new ServiceCollection().AddMediator().BuildServiceProvider();

        await Should.ThrowAsync<InvalidOperationException>(() =>
            services.GetRequiredService<IMediator>().Send(new Ping("hello"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void A_request_cannot_be_widened_to_an_unregistered_response_contract()
    {
        object command = new Ping("hello");
        object query = new ReadMessage();

        (command is IRequest<object>).ShouldBeFalse();
        (command is ICommand<object>).ShouldBeFalse();
        (query is IRequest<object>).ShouldBeFalse();
        (query is IQuery<object>).ShouldBeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Dispatches_each_declared_response_contract_in_either_order_across_scopes(bool stringFirst)
    {
        using var services = new ServiceCollection().AddMediator()
            .AddScoped<IRequestHandler<MultiResponse, string>, StringHandler>()
            .AddScoped<IRequestHandler<MultiResponse, int>, NumberHandler>()
            .BuildServiceProvider();

        foreach (var sendStringFirst in new[] { stringFirst, !stringFirst })
        {
            using var scope = services.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var request = new MultiResponse();
            if (sendStringFirst)
            {
                (await mediator.Send<string>(request, TestContext.Current.CancellationToken)).ShouldBe("pong");
                (await mediator.Send<int>(request, TestContext.Current.CancellationToken)).ShouldBe(42);
            }
            else
            {
                (await mediator.Send<int>(request, TestContext.Current.CancellationToken)).ShouldBe(42);
                (await mediator.Send<string>(request, TestContext.Current.CancellationToken)).ShouldBe("pong");
            }
        }
    }

    // A domain event handler runs inside the transaction of a command: a request it sends would open another one.
    [Fact]
    public async Task Refuses_a_request_sent_while_a_domain_event_handler_runs_and_names_the_handler()
    {
        using var services = Services().BuildServiceProvider();
        var mediator = services.GetRequiredService<IMediator>();

        using (DomainEventHandlerScope.Enter(typeof(SampleEventHandler), typeof(SampleEvent)))
        {
            var exception = await Should.ThrowAsync<InvalidOperationException>(() => mediator.Send(new Ping("hello"), TestContext.Current.CancellationToken));

            exception.Message.ShouldStartWith("Domain event handler 'SampleEventHandler' sent 'Ping' through IMediator while handling 'SampleEvent'. ");
        }

        services.GetRequiredService<Calls>().ShouldBeEmpty();
        (await mediator.Send(new Ping("again"), TestContext.Current.CancellationToken)).ShouldBe("pong again");
    }

    private static IServiceCollection Services() =>
        new ServiceCollection()
            .AddMediator()
            .AddSingleton<Calls>()
            .AddScoped<IRequestHandler<Ping, string>, PingHandler>();

    public sealed record Ping(string Message) : ICommand<string>;

    public sealed record ReadMessage : IQuery<string>;

    public sealed record MultiResponse : ICommand<string>, ICommand<int>;

    private sealed class StringHandler : IRequestHandler<MultiResponse, string>
    {
        public Task<string> Handle(MultiResponse request, CancellationToken cancellationToken) => Task.FromResult("pong");
    }

    private sealed class NumberHandler : IRequestHandler<MultiResponse, int>
    {
        public Task<int> Handle(MultiResponse request, CancellationToken cancellationToken) => Task.FromResult(42);
    }

    private sealed class Calls : List<string>;

    private sealed record SampleEvent : IDomainEvent;

    private sealed class SampleEventHandler : IDomainEventHandler<SampleEvent>
    {
        public Task Handle(SampleEvent domainEvent, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class PingHandler(Calls calls) : IRequestHandler<Ping, string>
    {
        public Task<string> Handle(Ping request, CancellationToken cancellationToken)
        {
            calls.Add("handler");
            return Task.FromResult($"pong {request.Message}");
        }
    }

    private sealed class RecordingBehavior(string name, Calls calls) : IPipelineBehavior<Ping, string>
    {
        public async Task<string> Handle(Ping request, RequestHandlerDelegate<string> next, CancellationToken cancellationToken)
        {
            calls.Add($"{name} before");
            var response = await next();
            calls.Add($"{name} after");
            return response;
        }
    }

    private sealed class RejectingBehavior : IPipelineBehavior<Ping, string>
    {
        public Task<string> Handle(Ping request, RequestHandlerDelegate<string> next, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Rejected before the handler.");
    }
}
