using Demo.Features.Bookings.CancelBooking;
using Demo.Features.Bookings.ConfirmBooking;

namespace Demo.Features.Bookings;

public static class BookingsModule
{
    public static IServiceCollection AddBookingsModule(this IServiceCollection services) =>
        services
            .AddSingleton(TimeProvider.System)
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
