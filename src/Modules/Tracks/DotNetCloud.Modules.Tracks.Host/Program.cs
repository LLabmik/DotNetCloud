using DotNetCloud.Core.Data.Extensions;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Grpc;
using DotNetCloud.Core.Security;
using DotNetCloud.Modules.Tracks;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Host.Controllers;
using DotNetCloud.Modules.Tracks.Host.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

// Legacy shared config (DOTNETCLOUD_CONFIG_DIR) is only present when the supervisor launches this
// module in compatibility mode. A sandboxed module gets the non-secret topology in module.json.
var configDir = Environment.GetEnvironmentVariable("DOTNETCLOUD_CONFIG_DIR");
if (!string.IsNullOrEmpty(configDir))
{
    var p = Path.Combine(configDir, "config.json");
    if (File.Exists(p))
        builder.Configuration.AddJsonFile(p, optional: true, reloadOnChange: false);
}

var moduleConfigDir = Environment.GetEnvironmentVariable("DOTNETCLOUD_MODULE_CONFIG_DIR");
if (!string.IsNullOrEmpty(moduleConfigDir))
{
    var moduleJsonPath = Path.Combine(moduleConfigDir, "module.json");
    if (File.Exists(moduleJsonPath))
        builder.Configuration.AddJsonFile(moduleJsonPath, optional: false, reloadOnChange: false);
}

// Bind the endpoint the supervisor assigned: a Unix socket, a named pipe or loopback TCP.
DotNetCloud.Core.Grpc.ModuleGrpcEndpointBinding.ConfigureModuleGrpcEndpoint(builder);

// DataProtection keys are private to the module (F1/F4). A sandboxed host must never share the
// core's key ring: sharing it would let the module mint core cookies, and the core's rotation would
// silently break the module.
//
// Legacy compatibility launches (no manifest yet) do not receive DOTNETCLOUD_MODULE_DATA_DIR, and the
// module's content root is /opt/dotnetcloud — read-only for the module user. Falling back to the
// shared DOTNETCLOUD_DATA_DIR keeps the pre-sandbox behaviour there; the private ring starts with the
// data-plane flip, which is the first launch that provides a module data directory.
var moduleDataDir = Environment.GetEnvironmentVariable("DOTNETCLOUD_MODULE_DATA_DIR");
string dpKeysPath;
string dpApplicationName;

if (!string.IsNullOrWhiteSpace(moduleDataDir))
{
    dpKeysPath = Path.Combine(moduleDataDir, "data-protection-keys");
    dpApplicationName = "DotNetCloud.Modules.Tracks";
}
else
{
    var legacyDataDir = Environment.GetEnvironmentVariable("DOTNETCLOUD_DATA_DIR");
    dpKeysPath = !string.IsNullOrWhiteSpace(legacyDataDir)
        ? Path.Combine(legacyDataDir, "data-protection-keys")
        : Path.Combine(builder.Environment.ContentRootPath, "data-protection-keys");
    dpApplicationName = "DotNetCloud";
}

Directory.CreateDirectory(dpKeysPath);
builder.Services.AddDataProtection()
    .SetApplicationName(dpApplicationName)
    .PersistKeysToFileSystem(new DirectoryInfo(dpKeysPath));

// Identity (F4/F5): the core reverse-proxy strips the browser's session cookie and mints a
// short-lived, module-audience assertion instead. The module therefore authenticates no user of its
// own — no cookie scheme, no shared key ring — and every request's acting user comes from
// IModuleUserAssertionAccessor (see TracksControllerBase).
builder.Services.AddModuleUserAssertionAccessor();

// Register the gRPC-backed audit logger (SOC 2 CC4) — routes to Core.Server.
builder.Services.AddAuditLogger();

// Organization membership is core data. The host has no access to core.organizations, so the
// organizations endpoint resolves the acting user's memberships through the capability.
builder.Services.AddGrpcOrganizationDirectory();

// Register the module as singleton
builder.Services.AddSingleton<TracksModule>();

// File validation service for upload security
builder.Services.AddSingleton<IFileValidationService, FileValidationService>();

// Register EF Core with the configured database provider. A sandboxed module is handed a scoped
// connection string (its own principal, its own schema, search_path pinned); only a legacy launch
// falls back to the shared config.json.
var sandboxConnectionString = Environment.GetEnvironmentVariable("DOTNETCLOUD_MODULE_DB_CONNECTION_STRING");
var connectionString = !string.IsNullOrEmpty(sandboxConnectionString)
    ? sandboxConnectionString
    : builder.Configuration["connectionString"]
        ?? builder.Configuration.GetConnectionString("DefaultConnection");
var dbProvider = builder.Configuration["databaseProvider"]
    ?? builder.Configuration["database:provider"];

if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(dbProvider))
{
    throw new InvalidOperationException(
        "The Tracks module requires a database connection string and provider. " +
        "These are provided via config.json (DOTNETCLOUD_CONFIG_DIR) when launched by the core server.");
}

var provider = ResolveDatabaseProvider(dbProvider);

builder.Services.AddSingleton<ITableNamingStrategy>(
    provider == DatabaseProvider.SqlServer ? new SqlServerNamingStrategy() : new PostgreSqlNamingStrategy());

// The SQL Server migrations live in their own assembly, so the host must name it for the provider.
builder.Services.AddDbContext<TracksDbContext>(options =>
    DbResiliencePolicy.Configure(
        options,
        provider,
        connectionString,
        provider == DatabaseProvider.SqlServer ? "DotNetCloud.Modules.Tracks.Data.SqlServer" : null));

// In-process event bus for standalone operation
builder.Services.AddSingleton<IEventBus, InProcessEventBus>();

// Register all business-logic services
builder.Services.AddTracksServices(builder.Configuration);

// Register the gRPC-based IUserDirectory so controllers/services can resolve
// user display names by calling Core.Server's CoreCapabilities gRPC service.
builder.Services.AddSingleton<DotNetCloud.Core.Capabilities.IUserDirectory, GrpcUserDirectoryService>();

// gRPC
builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<DotNetCloud.Core.Grpc.ModulePeerAuthenticationInterceptor>();

    // Core-initiated calls carry a core-signed user assertion; adopt it as the ambient user so a
    // handler never has to trust a user id in the payload.
    options.Interceptors.Add<DotNetCloud.Core.Grpc.ModuleUserAssertionGrpcInterceptor>();
});
builder.Services.AddControllers(options =>
    {
        // A request without a verified assertion must be a 401, never a 500. The gate runs before
        // model binding so even actions that never resolve a caller (share links, guest access,
        // webhook deliveries, the recurring-rule sweep) stay behind authentication.
        options.Filters.Add<TracksRequireUserAssertionFilter>();

        // Safety net for domain exceptions that escape a controller's own try/catch.
        options.Filters.Add<TracksExceptionFilter>();
    })
    // The framed UI sends enum names ("InProgress", "Rotate"), which MVC cannot bind on its own.
    .AddModuleJsonOptions();
builder.Services.AddHealthChecks()
    .AddCheck<TracksHealthCheck>("tracks_module");

// Account erasure (F12): this module owns products, work items and everything beneath them, so it
// owns their deletion. The core calls EraseUserData and retries until the module acknowledges.
builder.Services.AddModuleUserErasureHandler<TracksUserErasureHandler>();

var app = builder.Build();

// Adopt the core-signed end-user assertion before anything can call back into the core.
app.UseModuleUserAssertion();

// Show exception details only while developing; a sandboxed host must not leak stack traces.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Serve this module's own web UI. The core frames /apps/dotnetcloud.tracks/ and proxies it here, so
// the page and its assets are plain static files of this host — index.html is the default document,
// and every reference in it is relative because the proxy strips the prefix.
//
// The UI is a Blazor WebAssembly app (DotNetCloud.Modules.Tracks.Web). Its published files — including
// _framework/, which is what a standalone WebAssembly app loads — arrive here as this project's static
// web assets through the project reference, so plain static files is the whole story: no
// UseBlazorFrameworkFiles, which exists to map a *client* project's assets from a server project.
app.UseDefaultFiles();
app.UseStaticFiles();

// Map gRPC services
app.MapGrpcService<TracksGrpcService>();
app.MapGrpcService<TracksLifecycleService>();

// Map REST API controllers
// Trust X-Forwarded-Proto from the YARP proxy so __Host- cookies work over HTTP.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto,
    KnownProxies = { System.Net.IPAddress.Loopback, System.Net.IPAddress.IPv6Loopback },
});
app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health");

// Seed built-in product templates on startup (idempotent). A database that is briefly unavailable
// must not prevent the host from starting: the supervisor restarts a crashed host and the module's
// health check then reports the degraded state.
try
{
    using var seedScope = app.Services.CreateScope();
    var templateSeeder = seedScope.ServiceProvider.GetRequiredService<TemplateSeedService>();
    await templateSeeder.EnsureSeededAsync(CancellationToken.None);
}
catch (Exception ex)
{
    app.Services.GetRequiredService<ILoggerFactory>()
        .CreateLogger("Tracks.Startup")
        .LogWarning(ex, "Product template seeding failed; continuing in degraded mode.");
}

// Minimal info endpoint. Deliberately not mapped at "/": a mapped route there would shadow
// wwwroot/index.html, which static files skip once endpoint routing has matched.
app.MapGet("/api/info", () => Results.Ok(new
{
    module = "dotnetcloud.tracks",
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
