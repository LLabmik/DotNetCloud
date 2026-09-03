using DotNetCloud.Core.Auth.Authorization;
using DotNetCloud.Core.Auth.Introspection;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Grpc;
using DotNetCloud.Core.Data.Extensions;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Services;
using DotNetCloud.Modules.Music;
using DotNetCloud.Modules.Music.Data;
using DotNetCloud.Modules.Music.Data.Services;
using DotNetCloud.Modules.Music.Host.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Force console output to verify ProcessSupervisor capture works
Console.Error.WriteLine("[MUSIC] Module starting...");
Console.Error.Flush();

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

// Real-time search indexing bridge — forwards SearchIndexRequestEvent to Core.Server.
builder.Services.AddSearchIndexBridge();

// Authentication: supports both cookie (browser/Blazor) and introspection (desktop/mobile).
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
        options.Events.OnValidatePrincipal = context =>
        {
            Console.Error.WriteLine($"[MUSIC] OnValidatePrincipal: Principal is null={context.Principal == null}, Identity is null={context.Principal?.Identity == null}, IsAuthenticated={context.Principal?.Identity?.IsAuthenticated}, Name={context.Principal?.Identity?.Name}, AuthType={context.Principal?.Identity?.AuthenticationType}");
            Console.Error.Flush();
            if (context.Principal?.Identity?.IsAuthenticated == true)
                return Task.CompletedTask;
            Console.Error.WriteLine("[MUSIC] OnValidatePrincipal: REJECTING principal");
            Console.Error.Flush();
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
        // Route to introspection handler for Bearer tokens, Cookie handler for browser requests
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

// Register the module as singleton
builder.Services.AddSingleton<MusicModule>();

// Register EF Core with the configured database provider (no in-memory fallback)
var connectionString = builder.Configuration["connectionString"]
    ?? builder.Configuration.GetConnectionString("DefaultConnection");
var dbProvider = builder.Configuration["databaseProvider"]
    ?? builder.Configuration["database:provider"];

if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(dbProvider))
{
    throw new InvalidOperationException(
        "The Music module requires a database connection string and provider. " +
        "These are provided via config.json (DOTNETCLOUD_CONFIG_DIR) when launched by the core server.");
}

builder.Services.AddSingleton<ITableNamingStrategy>(
    string.Equals(dbProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase)
        ? new PostgreSqlNamingStrategy()
        : new SqlServerNamingStrategy());

var provider = ResolveDatabaseProvider(dbProvider);

void ConfigureDb(DbContextOptionsBuilder o) =>
    DbResiliencePolicy.Configure(o, provider, connectionString);

builder.Services.AddDbContextFactory<MusicDbContext>(ConfigureDb);
builder.Services.AddDbContext<MusicDbContext>(ConfigureDb);

// Register Files DbContext and storage engine so IDownloadService can be resolved.
builder.Services.AddDbContextFactory<FilesDbContext>(ConfigureDb);
builder.Services.AddDbContext<FilesDbContext>(ConfigureDb);
builder.Services.AddSingleton<IFileStorageEngine>(
    sp => new LocalFileStorageEngine(
        builder.Configuration["Files:Storage:RootPath"] ?? "/var/lib/dotnetcloud/storage",
        sp.GetRequiredService<ILogger<LocalFileStorageEngine>>()));

// In-process event bus for standalone operation
builder.Services.AddSingleton<IEventBus, InProcessEventBus>();

// Register all business-logic services
builder.Services.AddMusicServices(builder.Configuration);

// gRPC
builder.Services.AddGrpc();
builder.Services.AddControllers();
builder.Services.AddHealthChecks()
    .AddCheck<MusicHealthCheck>("music_module");

var app = builder.Build();

// Show full exception details for debugging; remove in production.
app.UseDeveloperExceptionPage();

// Log all unhandled exceptions to stderr (captured by ProcessSupervisor).
app.Use(async (context, next) =>
{
    try
    { await next(); }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[MUSIC] UNHANDLED: {ex}");
        Console.Error.Flush();
        throw;
    }
});

// Map gRPC services
app.MapGrpcService<MusicGrpcServiceImpl>();
app.MapGrpcService<MusicLifecycleService>();

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
    module = "dotnetcloud.music",
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
