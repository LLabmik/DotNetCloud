using DotNetCloud.Core.Auth.Authorization;
using DotNetCloud.Core.Auth.Security;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Host.Services;
using DotNetCloud.Modules.Files.Services;
using DotNetCloud.Modules.Search.Client;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
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

// Load shared OpenIddict signing keys for JWT Bearer token validation.
// These keys are the same ones used by Core.Server to sign access tokens,
// stored in the shared oidc-keys directory.
var dataRoot = Environment.GetEnvironmentVariable("DOTNETCLOUD_DATA_DIR");
var oidcKeysDir = Path.Combine(
    !string.IsNullOrWhiteSpace(dataRoot) ? dataRoot : AppContext.BaseDirectory,
    "oidc-keys");
using var keyLoggerFactory = LoggerFactory.Create(b => b.AddConsole());
var keyLogger = keyLoggerFactory.CreateLogger("DotNetCloud.OidcKeys");
var signingKeys = OidcKeyManager.LoadAllKeys(oidcKeysDir, OidcKeyManager.SigningKeyPrefix, keyLogger);

if (signingKeys.Count == 0)
{
    keyLogger.LogWarning("No OpenIddict signing keys found in {OidcKeysDir}. " +
        "JWT Bearer authentication will not be available for this module.", oidcKeysDir);
}

// Register OpenIddict validation for JWT Bearer token support.
// Uses the shared RSA signing keys from oidc-keys/ to validate tokens
// issued by Core.Server's OpenIddict server.
builder.Services.AddOpenIddict()
    .AddValidation(options =>
    {
        options.SetIssuer("https://cloud.dotnetcloud.net/");

        // Register the shared signing keys for local JWT validation.
        // This allows the module to validate tokens without calling
        // Core.Server's introspection endpoint on every request.
        foreach (var key in signingKeys)
        {
            options.AddSigningKey(key);
        }

        options.UseSystemNetHttp();
        options.UseAspNetCore();
    });

// Authentication: supports both cookie (browser/Blazor) and Bearer JWT (desktop/mobile).
// A policy scheme automatically routes to the correct handler based on the request.
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = "DotNetCloud.Module";
        options.DefaultAuthenticateScheme = "DotNetCloud.Module";
        options.DefaultChallengeScheme = "DotNetCloud.Module";
    })
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
    })
    .AddPolicyScheme("DotNetCloud.Module", "DotNetCloud.Module", options =>
    {
        // Route to OpenIddict validation for requests with Authorization: Bearer header
        // (desktop/mobile clients). Route to Cookie handler for browser/Blazor requests.
        options.ForwardDefaultSelector = context =>
        {
            if (context.Request.Headers.TryGetValue("Authorization", out var auth)
                && auth.Count > 0
                && auth[0]?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
            {
                return "OpenIddict.Validation.AspNetCore";
            }
            return "Identity.Application";
        };
    });

builder.Services.AddAuthorization(options => AuthorizationPolicies.Configure(options));
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

// --- Services ---

// Register the Files module as a singleton for lifecycle management
builder.Services.AddSingleton<FilesModule>();

// Register EF Core with config-driven database, falling back to in-memory
var connectionString = builder.Configuration["connectionString"]
    ?? builder.Configuration.GetConnectionString("DefaultConnection");
var dbProvider = builder.Configuration["databaseProvider"]
    ?? builder.Configuration["database:provider"];

if (!string.IsNullOrEmpty(connectionString) && !string.IsNullOrEmpty(dbProvider))
{
    builder.Services.AddDbContext<FilesDbContext>(options =>
    {
        if (string.Equals(dbProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase))
            options.UseNpgsql(connectionString);
        else
            options.UseSqlServer(connectionString);
    });

    // Register a read-only CoreDbContext for querying identity tables (dbo.Groups)
    // directly from the Files module, avoiding gRPC round-trips for group validation.
    builder.Services.AddDbContext<CoreDbContext>(options =>
    {
        if (string.Equals(dbProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase))
            options.UseNpgsql(connectionString);
        else
            options.UseSqlServer(connectionString);
    }, ServiceLifetime.Transient);

    builder.Services.AddSingleton<ITableNamingStrategy>(string.Equals(dbProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase)
        ? new PostgreSqlNamingStrategy()
        : new SqlServerNamingStrategy());
}
else
{
    builder.Services.AddDbContext<FilesDbContext>(options =>
        options.UseInMemoryDatabase("FilesModule"));
    builder.Services.AddDbContext<CoreDbContext>(options =>
        options.UseInMemoryDatabase("FilesModule"), ServiceLifetime.Transient);
    builder.Services.AddSingleton<ITableNamingStrategy>(new PostgreSqlNamingStrategy());
}

// Files module business logic services
builder.Services.AddFilesServices(builder.Configuration);

// File storage engine (local filesystem, configurable base path)
var storagePath = builder.Configuration.GetValue<string>("Files:StoragePath");
if (string.IsNullOrWhiteSpace(storagePath))
{
    var dataDir = Environment.GetEnvironmentVariable("DOTNETCLOUD_DATA_DIR");
    storagePath = !string.IsNullOrWhiteSpace(dataDir)
        ? Path.Combine(dataDir, "storage")
        : Path.Combine(builder.Environment.ContentRootPath, "storage");
}
builder.Services.AddSingleton<IFileStorageEngine>(sp =>
    new LocalFileStorageEngine(storagePath, sp.GetRequiredService<ILogger<LocalFileStorageEngine>>()));

// In-process event bus for standalone operation
builder.Services.AddSingleton<IEventBus, InProcessEventBus>();

// Search FTS client for full-text search via Search module gRPC
// DOTNETCLOUD_SEARCH_MODULE_ENDPOINT is set by ProcessSupervisor to the Search module's
// dynamically-allocated gRPC address. Falls back to config for local development.
var searchModuleAddress = Environment.GetEnvironmentVariable("DOTNETCLOUD_SEARCH_MODULE_ENDPOINT");
if (!string.IsNullOrWhiteSpace(searchModuleAddress))
{
    builder.Services.AddSearchFtsClient(searchModuleAddress);
}
else
{
    builder.Services.AddSearchFtsClient(builder.Configuration);
}

// gRPC
builder.Services.AddGrpc();

// REST API controllers
builder.Services.AddControllers(options =>
{
    options.Filters.Add<DotNetCloud.Modules.Files.Filters.DeviceIdentityFilter>();
});

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck<FilesHealthCheck>("files_module");

// OpenAPI document generation
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Info.Title = "DotNetCloud Files API";
        document.Info.Version = "1.0.0";
        document.Info.Description = "Files module REST API — upload, download, share, version, and manage files and folders.";
        return Task.CompletedTask;
    });
});

var app = builder.Build();

// Show full exception details for debugging; remove in production.
app.UseDeveloperExceptionPage();

// --- Middleware ---

// Map gRPC services
app.MapGrpcService<FilesGrpcService>();
app.MapGrpcService<FilesLifecycleService>();

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

// OpenAPI + Scalar interactive docs (development only)
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("DotNetCloud Files API Documentation")
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

// Minimal info endpoint
app.MapGet("/", () => Results.Ok(new
{
    module = "dotnetcloud.files",
    version = "1.0.0",
    status = "running"
}));

app.Run();

/// <summary>
/// Public Program type for integration testing via <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program;
