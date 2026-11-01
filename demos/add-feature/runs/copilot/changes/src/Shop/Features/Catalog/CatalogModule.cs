using FluentValidation;
using Shop.Domain.Catalog.Events;
using Shop.Features.Catalog.GetProduct;
using Shop.Features.Catalog.OpenStockOnProductRegistered;
using Shop.Features.Catalog.RegisterProduct;
using Shop.Shared.Messaging;

namespace Shop.Features.Catalog;

public static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services) =>
        services
            .AddScoped<IRequestHandler<RegisterProductCommand, Guid>, RegisterProductHandler>()
            .AddScoped<IValidator<RegisterProductCommand>, RegisterProductValidator>()
            .AddScoped<IRequestHandler<GetProductQuery, GetProductDto?>, GetProductHandler>()
            .AddScoped<IDomainEventHandler<ProductRegistered>, OpenStockOnProductRegisteredHandler>();

    public static IEndpointRouteBuilder MapCatalogModule(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/catalog");
        RegisterProductEndpoint.Map(group);
        GetProductEndpoint.Map(group);
        return endpoints;
    }
}
