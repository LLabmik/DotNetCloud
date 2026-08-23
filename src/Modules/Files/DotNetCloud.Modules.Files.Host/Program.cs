using DotNetCloud.Core.Auth.Authorization;
using DotNetCloud.Core.Auth.Introspection;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Grpc;
using DotNetCloud.Core.ServiceDefaults.Media;
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
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

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

// Register token introspection client (replaces local JWT key validation).
// Bearer tokens are validated by calling Core.Server's TokenIntrospection gRPC service.
builder.Services.AddTokenIntrospection();

// Register the gRPC-backed audit logger (SOC 2 CC4) — routes to Core.Server.
builder.Services.AddAuditLogger();

// Authentication: supports both cookie (browser/Blazor) and introspection (desktop/mobile).
// A policy scheme automatically routes to the correct handler based on the request.
// The introspection handler validates bearer tokens by calling Core.Server's
// TokenIntrospection gRPC service — no local signing keys needed.
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
    .AddIntrospection(IntrospectionAuthenticationExtensions.SchemeName)
    .AddPolicyScheme("DotNetCloud.Module", "DotNetCloud.Module", options =>
    {
        // Route to introspection handler for requests with Authorization: Bearer header
        // (desktop/mobile clients). Route to Cookie handler for browser/Blazor requests.
        options.ForwardDefaultSelector = context =>
        {
            if (context.Request.Headers.TryGetValue("Authorization", out var auth)
                && auth.Count > 0
                && auth[0]?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
            {
                return IntrospectionAuthenticationExtensions.SchemeName;
            }
            return "Identity.Application";
        };
    });

builder.Services.AddAuthorization(options => AuthorizationPolicies.Configure(options));
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

// --- Services ---

// Register the Files module as a singleton for lifecycle management
builder.Services.AddSingleton<FilesModule>();

// Register EF Core with the configured database provider (no in-memory fallback)
var connectionString = builder.Configuration["connectionString"]
    ?? builder.Configuration.GetConnectionString("DefaultConnection");
var dbProvider = builder.Configuration["databaseProvider"]
    ?? builder.Configuration["database:provider"];

if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(dbProvider))
{
    throw new InvalidOperationException(
        "The Files module requires a database connection string and provider. " +
        "These are provided via config.json (DOTNETCLOUD_CONFIG_DIR) when launched by the core server.");
}

builder.Services.AddDbContext<FilesDbContext>(options =>
{
    if (string.Equals(dbProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase))
        options.UseNpgsql(connectionString);
    else
        options.UseSqlServer(connectionString, sql => sql.MigrationsAssembly("DotNetCloud.Modules.Files.Data.SqlServer"));
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

// Files module business logic services
builder.Services.AddFilesServices(builder.Configuration);

// Media metadata extractors (EXIF for photos, tag readers for audio/video)
builder.Services.AddMediaMetadataExtractors();

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

// Request decompression — handles gzip-compressed chunk upload bodies from
// desktop/mobile clients that use Content-Encoding: gzip on chunk PUT requests.
builder.Services.AddRequestDecompression();

// Rate limiting — policies must be registered so [EnableRateLimiting] attributes on
// controllers don't throw 502. Actual enforcement happens in Core.Server's YARP proxy;
// these use the same config values for consistency.
builder.Services.AddRateLimiter(limiterOptions =>
{
    limiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Load module rate limits from config (same section as Core.Server)
    var rateLimitSection = builder.Configuration.GetSection("RateLimiting:ModuleLimits");

    void AddModulePolicy(string moduleName, string policyName)
    {
        var config = rateLimitSection.GetSection(moduleName);
        var permitLimit = config.GetValue<int?>("PermitLimit") ?? 100;
        var windowSeconds = config.GetValue<int?>("WindowSeconds") ?? 60;
        var perDevice = config.GetValue<bool?>("PerDevice") ?? false;

        limiterOptions.AddPolicy(policyName, context =>
        {
            var userId = context.User?.FindFirst("sub")?.Value ?? "anonymous";
            var partitionKey = perDevice
                ? $"{moduleName}:{userId}:{context.Request.Headers["X-Device-Id"].FirstOrDefault() ?? "no-device"}"
                : $"{moduleName}:{userId}";

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: partitionKey,
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = TimeSpan.FromSeconds(windowSeconds),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
        });
    }

    AddModulePolicy("sync-changes", "module-sync-changes");
    AddModulePolicy("sync-tree", "module-sync-tree");
    AddModulePolicy("sync-reconcile", "module-sync-reconcile");
    AddModulePolicy("sync-stream", "module-sync-stream");
    AddModulePolicy("upload-initiate", "module-upload-initiate");
    AddModulePolicy("upload-chunks", "module-upload-chunks");
    AddModulePolicy("download", "module-download");
});

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

// Request decompression — handles gzip-compressed chunk upload bodies from
// desktop/mobile clients that use Content-Encoding: gzip on chunk PUT requests.
app.UseRequestDecompression();

// Map REST API controllers
// Trust X-Forwarded-Proto from the YARP proxy so __Host- cookies work over HTTP.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto,
    KnownProxies = { System.Net.IPAddress.Loopback, System.Net.IPAddress.IPv6Loopback },
});
// Rate limiting middleware (after auth, before controllers)
app.UseRateLimiter();

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
