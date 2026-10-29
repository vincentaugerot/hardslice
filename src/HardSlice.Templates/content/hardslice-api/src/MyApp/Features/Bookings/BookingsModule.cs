using FluentValidation;
using MyApp.Domain.Bookings;
using MyApp.Features.Bookings.CancelBooking;
using MyApp.Features.Bookings.ConfirmBooking;
using MyApp.Shared.Messaging;

namespace MyApp.Features.Bookings;

public static class BookingsModule
{
    // Handlers are registered by their contract only: endpoints reach them through IMediator, never directly.
    public static IServiceCollection AddBookingsModule(this IServiceCollection services) =>
        services
            .AddScoped<IRequestHandler<ConfirmBookingCommand, BookingId>, ConfirmBookingHandler>()
            .AddScoped<IValidator<ConfirmBookingCommand>, ConfirmBookingValidator>()
            .AddScoped<IRequestHandler<CancelBookingCommand, bool>, CancelBookingHandler>()
            .AddScoped<IValidator<CancelBookingCommand>, CancelBookingValidator>();

    public static IEndpointRouteBuilder MapBookingsModule(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/bookings");
        ConfirmBookingEndpoint.Map(group);
        CancelBookingEndpoint.Map(group);
        return endpoints;
    }
}
