using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Notes;
using DotNetCloud.Modules.Notes.Data;
using DotNetCloud.Modules.Notes.Host.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Register the module as singleton
builder.Services.AddSingleton<NotesModule>();

// Register EF Core with in-memory database (dev only)
builder.Services.AddDbContext<NotesDbContext>(options =>
    options.UseInMemoryDatabase("NotesModule"));

// In-process event bus for standalone operation
builder.Services.AddSingleton<IEventBus, InProcessEventBus>();

// Register all business-logic services
builder.Services.AddNotesServices(builder.Configuration);

// gRPC
builder.Services.AddGrpc();
builder.Services.AddControllers();
builder.Services.AddHealthChecks()
    .AddCheck<NotesHealthCheck>("notes_module");

var app = builder.Build();

// Map gRPC services
app.MapGrpcService<NotesGrpcService>();
app.MapGrpcService<NotesLifecycleService>();

// Map REST API controllers
app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health");

// Minimal info endpoint
app.MapGet("/", () => Results.Ok(new
{
    module = "dotnetcloud.notes",
    version = "1.0.0",
    status = "running"
}));

app.Run();

/// <summary>Marker class for integration test host reference.</summary>
public partial class Program;
