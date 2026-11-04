#if (ResultIsFiltered)
using FluentValidation;
#endif
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
public sealed record QueryNameQuery(Guid Id) : IQuery<ResultDtoName?>;
#elif (ResultIsFiltered)
// Reads the AggregateName that FilterName selects, from the query string. FilterName is nullable so that a request without it
// reaches the validator, which answers 400 naming it. Add the other filters of the use case, each with its rule.
public sealed record QueryNameQuery(FilterType? FilterName) : IQuery<IReadOnlyList<ResultDtoName>>;
#else
// Reads every AggregateName. Add the parameters of the use case, such as a filter, and their validator if any.
public sealed record QueryNameQuery : IQuery<IReadOnlyList<ResultDtoName>>;
#endif

// Values only: never an entity, not even in a property. Add the values the use case reads.
public sealed record ResultDtoName(Guid Id);
#if (ResultIsFiltered)

// Checks the shape of the filters only, before the handler runs: business rules stay in the domain.
internal sealed class QueryNameValidator : AbstractValidator<QueryNameQuery>
{
    public QueryNameValidator() =>
#if (FilterIsString)
        RuleFor(query => query.FilterName).NotEmpty();
#else
        RuleFor(query => query.FilterName).NotNull();
#endif
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
#else
internal sealed class QueryNameHandler(AppDbContext db) : IRequestHandler<QueryNameQuery, IReadOnlyList<ResultDtoName>>
{
    public async Task<IReadOnlyList<ResultDtoName>> Handle(QueryNameQuery query, CancellationToken cancellationToken) =>
        await db.Set<AggregateName>()
            .AsNoTracking()
#if (ResultIsFiltered)
            // TODO(hardslice): keep the AggregateName that query.FilterName selects, such as .Where(aggregate => aggregate.FilterName == query.FilterName), and join the other aggregates the use case reads, each with AsNoTracking().
#endif
            .OrderBy(aggregate => aggregate.Id)
            .Select(aggregate => new ResultDtoName(aggregate.Id.Value))
            .ToListAsync(cancellationToken);
}
#endif

internal static class QueryNameEndpoint
{
#if (ResultIsFiltered)
    // [AsParameters] binds each parameter of the query from the query string, such as ?filterName=filter-query-value.
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
#elif (ResultIsFiltered)
        group.MapGet("/query-route", async ([AsParameters] QueryNameQuery query, IMediator mediator, CancellationToken cancellationToken) =>
            TypedResults.Ok(await mediator.Send(query, cancellationToken)));
#else
        group.MapGet("/query-route", async (IMediator mediator, CancellationToken cancellationToken) =>
            TypedResults.Ok(await mediator.Send(new QueryNameQuery(), cancellationToken)));
#endif
}
