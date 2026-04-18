using DotNetCloud.Core.Events;
using DotNetCloud.Modules.AI;
using DotNetCloud.Modules.AI.Data;
using DotNetCloud.Modules.AI.Host.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Register the module as singleton
builder.Services.AddSingleton<AiModule>();

// Register EF Core with in-memory database (dev) — swap for Npgsql/SqlServer in production
builder.Services.AddDbContext<AiDbContext>(options =>
    options.UseInMemoryDatabase("AiModule"));

// In-process event bus for standalone operation
builder.Services.AddSingleton<IEventBus, InProcessEventBus>();

// Register all business-logic services (includes Ollama HTTP client)
builder.Services.AddAiServices(builder.Configuration);

// gRPC
builder.Services.AddGrpc();
builder.Services.AddControllers();
builder.Services.AddHealthChecks()
    .AddCheck<AiHealthCheck>("ai_module");

var app = builder.Build();

// Map REST API controllers
app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health");

// Minimal info endpoint
app.MapGet("/", () => Results.Ok(new
{
    module = "dotnetcloud.ai",
    version = "1.0.0",
    status = "running"
}));

app.Run();
