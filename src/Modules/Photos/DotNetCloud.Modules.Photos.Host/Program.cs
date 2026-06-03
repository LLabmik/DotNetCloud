using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Photos;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Photos.Data.Services;
using DotNetCloud.Modules.Photos.Host.Services;
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
builder.Services.AddSingleton<PhotosModule>();

// Register EF Core with config-driven database, falling back to in-memory
var connectionString = builder.Configuration["connectionString"]
    ?? builder.Configuration.GetConnectionString("DefaultConnection");
var dbProvider = builder.Configuration["databaseProvider"]
    ?? builder.Configuration["database:provider"];

if (!string.IsNullOrEmpty(connectionString) && !string.IsNullOrEmpty(dbProvider))
{
    builder.Services.AddDbContext<PhotosDbContext>(options =>
    {
        if (string.Equals(dbProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase))
            options.UseNpgsql(connectionString);
        else
            options.UseSqlServer(connectionString);
    });
}
else
{
    builder.Services.AddDbContext<PhotosDbContext>(options =>
        options.UseInMemoryDatabase("PhotosModule"));
}

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
app.MapGrpcService<PhotosLifecycleService>();

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
