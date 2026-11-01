using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Shop.Domain.Catalog;
using Shop.Shared.Messaging;
using Shop.Shared.Persistence;

namespace Shop.Features.Catalog.RegisterProduct;

public sealed record RegisterProductCommand(string Name, decimal Price) : ICommand<Guid>;

// Checks the shape of the input only: business rules stay in the aggregate.
internal sealed class RegisterProductValidator : AbstractValidator<RegisterProductCommand>
{
    public RegisterProductValidator() =>
        RuleFor(command => command.Name).NotEmpty().MaximumLength(100);
}

// Changes the aggregate only: TransactionBehavior saves and commits it, and publishes its domain events before.
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
        group.MapPost("/products", async Task<Created<Guid>> (RegisterProductCommand command, IMediator mediator, CancellationToken cancellationToken) =>
        {
            var id = await mediator.Send(command, cancellationToken);
            return TypedResults.Created($"/catalog/products/{id}", id);
        });
}
