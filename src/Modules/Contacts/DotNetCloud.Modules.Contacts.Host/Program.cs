using DotNetCloud.Core.Events;
using DotNetCloud.Core.Security;
using DotNetCloud.Modules.Contacts;
using DotNetCloud.Modules.Contacts.Data;
using DotNetCloud.Modules.Contacts.Host.Services;
using DotNetCloud.Modules.Calendar.Data;
using DotNetCloud.Modules.Notes.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Load shared config from DOTNETCLOUD_CONFIG_DIR (database connection string from setup)
var configDir = Environment.GetEnvironmentVariable("DOTNETCLOUD_CONFIG_DIR");
if (!string.IsNullOrEmpty(configDir))
{
    var configJsonPath = Path.Combine(configDir, "config.json");
    if (File.Exists(configJsonPath))
    {
        builder.Configuration.AddJsonFile(configJsonPath, optional: true, reloadOnChange: false);
    }
}

// Read gRPC endpoint assigned by the ProcessSupervisor
var grpcEndpoint = Environment.GetEnvironmentVariable("DOTNETCLOUD_GRPC_ENDPOINT");
if (!string.IsNullOrEmpty(grpcEndpoint))
{
    var uri = new Uri(grpcEndpoint.Replace("unix://", "http://").Replace("net.pipe://", "http://"));
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Listen(System.Net.IPAddress.Loopback, uri.Port, listenOptions =>
        {
            listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
        });
    });
}

// --- Services ---

// Register the Contacts module as a singleton for lifecycle management
builder.Services.AddSingleton<ContactsModule>();

// File validation service for upload security
builder.Services.AddSingleton<IFileValidationService, FileValidationService>();

// Use shared database from core server config, fall back to in-memory
var connStr = builder.Configuration["connectionString"]
    ?? builder.Configuration.GetConnectionString("DefaultConnection");
var dbProvider = builder.Configuration["databaseProvider"]
    ?? builder.Configuration["database:provider"];

if (!string.IsNullOrEmpty(connStr) && !string.IsNullOrEmpty(dbProvider))
{
    void ConfigureDb(DbContextOptionsBuilder o)
    {
        if (string.Equals(dbProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase))
            o.UseNpgsql(connStr);
        else
            o.UseSqlServer(connStr);
    }
    builder.Services.AddDbContext<ContactsDbContext>(ConfigureDb);
    builder.Services.AddDbContext<CalendarDbContext>(ConfigureDb);
    builder.Services.AddDbContext<NotesDbContext>(ConfigureDb);
}
else
{
    builder.Services.AddDbContext<ContactsDbContext>(o => o.UseInMemoryDatabase("ContactsModule"));
    builder.Services.AddDbContext<CalendarDbContext>(o => o.UseInMemoryDatabase("CalendarModule"));
    builder.Services.AddDbContext<NotesDbContext>(o => o.UseInMemoryDatabase("NotesModule"));
}

// In-process event bus for standalone operation
builder.Services.AddSingleton<IEventBus, InProcessEventBus>();

// Register all contacts business-logic services (Contact, Group, Share, VCard)
builder.Services.AddContactsServices(builder.Configuration);

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
