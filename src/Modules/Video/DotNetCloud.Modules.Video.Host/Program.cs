using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Services;
using DotNetCloud.Modules.Video;
using DotNetCloud.Modules.Video.Data;
using DotNetCloud.Modules.Video.Host.Services;
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

// Cookie auth — same cookie name as Core.Server. SecurePolicy=None because
// the YARP proxy forwards over HTTP (localhost).
builder.Services.AddAuthentication("Identity.Application")
    .AddCookie("Identity.Application", options =>
    {
        options.Cookie.Name = ".AspNetCore.Identity.Application";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.None;
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

// Register Files DbContext and storage engine so IDownloadService can be resolved.
builder.Services.AddDbContext<FilesDbContext>(options =>
{
    const string filesMigrationsAssembly = "DotNetCloud.Modules.Files.Data.SqlServer";

    switch (provider)
    {
        case DatabaseProvider.PostgreSQL:
            options.UseNpgsql(connectionString);
            break;
        case DatabaseProvider.SqlServer:
            options.UseSqlServer(connectionString, sqlServerOptions =>
            {
                sqlServerOptions.MigrationsAssembly(filesMigrationsAssembly);
            });
            break;
    }
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
