using MyApp.Features.Bookings;
using MyApp.Shared.Behaviors;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddBookingsModule();

var app = builder.Build();

app.UseExceptionHandler();
app.MapBookingsModule();

app.Run();
