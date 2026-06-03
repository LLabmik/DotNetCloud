using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Video;
using DotNetCloud.Modules.Video.Data;
using DotNetCloud.Modules.Video.Host.Services;
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
builder.Services.AddSingleton<VideoModule>();

// Resolve database provider and connection string from configuration
var provider = ResolveDatabaseProvider(builder.Configuration);
var connectionString = builder.Configuration["connectionString"]
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException(
        "Connection string not found. Set 'ConnectionStrings:DefaultConnection' in appsettings.json " +
        "or 'connectionString' in config.json.");
}

// Register EF Core with the configured database provider
builder.Services.AddDbContext<VideoDbContext>(options =>
{
    const string migrationsAssembly = "DotNetCloud.Modules.Video.Data.SqlServer";

    switch (provider)
    {
        case DatabaseProvider.PostgreSQL:
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                npgsqlOptions.CommandTimeout(30);
            });
            break;

        case DatabaseProvider.SqlServer:
            options.UseSqlServer(connectionString, sqlServerOptions =>
            {
                sqlServerOptions.EnableRetryOnFailure(maxRetryCount: 3);
                sqlServerOptions.CommandTimeout(30);
                sqlServerOptions.MigrationsAssembly(migrationsAssembly);
            });
            break;

        default:
            throw new InvalidOperationException($"Unsupported database provider: {provider}");
    }

    // Suppress pending model changes warning for PostgreSQL provider
    options.ConfigureWarnings(warnings =>
    {
        warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning);
    });
});

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
app.MapGrpcService<VideoLifecycleService>();

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

// Resolves the configured database provider from configuration.
// Reads from 'Database:Provider' (preferred) or legacy 'databaseProvider'.
static DatabaseProvider ResolveDatabaseProvider(IConfiguration configuration)
{
    var configuredProvider = configuration["Database:Provider"] ?? configuration["databaseProvider"];

    if (string.IsNullOrWhiteSpace(configuredProvider))
    {
        throw new InvalidOperationException(
            "Database provider not configured. Set 'Database:Provider' in appsettings.json " +
            "or config.json. Supported values: PostgreSQL, SqlServer.");
    }

    if (!DatabaseProviderConfiguration.TryParseConfiguredProvider(configuredProvider, out var provider))
    {
        throw new InvalidOperationException(
            $"Invalid database provider '{configuredProvider}'. Supported values: PostgreSQL, SqlServer.");
    }

    return provider;
}

/// <summary>Marker class for integration test host reference.</summary>
public partial class Program;
