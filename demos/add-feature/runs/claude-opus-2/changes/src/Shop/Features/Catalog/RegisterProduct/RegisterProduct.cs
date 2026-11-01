using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Shop.Domain.Catalog;
using Shop.Shared.Messaging;
using Shop.Shared.Persistence;

namespace Shop.Features.Catalog.RegisterProduct;

// Registers a new product with its name and its price in euros; returns the identifier of the product.
public sealed record RegisterProductCommand(string Name, decimal Price) : ICommand<Guid>;

// Checks the shape of the input only: the rules of a product, its price included, stay in the aggregate.
internal sealed class RegisterProductValidator : AbstractValidator<RegisterProductCommand>
{
    public RegisterProductValidator() =>
        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(Product.NameMaxLength);
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
        // Answers 201 with the identifier of the product, located where GetProduct reads it.
        group.MapPost("/products", async Task<Created<Guid>> (RegisterProductCommand command, HttpRequest request, IMediator mediator, CancellationToken cancellationToken) =>
        {
            var id = await mediator.Send(command, cancellationToken);
            return TypedResults.Created($"{request.PathBase}{request.Path}/{id}", id);
        });
}
