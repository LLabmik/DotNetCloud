using DotNetCloud.Core.Auth.Authorization;
using DotNetCloud.Core.Data.Infrastructure;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Search;
using DotNetCloud.Modules.Search.Data;
using DotNetCloud.Modules.Search.Host.Services;
using DotNetCloud.Modules.Search.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;

using ISearchProvider = DotNetCloud.Core.Capabilities.ISearchProvider;
using Microsoft.AspNetCore.Diagnostics;

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

// Share DataProtection keys with Core.Server so auth cookies from the proxy are valid.
var dataProtectionDir = Environment.GetEnvironmentVariable("DOTNETCLOUD_DATA_DIR");
var dpKeysPath = !string.IsNullOrWhiteSpace(dataProtectionDir)
    ? Path.Combine(dataProtectionDir, "data-protection-keys")
    : Path.Combine(builder.Environment.ContentRootPath, "data-protection-keys");
Directory.CreateDirectory(dpKeysPath);
builder.Services.AddDataProtection()
    .SetApplicationName("DotNetCloud")
    .PersistKeysToFileSystem(new DirectoryInfo(dpKeysPath));

// Cookie auth — same cookie name as Core.Server. SecurePolicy=Always because
// the YARP proxy sets X-Forwarded-Proto, so UseForwardedHeaders() enables Secure.
builder.Services.AddAuthentication("Identity.Application")
    .AddCookie("Identity.Application", options =>
    {
        options.Cookie.Name = ".AspNetCore.Identity.Application";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.IsEssential = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(24);
        options.SlidingExpiration = true;

        // Skip Identity user-store lookup — the cookie was already validated
        // by Core.Server. Just accept the decrypted principal as-is.
        options.Events.OnValidatePrincipal = static context =>
        {
            if (context.Principal?.Identity?.IsAuthenticated == true)
                return Task.CompletedTask;
            context.RejectPrincipal();
            return Task.CompletedTask;
        };

        // Return 401 for API requests instead of redirecting to login.
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization(options => AuthorizationPolicies.Configure(options));
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

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

// Show full exception details for debugging; remove in production.
app.UseDeveloperExceptionPage();

// --- Middleware ---

// Map gRPC services
app.MapGrpcService<SearchGrpcService>();
app.MapGrpcService<SearchLifecycleService>();

// Map REST API controllers
// Trust X-Forwarded-Proto from the YARP proxy so __Host- cookies work over HTTP.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto,
    KnownProxies = { System.Net.IPAddress.Loopback, System.Net.IPAddress.IPv6Loopback },
});
app.UseAuthentication();
app.UseAuthorization();
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
