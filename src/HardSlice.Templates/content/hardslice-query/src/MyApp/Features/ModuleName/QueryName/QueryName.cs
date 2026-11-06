#if (ResultTakesFilters)
using FluentValidation;
#endif
#if (ResultIsSingle || ResultIsSummary)
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
public sealed record QueryNameQuery(Guid Id) : IQuery<ResultDtoName?>;
#elif (ResultIsFiltered)
// Reads the AggregateName that its filters select. Add the other filters of the use case, each with its rule.
FilterComments
public sealed record QueryNameQuery(ParameterDeclarations) : IQuery<IReadOnlyList<ResultDtoName>>;
#elif (ResultIsSummary)
// Computes one ResultDtoName on the AggregateName that its filters select: none selected is a null response, answered with 404.
// Add the other filters of the use case, each with its rule.
FilterComments
public sealed record QueryNameQuery(ParameterDeclarations) : IQuery<ResultDtoName?>;
#else
// Reads every AggregateName. Add the parameters of the use case, such as a filter, and their validator if any.
public sealed record QueryNameQuery : IQuery<IReadOnlyList<ResultDtoName>>;
#endif

#if (ResultIsSummary)
// Values only: never an entity, not even in a property. Replace Count with the values the use case computes, such as the count of
// each state.
public sealed record ResultDtoName(int Count);
#else
// Values only: never an entity, not even in a property. Add the values the use case reads.
public sealed record ResultDtoName(Guid Id);
#endif
#if (ResultTakesFilters)

// Checks the shape of the filters only, before the handler runs: business rules stay in the domain.
internal sealed class QueryNameValidator : AbstractValidator<QueryNameQuery>
{
    public QueryNameValidator()
    {
        ParameterRules
    }
}
#endif

// Reads a projection without tracking: a query never loads the aggregate, never saves, and runs outside any transaction.
#if (ResultIsSingle)
internal sealed class QueryNameHandler(AppDbContext db) : IRequestHandler<QueryNameQuery, ResultDtoName?>
{
    public Task<ResultDtoName?> Handle(QueryNameQuery query, CancellationToken cancellationToken)
    {
        var id = new AggregateNameId(query.Id);
        return db.Set<AggregateName>()
            .AsNoTracking()
            .Where(aggregate => aggregate.Id == id)
            .Select(aggregate => new ResultDtoName(aggregate.Id.Value))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
#elif (ResultIsSummary)
// PostgreSQL computes the values in one query: GroupBy on a constant puts every selected AggregateName in one group, and Select
// projects it, such as group.Count(aggregate => aggregate.State == State.Done) for count(*) FILTER (WHERE ...). No group, so no
// row, when the filters select none: SingleOrDefaultAsync returns null. Never load the rows to count them in memory.
internal sealed class QueryNameHandler(AppDbContext db) : IRequestHandler<QueryNameQuery, ResultDtoName?>
{
    public Task<ResultDtoName?> Handle(QueryNameQuery query, CancellationToken cancellationToken) =>
        db.Set<AggregateName>()
            .AsNoTracking()
            // TODO(hardslice): keep the AggregateName that the filters of the query select, such as .Where(aggregate => aggregate.FirstFilterName == query.FirstFilterName), then compute each value of ResultDtoName on the group in Select.
            .GroupBy(aggregate => 1)
            .Select(group => new ResultDtoName(group.Count()))
            .SingleOrDefaultAsync(cancellationToken);
}
#else
internal sealed class QueryNameHandler(AppDbContext db) : IRequestHandler<QueryNameQuery, IReadOnlyList<ResultDtoName>>
{
    public async Task<IReadOnlyList<ResultDtoName>> Handle(QueryNameQuery query, CancellationToken cancellationToken) =>
        await db.Set<AggregateName>()
            .AsNoTracking()
#if (ResultIsFiltered)
            // TODO(hardslice): keep the AggregateName that the filters of the query select, such as .Where(aggregate => aggregate.FirstFilterName == query.FirstFilterName), and join the other aggregates the use case reads, each with AsNoTracking().
#endif
            .OrderBy(aggregate => aggregate.Id)
            .Select(aggregate => new ResultDtoName(aggregate.Id.Value))
            .ToListAsync(cancellationToken);
}
#endif

internal static class QueryNameEndpoint
{
#if (HasRouteFilters && HasQueryStringFilters)
    // [AsParameters] binds route-filter-bindings of the route,
    // and each other parameter of the query from the query string, such as ?query-string-example.
#elif (HasRouteFilters)
    // [AsParameters] binds route-filter-bindings of the route,
    // and each other parameter of the query from the query string.
#elif (ResultTakesFilters)
    // [AsParameters] binds each parameter of the query from the query string, such as ?query-string-example.
#endif
#if (RequireIsGiven)
    // The policy AuthorizationPolicyName answers 401 or 403 before the delegate runs: those responses are not in its signature (HS015),
    // and an integration test asserts the 401.
#endif
    public static void Map(RouteGroupBuilder group) =>
#if (RequireIsGiven && ResultIsSingle)
        group.MapGet("/query-id-route", async Task<Results<Ok<ResultDtoName>, NotFound>> (Guid id, IMediator mediator, CancellationToken cancellationToken) =>
                await mediator.Send(new QueryNameQuery(id), cancellationToken) is { } dto ? TypedResults.Ok(dto) : TypedResults.NotFound())
            .RequireAuthorization("AuthorizationPolicyName");
#elif (RequireIsGiven && ResultIsSummary)
        group.MapGet("/query-route", async Task<Results<Ok<ResultDtoName>, NotFound>> ([AsParameters] QueryNameQuery query, IMediator mediator, CancellationToken cancellationToken) =>
                await mediator.Send(query, cancellationToken) is { } dto ? TypedResults.Ok(dto) : TypedResults.NotFound())
            .RequireAuthorization("AuthorizationPolicyName");
#elif (RequireIsGiven && ResultIsFiltered)
        group.MapGet("/query-route", async ([AsParameters] QueryNameQuery query, IMediator mediator, CancellationToken cancellationToken) =>
                TypedResults.Ok(await mediator.Send(query, cancellationToken)))
            .RequireAuthorization("AuthorizationPolicyName");
#elif (RequireIsGiven)
        group.MapGet("/query-route", async (IMediator mediator, CancellationToken cancellationToken) =>
                TypedResults.Ok(await mediator.Send(new QueryNameQuery(), cancellationToken)))
            .RequireAuthorization("AuthorizationPolicyName");
#elif (ResultIsSingle)
        group.MapGet("/query-id-route", async Task<Results<Ok<ResultDtoName>, NotFound>> (Guid id, IMediator mediator, CancellationToken cancellationToken) =>
            await mediator.Send(new QueryNameQuery(id), cancellationToken) is { } dto ? TypedResults.Ok(dto) : TypedResults.NotFound());
#elif (ResultIsSummary)
        group.MapGet("/query-route", async Task<Results<Ok<ResultDtoName>, NotFound>> ([AsParameters] QueryNameQuery query, IMediator mediator, CancellationToken cancellationToken) =>
            await mediator.Send(query, cancellationToken) is { } dto ? TypedResults.Ok(dto) : TypedResults.NotFound());
#elif (ResultIsFiltered)
        group.MapGet("/query-route", async ([AsParameters] QueryNameQuery query, IMediator mediator, CancellationToken cancellationToken) =>
            TypedResults.Ok(await mediator.Send(query, cancellationToken)));
#else
        group.MapGet("/query-route", async (IMediator mediator, CancellationToken cancellationToken) =>
            TypedResults.Ok(await mediator.Send(new QueryNameQuery(), cancellationToken)));
#endif
}
