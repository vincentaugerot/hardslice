using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
// --aggregate names a type of this namespace: an unknown aggregate does not compile here.
using Shop.Domain.Catalog;
using Shop.Shared.Messaging;
using Shop.Shared.Persistence;

namespace Shop.Features.Catalog.GetProduct;

// Reads one Product by its identifier: an unknown one is a null response, answered with 404.
public sealed record GetProductQuery(Guid Id) : IQuery<GetProductDto?>;

// Values only: never an entity, not even in a property. Add the values the use case reads.
public sealed record GetProductDto(Guid Id, string Name, decimal Price);

// Reads a projection without tracking: a query never loads the aggregate, never saves, and runs outside any transaction.
internal sealed class GetProductHandler(AppDbContext db) : IRequestHandler<GetProductQuery, GetProductDto?>
{
    public Task<GetProductDto?> Handle(GetProductQuery query, CancellationToken cancellationToken)
    {
        var id = new ProductId(query.Id);
        return db.Set<Product>()
            .AsNoTracking()
            .Where(aggregate => aggregate.Id == id)
            .Select(aggregate => new GetProductDto(aggregate.Id.Value, aggregate.Name, aggregate.Price))
            .SingleOrDefaultAsync(cancellationToken);
    }
}

internal static class GetProductEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/get-product/{id:guid}", async Task<Results<Ok<GetProductDto>, NotFound>> (Guid id, IMediator mediator, CancellationToken cancellationToken) =>
            await mediator.Send(new GetProductQuery(id), cancellationToken) is { } dto ? TypedResults.Ok(dto) : TypedResults.NotFound());
}
