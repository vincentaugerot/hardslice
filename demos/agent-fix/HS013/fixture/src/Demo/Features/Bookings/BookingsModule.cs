using FluentValidation;
using Demo.Domain.Bookings;
using Demo.Features.Bookings.CancelBooking;
using Demo.Features.Bookings.ConfirmBooking;
using Demo.Features.Bookings.DeleteBooking;
using Demo.Features.Bookings.GetBooking;
using Demo.Shared.Messaging;

namespace Demo.Features.Bookings;

public static class BookingsModule
{
    // Handlers are registered by their contract only: endpoints reach them through IMediator, never directly.
    public static IServiceCollection AddBookingsModule(this IServiceCollection services) =>
        services
            .AddScoped<IRequestHandler<ConfirmBookingCommand, BookingId>, ConfirmBookingHandler>()
            .AddScoped<IValidator<ConfirmBookingCommand>, ConfirmBookingValidator>()
            .AddScoped<IRequestHandler<CancelBookingCommand, bool>, CancelBookingHandler>()
            .AddScoped<IValidator<CancelBookingCommand>, CancelBookingValidator>()
            .AddScoped<IRequestHandler<GetBookingQuery, BookingDto?>, GetBookingHandler>()
            .AddScoped<IRequestHandler<DeleteBookingCommand, bool>, DeleteBookingHandler>();

    public static IEndpointRouteBuilder MapBookingsModule(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/bookings");
        ConfirmBookingEndpoint.Map(group);
        CancelBookingEndpoint.Map(group);
        GetBookingEndpoint.Map(group);
        DeleteBookingEndpoint.Map(group);
        return endpoints;
    }
}
