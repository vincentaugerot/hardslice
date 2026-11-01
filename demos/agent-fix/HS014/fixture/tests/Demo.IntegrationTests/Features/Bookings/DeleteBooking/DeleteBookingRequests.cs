namespace Demo.IntegrationTests.Features.Bookings.DeleteBooking;

// The requests the tests of DeleteBooking will send.
internal static class DeleteBookingRequests
{
    public static Task<HttpResponseMessage> DeleteAsync(HttpClient client, Guid bookingId) =>
        client.DeleteAsync($"/bookings/{bookingId}", TestContext.Current.CancellationToken);
}
