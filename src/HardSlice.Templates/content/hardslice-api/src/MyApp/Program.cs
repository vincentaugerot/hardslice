#if (!NoSample)
using MyApp.Features.Bookings;
#endif
using MyApp.Shared.Behaviors;
using MyApp.Shared.Messaging;
using MyApp.Shared.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
// The clock handlers inject instead of reading DateTime.UtcNow (HS010); the host does not register it.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddMediator();
builder.Services.AddPipelineBehaviors();
#if (!NoSample)
builder.Services.AddBookingsModule();
#endif

var app = builder.Build();

app.UseExceptionHandler();
#if (!NoSample)
app.MapBookingsModule();
#endif

app.Run();
