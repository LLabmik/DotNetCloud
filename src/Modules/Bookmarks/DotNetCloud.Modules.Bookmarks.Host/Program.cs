using DotNetCloud.Core.Data.Extensions;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Grpc;
using DotNetCloud.Core.Security;
using DotNetCloud.Modules.Bookmarks;
using DotNetCloud.Modules.Bookmarks.Data;
using DotNetCloud.Modules.Bookmarks.Host.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
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

builder.Services.AddAuthorization();

// Register the gRPC-backed audit logger (SOC 2 CC4) — routes to Core.Server.
builder.Services.AddAuditLogger();

// Real-time search indexing bridge — forwards SearchIndexRequestEvent to Core.Server.
builder.Services.AddSearchIndexBridge();

// Register the module as singleton
builder.Services.AddSingleton<BookmarksModule>();

// File validation service for upload security
builder.Services.AddSingleton<IFileValidationService, FileValidationService>();

// Register EF Core with SQL Server or PostgreSQL based on configuration
var connectionString = builder.Configuration["connectionString"]
    ?? builder.Configuration.GetConnectionString("DefaultConnection");
var dbProvider = builder.Configuration["databaseProvider"]
    ?? builder.Configuration["database:provider"];

if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(dbProvider))
{
    throw new InvalidOperationException(
        "The Bookmarks module requires a database connection string and provider. " +
        "These are provided via config.json (DOTNETCLOUD_CONFIG_DIR) when launched by the core server.");
}

var provider = ResolveDatabaseProvider(dbProvider);

builder.Services.AddDbContext<BookmarksDbContext>(options =>
    DbResiliencePolicy.Configure(
        options,
        provider,
        connectionString,
        provider == DatabaseProvider.SqlServer ? "DotNetCloud.Modules.Bookmarks.Data.SqlServer" : null));

// In-process event bus for standalone operation
builder.Services.AddSingleton<IEventBus, InProcessEventBus>();

// Register all business-logic services
builder.Services.AddBookmarksServices(builder.Configuration);

// gRPC
builder.Services.AddGrpc();
builder.Services.AddControllers();
builder.Services.AddHealthChecks()
    .AddCheck<BookmarksHealthCheck>("bookmarks_module");

var app = builder.Build();

// Initialize the database schema (creates tables if they don't exist).
// Retries with exponential backoff so a database that is briefly unavailable at
// startup does not crash the module — it continues running in degraded mode and
// resumes schema creation once the database recovers.
if (!string.IsNullOrEmpty(connectionString) && !string.IsNullOrEmpty(dbProvider))
{
    const int maxAttempts = 5;
    var delay = TimeSpan.FromSeconds(2);
    var initLogger = app.Services.GetRequiredService<ILogger<Program>>();

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BookmarksDbContext>();
            await BookmarksDbInitializer.InitializeAsync(db, initLogger, CancellationToken.None);
            break;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            initLogger.LogWarning(ex,
                "Bookmarks database initialization attempt {Attempt}/{MaxAttempts} failed. Retrying in {Delay}s...",
                attempt, maxAttempts, delay.TotalSeconds);
            await Task.Delay(delay);
            delay *= 2;
        }
        catch (Exception ex)
        {
            initLogger.LogError(ex,
                "Bookmarks database initialization failed after {MaxAttempts} attempts. " +
                "Continuing in degraded mode; retries will resume when the database recovers.",
                maxAttempts);
        }
    }
}

// Show full exception details for debugging; remove in production.
app.UseDeveloperExceptionPage();

// Map gRPC services
app.MapGrpcService<BookmarksGrpcService>();
app.MapGrpcService<BookmarksLifecycleService>();

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
    module = "dotnetcloud.bookmarks",
    version = "1.0.0",
    status = "running"
}));

app.Run();

// Resolves the configured database provider string into the canonical enum.
static DatabaseProvider ResolveDatabaseProvider(string? configured) =>
    DatabaseProviderConfiguration.TryParseConfiguredProvider(configured ?? string.Empty, out var provider)
        ? provider
        : throw new InvalidOperationException($"Unsupported database provider '{configured}'.");

/// <summary>Marker class for integration test host reference.</summary>
public partial class Program;
