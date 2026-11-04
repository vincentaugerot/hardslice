using FluentValidation;
using MyApp.Features.Bookings.CancelBooking;
using MyApp.Features.Bookings.ConfirmBooking;
using MyApp.Features.Bookings.GetBooking;
using MyApp.Shared.Messaging;

namespace MyApp.Features.Bookings;

public static class BookingsModule
{
    // The route of the group that maps the endpoints of the module: the integration tests of its slices call theirs under it.
    public const string Route = "/bookings";

    // Handlers are registered by their contract only: endpoints reach them through IMediator, never directly.
    public static IServiceCollection AddBookingsModule(this IServiceCollection services) =>
        services
            .AddScoped<IRequestHandler<ConfirmBookingCommand, Guid>, ConfirmBookingHandler>()
            .AddScoped<IValidator<ConfirmBookingCommand>, ConfirmBookingValidator>()
            .AddScoped<IRequestHandler<CancelBookingCommand, bool>, CancelBookingHandler>()
            .AddScoped<IValidator<CancelBookingCommand>, CancelBookingValidator>()
            .AddScoped<IRequestHandler<GetBookingQuery, BookingDto?>, GetBookingHandler>();

    public static IEndpointRouteBuilder MapBookingsModule(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(Route);
        ConfirmBookingEndpoint.Map(group);
        CancelBookingEndpoint.Map(group);
        GetBookingEndpoint.Map(group);
        return endpoints;
    }
}
