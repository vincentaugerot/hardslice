using FluentValidation;
using Shop.Domain.Catalog;
using Shop.Shared.Messaging;
using Shop.Shared.Persistence;

namespace Shop.Features.Catalog.RegisterProduct;

public sealed record RegisterProductCommand(string Name, decimal Price) : ICommand<Guid>;

internal sealed class RegisterProductValidator : AbstractValidator<RegisterProductCommand>
{
    public RegisterProductValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Price).GreaterThan(0);
    }
}

internal sealed class RegisterProductHandler(AppDbContext db) : IRequestHandler<RegisterProductCommand, Guid>
{
    public Task<Guid> Handle(RegisterProductCommand command, CancellationToken cancellationToken)
    {
        var product = Product.Register(command.Name, command.Price);
        db.Set<Product>().Add(product);
        return Task.FromResult(product.Id.Value);
    }
}

internal static class RegisterProductEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/register-product", async (RegisterProductCommand command, IMediator mediator, CancellationToken cancellationToken) =>
            TypedResults.Ok(await mediator.Send(command, cancellationToken)));
}
