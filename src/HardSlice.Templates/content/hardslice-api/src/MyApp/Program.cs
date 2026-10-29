using MyApp.Features.Bookings;
using MyApp.Shared.Behaviors;
using MyApp.Shared.Messaging;
using MyApp.Shared.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddMediator();
builder.Services.AddPipelineBehaviors();
builder.Services.AddBookingsModule();

var app = builder.Build();

app.UseExceptionHandler();
app.MapBookingsModule();

app.Run();
