#if (!NoSample)
using MyApp.Features.Bookings;
#endif
using MyApp.Shared.Behaviors;
using MyApp.Shared.Http;
using MyApp.Shared.Messaging;
using MyApp.Shared.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
// A date and time without its offset is answered with 400 instead of being read in the time zone of the server.
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new ExplicitOffsetJsonConverter()));
// The clock handlers inject instead of reading DateTime.UtcNow (HS010); the host does not register it.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddExceptionHandler<BadRequestExceptionHandler>();
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
