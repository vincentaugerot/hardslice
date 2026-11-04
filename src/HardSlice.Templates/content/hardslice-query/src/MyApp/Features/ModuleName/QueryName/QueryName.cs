#if (ResultIsSingle)
using Microsoft.AspNetCore.Http.HttpResults;
#endif
using Microsoft.EntityFrameworkCore;
// --aggregate names a type of this namespace: an unknown aggregate does not compile here.
using MyApp.Domain.ModuleName;
using MyApp.Shared.Messaging;
using MyApp.Shared.Persistence;

namespace MyApp.Features.ModuleName.QueryName;

#if (ResultIsSingle)
// Reads one AggregateName by its identifier: an unknown one is a null response, answered with 404.
public sealed record QueryNameQuery(Guid Id) : IQuery<QueryNameDto?>;
#else
// Reads every AggregateName. Add the parameters of the use case, such as a filter, and their validator if any.
public sealed record QueryNameQuery : IQuery<IReadOnlyList<QueryNameDto>>;
#endif

// Values only: never an entity, not even in a property. Add the values the use case reads.
public sealed record QueryNameDto(Guid Id);

// Reads a projection without tracking: a query never loads the aggregate, never saves, and runs outside any transaction.
#if (ResultIsSingle)
internal sealed class QueryNameHandler(AppDbContext db) : IRequestHandler<QueryNameQuery, QueryNameDto?>
{
    public Task<QueryNameDto?> Handle(QueryNameQuery query, CancellationToken cancellationToken)
    {
        var id = new AggregateNameId(query.Id);
        return db.Set<AggregateName>()
            .AsNoTracking()
            .Where(aggregate => aggregate.Id == id)
            .Select(aggregate => new QueryNameDto(aggregate.Id.Value))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
#else
internal sealed class QueryNameHandler(AppDbContext db) : IRequestHandler<QueryNameQuery, IReadOnlyList<QueryNameDto>>
{
    public async Task<IReadOnlyList<QueryNameDto>> Handle(QueryNameQuery query, CancellationToken cancellationToken) =>
        await db.Set<AggregateName>()
            .AsNoTracking()
            .OrderBy(aggregate => aggregate.Id)
            .Select(aggregate => new QueryNameDto(aggregate.Id.Value))
            .ToListAsync(cancellationToken);
}
#endif

internal static class QueryNameEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
#if (ResultIsSingle)
        group.MapGet("/query-id-route", async Task<Results<Ok<QueryNameDto>, NotFound>> (Guid id, IMediator mediator, CancellationToken cancellationToken) =>
            await mediator.Send(new QueryNameQuery(id), cancellationToken) is { } dto ? TypedResults.Ok(dto) : TypedResults.NotFound());
#else
        group.MapGet("/query-route", async (IMediator mediator, CancellationToken cancellationToken) =>
            TypedResults.Ok(await mediator.Send(new QueryNameQuery(), cancellationToken)));
#endif
}
