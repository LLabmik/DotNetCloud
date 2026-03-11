using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat;
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Chat.Host.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- Services ---

// Register the Chat module as a singleton for lifecycle management
builder.Services.AddSingleton<ChatModule>();

// Register EF Core with an in-memory database for development
// In production, modules use the database configured by the core server
builder.Services.AddDbContext<ChatDbContext>(options =>
    options.UseInMemoryDatabase("ChatModule"));

// In-process event bus for standalone operation
builder.Services.AddSingleton<IEventBus, InProcessEventBus>();

// Register all chat business-logic services (Channel, Message, Reaction, Pin, Typing)
builder.Services.AddChatServices(builder.Configuration);

// gRPC
builder.Services.AddGrpc();

// REST API controllers
builder.Services.AddControllers();

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck<ChatHealthCheck>("chat_module");

var app = builder.Build();

// --- Middleware ---

// Map gRPC services
app.MapGrpcService<ChatGrpcService>();
app.MapGrpcService<ChatLifecycleService>();

// Map REST API controllers
app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health");

// Minimal info endpoint
app.MapGet("/", () => Results.Ok(new
{
    module = "dotnetcloud.chat",
    version = "1.0.0",
    status = "running"
}));

app.Run();

/// <summary>Entry point marker for WebApplicationFactory in integration tests.</summary>
public partial class Program;
