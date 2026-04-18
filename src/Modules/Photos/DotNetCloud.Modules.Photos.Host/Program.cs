using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Photos;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Photos.Data.Services;
using DotNetCloud.Modules.Photos.Host.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Register the module as singleton
builder.Services.AddSingleton<PhotosModule>();

// Register EF Core with in-memory database (dev only)
builder.Services.AddDbContext<PhotosDbContext>(options =>
    options.UseInMemoryDatabase("PhotosModule"));

// In-process event bus for standalone operation
builder.Services.AddSingleton<IEventBus, InProcessEventBus>();

// Register all business-logic services
builder.Services.AddPhotosServices(builder.Configuration);

// gRPC
builder.Services.AddGrpc();
builder.Services.AddControllers();
builder.Services.AddHealthChecks()
    .AddCheck<PhotosHealthCheck>("photos_module");

var app = builder.Build();

// Map gRPC services
app.MapGrpcService<PhotosGrpcServiceImpl>();

// Map REST API controllers
app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health");

// Minimal info endpoint
app.MapGet("/", () => Results.Ok(new
{
    module = "dotnetcloud.photos",
    version = "1.0.0",
    status = "running"
}));

app.Run();

/// <summary>Marker class for integration test host reference.</summary>
public partial class Program;
