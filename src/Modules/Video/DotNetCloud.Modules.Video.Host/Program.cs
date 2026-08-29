using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Data.Extensions;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Grpc;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Services;
using DotNetCloud.Modules.Video;
using DotNetCloud.Modules.Video.Data;
using DotNetCloud.Modules.Video.Host.Services;
using DotNetCloud.Modules.Video.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
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

// Register naming strategy for module DbContext based on configured provider
builder.Services.AddSingleton<ITableNamingStrategy>(provider == DatabaseProvider.SqlServer
    ? new SqlServerNamingStrategy()
    : new PostgreSqlNamingStrategy());

// Register EF Core with the configured database provider
builder.Services.AddDbContext<VideoDbContext>(options =>
{
    DbResiliencePolicy.Configure(
        options,
        provider,
        connectionString,
        provider == DatabaseProvider.SqlServer ? "DotNetCloud.Modules.Video.Data.SqlServer" : null);

    // Suppress pending model changes warning for PostgreSQL provider
    options.ConfigureWarnings(warnings =>
    {
        warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning);
    });
});

// Register Files DbContext and storage engine so IDownloadService can be resolved.
builder.Services.AddDbContext<FilesDbContext>(options =>
{
    DbResiliencePolicy.Configure(
        options,
        provider,
        connectionString,
        provider == DatabaseProvider.SqlServer ? "DotNetCloud.Modules.Files.Data.SqlServer" : null);

    options.ConfigureWarnings(warnings =>
    {
        warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning);
    });
});
builder.Services.AddSingleton<IFileStorageEngine>(
    sp => new LocalFileStorageEngine(
        builder.Configuration["Files:Storage:RootPath"] ?? "/var/lib/dotnetcloud/storage",
        sp.GetRequiredService<ILogger<LocalFileStorageEngine>>()));

// In-process event bus for standalone operation
builder.Services.AddSingleton<IEventBus, InProcessEventBus>();

// Register all business-logic services
builder.Services.AddVideoServices(builder.Configuration);

// ─── Transcoding services (Host-specific) ─────────────────────
builder.Services.AddSingleton<FfmpegArgumentBuilder>();
builder.Services.AddSingleton<FfmpegProcessManager>();
builder.Services.AddSingleton<TranscodeCacheService>();
builder.Services.AddSingleton<TranscodingJobTracker>();
builder.Services.AddScoped<IVideoTranscodingService, VideoTranscodingService>();

// HLS idle watchdog — cancels abandoned transcode streams (safety net for
// crashes/network drops where no client cancel signal arrives).
builder.Services.AddHostedService<HlsStreamWatchdog>();

// gRPC
builder.Services.AddGrpc();
builder.Services.AddControllers();
builder.Services.AddHealthChecks()
    .AddCheck<VideoHealthCheck>("video_module");

var app = builder.Build();

// Show full exception details for debugging; remove in production.
app.UseDeveloperExceptionPage();

// Map gRPC services
app.MapGrpcService<VideoGrpcServiceImpl>();
app.MapGrpcService<VideoLifecycleService>();

// Map REST API controllers
// Trust X-Forwarded-Proto from the YARP proxy so __Host- cookies work over HTTP.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto,
    KnownProxies = { System.Net.IPAddress.Loopback, System.Net.IPAddress.IPv6Loopback },
});
app.UseAuthentication();
app.UseAuthorization();

// Thumbnail endpoints — served as minimal APIs BEFORE MapControllers
// to avoid pulling in the full controller DI chain for simple image serving.
app.MapGet("/api/v1/videos/{videoId:guid}/thumbnail", async (Guid videoId, IVideoThumbnailService thumbnailService) =>
{
    var (stream, contentType) = await thumbnailService.GetThumbnailAsync(videoId);
    if (stream is null)
        return Results.NotFound();
    return Results.File(stream, contentType ?? "image/jpeg");
});

app.MapGet("/api/v1/series/{seriesId:guid}/thumbnail", async (Guid seriesId, VideoDbContext db, IConfiguration config) =>
{
    var storageRoot = config["Files:Storage:RootPath"] ?? "/var/lib/dotnetcloud/storage";
    var mediaCachePath = config["Files:Storage:MediaCachePath"]
        ?? Path.Combine(storageRoot, ".media-cache");
    // Try canonical series poster hash in content-addressed storage
    var canonicalPosterHash = await db.CanonicalVideoSeries
        .Where(s => s.Id == seriesId)
        .Select(s => s.PosterHash)
        .FirstOrDefaultAsync();

    if (!string.IsNullOrEmpty(canonicalPosterHash))
    {
        var prefix = canonicalPosterHash.Length >= 2 ? canonicalPosterHash[..2] : canonicalPosterHash;
        var dir = Path.Combine(mediaCachePath, "images", prefix);
        if (Directory.Exists(dir))
        {
            var files = Directory.GetFiles(dir, $"{canonicalPosterHash}.*");
            if (files.Length > 0)
                return Results.File(await File.ReadAllBytesAsync(files[0]), "image/jpeg");
        }
    }
    return Results.NotFound();
});

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
