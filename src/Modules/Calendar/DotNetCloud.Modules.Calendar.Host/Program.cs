using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Calendar;
using DotNetCloud.Modules.Calendar.Data;
using DotNetCloud.Modules.Calendar.Host.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- Services ---

// Register the Calendar module as a singleton for lifecycle management
builder.Services.AddSingleton<CalendarModule>();

// Register EF Core with an in-memory database for development
// In production, modules use the database configured by the core server
builder.Services.AddDbContext<CalendarDbContext>(options =>
    options.UseInMemoryDatabase("CalendarModule"));

// In-process event bus for standalone operation
builder.Services.AddSingleton<IEventBus, InProcessEventBus>();

// Register all calendar business-logic services (Calendar, Event, Share, ICal)
builder.Services.AddCalendarServices(builder.Configuration);

// gRPC
builder.Services.AddGrpc();

// REST API controllers
builder.Services.AddControllers();

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck<CalendarHealthCheck>("calendar_module");

var app = builder.Build();

// --- Middleware ---

// Map gRPC services
app.MapGrpcService<CalendarGrpcService>();
app.MapGrpcService<CalendarLifecycleService>();

// Map REST API controllers
app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health");

// Minimal info endpoint
app.MapGet("/", () => Results.Ok(new
{
    module = "dotnetcloud.calendar",
    version = "1.0.0",
    status = "running"
}));

app.Run();

/// <summary>Entry point marker for WebApplicationFactory in integration tests.</summary>
public partial class Program;
