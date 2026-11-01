using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Shop.Domain.Catalog;
using Shop.Shared.Messaging;
using Shop.Shared.Persistence;

namespace Shop.Features.Catalog.GetProduct;

public sealed record GetProductQuery(Guid Id) : IQuery<GetProductDto?>;

public sealed record GetProductDto(Guid Id, string Name, decimal Price);

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
