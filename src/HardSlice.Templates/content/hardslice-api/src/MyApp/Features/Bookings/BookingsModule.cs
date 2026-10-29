using MyApp.Features.Bookings.CancelBooking;
using MyApp.Features.Bookings.ConfirmBooking;

namespace MyApp.Features.Bookings;

public static class BookingsModule
{
    public static IServiceCollection AddBookingsModule(this IServiceCollection services) =>
        services
            .AddSingleton<IBookingStore, InMemoryBookingStore>()
            .AddScoped<ConfirmBookingHandler>()
            .AddScoped<CancelBookingHandler>();

    public static IEndpointRouteBuilder MapBookingsModule(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/bookings");
        ConfirmBookingEndpoint.Map(group);
        CancelBookingEndpoint.Map(group);
        return endpoints;
    }
}
