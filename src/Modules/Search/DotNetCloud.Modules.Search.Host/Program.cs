using DotNetCloud.Core.Data.Infrastructure;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Search;
using DotNetCloud.Modules.Search.Data;
using DotNetCloud.Modules.Search.Host.Services;
using DotNetCloud.Modules.Search.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using ISearchProvider = DotNetCloud.Core.Capabilities.ISearchProvider;

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

// --- Services ---

// Register the Search module as a singleton for lifecycle management
builder.Services.AddSingleton<SearchModule>();

// Register EF Core with config-driven database, falling back to in-memory
var connectionString = builder.Configuration["connectionString"]
    ?? builder.Configuration.GetConnectionString("DefaultConnection");
var dbProviderFromConfig = builder.Configuration["databaseProvider"]
    ?? builder.Configuration["database:provider"];

if (!string.IsNullOrEmpty(connectionString) && !string.IsNullOrEmpty(dbProviderFromConfig))
{
    builder.Services.AddDbContext<SearchDbContext>(options =>
    {
        if (string.Equals(dbProviderFromConfig, "PostgreSql", StringComparison.OrdinalIgnoreCase))
            options.UseNpgsql(connectionString);
        else
            options.UseSqlServer(connectionString);
    });
}
else
{
    builder.Services.AddDbContext<SearchDbContext>(options =>
        options.UseInMemoryDatabase("SearchModule"));
}

// In-process event bus for standalone operation
builder.Services.AddSingleton<IEventBus, InProcessEventBus>();

// Search provider — auto-selected based on database provider configuration
var dbProvider = ResolveDatabaseProvider(builder.Configuration);
switch (dbProvider)
{
    case DatabaseProvider.SqlServer:
        builder.Services.AddScoped<ISearchProvider, SqlServerSearchProvider>();
        break;
    case DatabaseProvider.PostgreSQL:
    default:
        builder.Services.AddScoped<ISearchProvider, PostgreSqlSearchProvider>();
        break;
}

// Register all search services (query, indexing, extractors, reindex background service)
builder.Services.AddSearchServices(builder.Configuration);

// gRPC
builder.Services.AddGrpc();

// REST API controllers
builder.Services.AddControllers();

// Health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// --- Middleware ---

// Map gRPC services
app.MapGrpcService<SearchGrpcService>();
app.MapGrpcService<SearchLifecycleService>();

// Map REST API controllers
app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health");

// Minimal info endpoint
app.MapGet("/", () => Results.Ok(new
{
    module = "dotnetcloud.search",
    version = "1.0.0",
    status = "running"
}));

// Resolves the database provider from configuration.
// Falls back to PostgreSQL if configuration is missing or unrecognized.
static DatabaseProvider ResolveDatabaseProvider(IConfiguration configuration)
{
    var configuredProvider = configuration["Database:Provider"] ?? configuration["databaseProvider"];
    if (string.IsNullOrWhiteSpace(configuredProvider))
        return DatabaseProvider.PostgreSQL;

    var lower = configuredProvider.ToLowerInvariant();
    if (lower.Contains("sqlserver") || lower.Contains("sql server"))
        return DatabaseProvider.SqlServer;

    return DatabaseProvider.PostgreSQL;
}

app.Run();

/// <summary>Entry point marker for WebApplicationFactory in integration tests.</summary>
public partial class Program;
