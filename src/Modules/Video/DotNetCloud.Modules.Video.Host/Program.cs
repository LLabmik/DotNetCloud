using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Video;
using DotNetCloud.Modules.Video.Data;
using DotNetCloud.Modules.Video.Host.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Register the module as singleton
builder.Services.AddSingleton<VideoModule>();

// Register EF Core with in-memory database (dev only)
builder.Services.AddDbContext<VideoDbContext>(options =>
    options.UseInMemoryDatabase("VideoModule"));

// In-process event bus for standalone operation
builder.Services.AddSingleton<IEventBus, InProcessEventBus>();

// Register all business-logic services
builder.Services.AddVideoServices(builder.Configuration);

// gRPC
builder.Services.AddGrpc();
builder.Services.AddControllers();
builder.Services.AddHealthChecks()
    .AddCheck<VideoHealthCheck>("video_module");

var app = builder.Build();

// Map gRPC services
app.MapGrpcService<VideoGrpcServiceImpl>();

// Map REST API controllers
app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health");

// Minimal info endpoint
app.MapGet("/", () => Results.Ok(new
{
    module = "dotnetcloud.video",
    version = "1.0.0",
    status = "running"
}));

app.Run();

/// <summary>Marker class for integration test host reference.</summary>
public partial class Program;
