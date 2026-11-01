using FluentValidation;
using Shop.Domain.Catalog.Events;
using Shop.Features.Catalog.GetProduct;
using Shop.Features.Catalog.OpenStockOnProductRegistered;
using Shop.Features.Catalog.RegisterProduct;
using Shop.Shared.Messaging;

namespace Shop.Features.Catalog;

// The public contract of the module: Program.cs registers and maps it, other modules use only the types of this namespace (HS012).
public static class CatalogModule
{
    // Handlers are registered by their contract only: endpoints reach them through IMediator, never directly.
    public static IServiceCollection AddCatalogModule(this IServiceCollection services) =>
        services
            .AddScoped<IRequestHandler<RegisterProductCommand, Guid>, RegisterProductHandler>()
            .AddScoped<IValidator<RegisterProductCommand>, RegisterProductValidator>()
            .AddScoped<IRequestHandler<GetProductQuery, GetProductDto?>, GetProductHandler>()
            .AddScoped<IDomainEventHandler<ProductRegistered>, OpenStockOnProductRegisteredHandler>();

    // Each slice maps its endpoint on the group of the module: <Slice>Endpoint.Map(group).
    public static IEndpointRouteBuilder MapCatalogModule(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/catalog");
        RegisterProductEndpoint.Map(group);
        GetProductEndpoint.Map(group);
        return endpoints;
    }
}
