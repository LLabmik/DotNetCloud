using DotNetCloud.Core.Events;
using DotNetCloud.Modules.AI;
using DotNetCloud.Modules.AI.Data;
using DotNetCloud.Modules.AI.Host.Services;
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
builder.Services.AddSingleton<AiModule>();

// Register EF Core with config-driven database, falling back to in-memory
var connectionString = builder.Configuration["connectionString"]
    ?? builder.Configuration.GetConnectionString("DefaultConnection");
var dbProvider = builder.Configuration["databaseProvider"]
    ?? builder.Configuration["database:provider"];

if (!string.IsNullOrEmpty(connectionString) && !string.IsNullOrEmpty(dbProvider))
{
    builder.Services.AddDbContext<AiDbContext>(options =>
    {
        if (string.Equals(dbProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase))
            options.UseNpgsql(connectionString);
        else
            options.UseSqlServer(connectionString);
    });
}
else
{
    builder.Services.AddDbContext<AiDbContext>(options =>
        options.UseInMemoryDatabase("AiModule"));
}

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

// Map gRPC services
app.MapGrpcService<AiGrpcService>();
app.MapGrpcService<AiLifecycleService>();

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
