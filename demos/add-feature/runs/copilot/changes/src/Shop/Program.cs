using Shop.Features.Catalog;
using Shop.Shared.Behaviors;
using Shop.Shared.Messaging;
using Shop.Shared.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
// The clock handlers inject instead of reading DateTime.UtcNow (HS010); the host does not register it.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddMediator();
builder.Services.AddPipelineBehaviors();

builder.Services.AddCatalogModule();

var app = builder.Build();

app.UseExceptionHandler();

app.MapCatalogModule();

app.Run();
