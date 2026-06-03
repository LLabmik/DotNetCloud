using DotNetCloud.Core.Events;
using DotNetCloud.Core.Security;
using DotNetCloud.Modules.Tracks;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Host.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Load shared config from DOTNETCLOUD_CONFIG_DIR
var configDir = Environment.GetEnvironmentVariable("DOTNETCLOUD_CONFIG_DIR");
if (!string.IsNullOrEmpty(configDir))
{
    var p = Path.Combine(configDir, "config.json");
    if (File.Exists(p))
        builder.Configuration.AddJsonFile(p, optional: true, reloadOnChange: false);
}

// Bind gRPC endpoint from DOTNETCLOUD_GRPC_ENDPOINT (set by ProcessSupervisor)
var grpcEndpoint = Environment.GetEnvironmentVariable("DOTNETCLOUD_GRPC_ENDPOINT");
if (!string.IsNullOrEmpty(grpcEndpoint))
{
    var uri = new Uri(grpcEndpoint.Replace("unix://", "http://").Replace("net.pipe://", "http://"));
    builder.WebHost.ConfigureKestrel(o =>
        o.Listen(System.Net.IPAddress.Loopback, uri.Port, l => l.Protocols = HttpProtocols.Http2));
}

// Register the module as singleton
builder.Services.AddSingleton<TracksModule>();

// File validation service for upload security
builder.Services.AddSingleton<IFileValidationService, FileValidationService>();

// Register EF Core with config-driven database, falling back to in-memory
var connectionString = builder.Configuration["connectionString"]
    ?? builder.Configuration.GetConnectionString("DefaultConnection");
var dbProvider = builder.Configuration["databaseProvider"]
    ?? builder.Configuration["database:provider"];

if (!string.IsNullOrEmpty(connectionString) && !string.IsNullOrEmpty(dbProvider))
{
    builder.Services.AddDbContext<TracksDbContext>(options =>
    {
        if (string.Equals(dbProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase))
            options.UseNpgsql(connectionString);
        else
            options.UseSqlServer(connectionString);
    });
}
else
{
    builder.Services.AddDbContext<TracksDbContext>(options =>
        options.UseInMemoryDatabase("TracksModule"));
}

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

// Seed built-in product templates on startup (idempotent)
using (var scope = app.Services.CreateScope())
{
    var templateSeeder = scope.ServiceProvider.GetRequiredService<TemplateSeedService>();
    await templateSeeder.EnsureSeededAsync(CancellationToken.None);
}

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
