using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Contacts;
using DotNetCloud.Modules.Contacts.Data;
using DotNetCloud.Modules.Contacts.Host.Services;
using DotNetCloud.Modules.Calendar.Data;
using DotNetCloud.Modules.Notes.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- Services ---

// Register the Contacts module as a singleton for lifecycle management
builder.Services.AddSingleton<ContactsModule>();

// Register EF Core with an in-memory database for development
// In production, modules use the database configured by the core server
builder.Services.AddDbContext<ContactsDbContext>(options =>
    options.UseInMemoryDatabase("ContactsModule"));
builder.Services.AddDbContext<CalendarDbContext>(options =>
    options.UseInMemoryDatabase("CalendarModule"));
builder.Services.AddDbContext<NotesDbContext>(options =>
    options.UseInMemoryDatabase("NotesModule"));

// In-process event bus for standalone operation
builder.Services.AddSingleton<IEventBus, InProcessEventBus>();

// Register all contacts business-logic services (Contact, Group, Share, VCard)
builder.Services.AddContactsServices(builder.Configuration);

// gRPC
builder.Services.AddGrpc();

// REST API controllers
builder.Services.AddControllers();

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck<ContactsHealthCheck>("contacts_module");

var app = builder.Build();

// --- Middleware ---

// Map gRPC services
app.MapGrpcService<ContactsGrpcService>();
app.MapGrpcService<ContactsLifecycleService>();

// Map REST API controllers
app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health");

// Minimal info endpoint
app.MapGet("/", () => Results.Ok(new
{
    module = "dotnetcloud.contacts",
    version = "1.0.0",
    status = "running"
}));

app.Run();

/// <summary>Entry point marker for WebApplicationFactory in integration tests.</summary>
public partial class Program;
