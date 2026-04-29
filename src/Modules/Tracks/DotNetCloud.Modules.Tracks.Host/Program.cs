using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Tracks;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Host.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Register the module as singleton
builder.Services.AddSingleton<TracksModule>();

// Register EF Core with in-memory database (dev only)
builder.Services.AddDbContext<TracksDbContext>(options =>
    options.UseInMemoryDatabase("TracksModule"));

// In-process event bus for standalone operation
builder.Services.AddSingleton<IEventBus, InProcessEventBus>();

// Register all business-logic services
builder.Services.AddTracksServices(builder.Configuration);

// gRPC
builder.Services.AddGrpc();
builder.Services.AddControllers();
builder.Services.AddHealthChecks()
    .AddCheck<TracksHealthCheck>("tracks_module");

var app = builder.Build();

// Map gRPC services
app.MapGrpcService<TracksGrpcService>();
app.MapGrpcService<TracksLifecycleService>();

// Map REST API controllers
app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health");

// TODO: Seed built-in product templates on startup (ProductTemplateService.SeedBuiltInTemplatesAsync)

// Minimal info endpoint
app.MapGet("/", () => Results.Ok(new
{
    module = "dotnetcloud.tracks",
    version = "1.0.0",
    status = "running"
}));

app.Run();

/// <summary>Marker class for integration test host reference.</summary>
public partial class Program;
