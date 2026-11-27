using DotNetCloud.Core.Data.Extensions;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Grpc;
using DotNetCloud.Core.Grpc.Services;
using DotNetCloud.Modules.Photos;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Photos.Host.Controllers;
using DotNetCloud.Modules.Photos.Host.Services;
using DotNetCloud.Modules.Sdk;
using DotNetCloud.Modules.Sdk.Api;
using DotNetCloud.Modules.Sdk.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

// Legacy shared config (DOTNETCLOUD_CONFIG_DIR) is only present when the supervisor still launches
// this module in compatibility mode (ProcessSupervisor:AllowLegacyAccess). A sandboxed module gets
// the non-secret topology in module.json instead.
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
var moduleDataDir = Environment.GetEnvironmentVariable("DOTNETCLOUD_MODULE_DATA_DIR");
var dpKeysPath = !string.IsNullOrWhiteSpace(moduleDataDir)
    ? Path.Combine(moduleDataDir, "data-protection-keys")
    : Path.Combine(builder.Environment.ContentRootPath, "data-protection-keys");
Directory.CreateDirectory(dpKeysPath);
builder.Services.AddDataProtection()
    .SetApplicationName("DotNetCloud.Modules.Photos")
    .PersistKeysToFileSystem(new DirectoryInfo(dpKeysPath));

// Identity (F4/F5): the core reverse-proxy strips the browser's session cookie and mints a
// short-lived, module-audience assertion instead. The module therefore authenticates no user of its
// own — no cookie scheme, no shared key ring — and every request's acting user comes from
// IModuleUserAssertionAccessor (see PhotosControllerBase).
builder.Services.AddModuleUserAssertionAccessor();

// The core module API: settings, audit, notifications, search submission and the acting user's
// files (IFileDirectory). A host started without a supervisor environment (a local `dotnet run`)
// gets the in-memory fake so the UI still loads.
ModuleEnvironment.TryFromEnvironment(out var moduleEnvironment);
if (moduleEnvironment is not null)
{
    builder.Services.AddSingleton(moduleEnvironment);
    builder.Services.AddSingleton<IDncModuleApi>(_ => new DncModuleApiClient(moduleEnvironment));
}
else
{
    builder.Services.AddSingleton<IDncModuleApi, InMemoryDncModuleApi>();
}

// Register the gRPC-backed audit logger (SOC 2 CC4) — routes to Core.Server.
builder.Services.AddAuditLogger();

// Real-time search indexing bridge — forwards SearchIndexRequestEvent to Core.Server.
builder.Services.AddSearchIndexBridge();

// Register the module as singleton
builder.Services.AddSingleton<PhotosModule>();

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
        "The Photos module requires a database connection string and provider. " +
        "These are provided via config.json (DOTNETCLOUD_CONFIG_DIR) when launched by the core server.");
}

var provider = ResolveDatabaseProvider(dbProvider);

builder.Services.AddSingleton<ITableNamingStrategy>(
    provider == DatabaseProvider.SqlServer ? new SqlServerNamingStrategy() : new PostgreSqlNamingStrategy());

builder.Services.AddDbContext<PhotosDbContext>(options =>
    DbResiliencePolicy.Configure(
        options,
        provider,
        connectionString,
        provider == DatabaseProvider.SqlServer ? "DotNetCloud.Modules.Photos.Data.SqlServer" : null));


// In-process event bus for standalone operation
builder.Services.AddSingleton<IEventBus, InProcessEventBus>();

// Register all business-logic services
builder.Services.AddPhotosServices(builder.Configuration);

// Team directory capability over gRPC (short-lived cache) — lets share/read services
// resolve a caller's team membership from Core.Server without a round trip per access query.
builder.Services.AddGrpcTeamDirectory();

// gRPC
builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<DotNetCloud.Core.Grpc.ModulePeerAuthenticationInterceptor>();

    // Core-initiated calls (media import, scheduled work) carry a core-signed user assertion; adopt
    // it as the ambient user so a handler never has to trust a user id in the payload.
    options.Interceptors.Add<DotNetCloud.Core.Grpc.ModuleUserAssertionGrpcInterceptor>();
});
builder.Services.AddControllers(options =>
    // A request without a verified assertion must be a 401, never a 500 (see the filter's remarks).
    options.Filters.Add<PhotosExceptionFilter>())
    // The framed UI is JavaScript and sends enum names ("Rotate"), which MVC cannot bind on its own.
    .AddModuleJsonOptions();
builder.Services.AddHealthChecks()
    .AddCheck<PhotosHealthCheck>("photos_module");

// Account erasure (F12): this module owns the photo library of a deleted account, so it owns their
// deletion. The core calls EraseUserData and retries until the module acknowledges.
builder.Services.AddModuleUserErasureHandler<PhotosUserErasureHandler>();

var app = builder.Build();

// Adopt the core-signed end-user assertion before anything can call back into the core.
app.UseModuleUserAssertion();

// Initialize the database schema (creates tables if they don't exist).
// Retries with exponential backoff so a database that is briefly unavailable at startup does not
// crash the module — it continues running in degraded mode and resumes once the database recovers.
{
    const int maxAttempts = 5;
    var delay = TimeSpan.FromSeconds(2);
    var initLogger = app.Services.GetRequiredService<ILogger<Program>>();

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PhotosDbContext>();
            await PhotosDbInitializer.InitializeAsync(db, initLogger, CancellationToken.None);
            break;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            initLogger.LogWarning(ex,
                "Photos database initialization attempt {Attempt}/{MaxAttempts} failed. Retrying in {Delay}s...",
                attempt, maxAttempts, delay.TotalSeconds);
            await Task.Delay(delay);
            delay *= 2;
        }
        catch (Exception ex)
        {
            initLogger.LogError(ex,
                "Photos database initialization failed after {MaxAttempts} attempts. " +
                "Continuing in degraded mode; retries will resume when the database recovers.",
                maxAttempts);
        }
    }
}

// Show exception details only while developing; a sandboxed host must not leak stack traces.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Serve this module's own web UI (phase A5). The core frames /apps/dotnetcloud.photos/ and proxies
// it here, so the page and its assets are plain static files of this host — index.html is the default
// document, and every reference in it is relative because the proxy strips the prefix.
app.UseDefaultFiles();
app.UseStaticFiles();

// Map gRPC services
app.MapGrpcService<PhotosGrpcServiceImpl>();
app.MapGrpcService<PhotosLifecycleService>();

// Map REST API controllers
// Trust X-Forwarded-Proto from the YARP proxy so host derivation matches the public origin.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto,
    KnownProxies = { System.Net.IPAddress.Loopback, System.Net.IPAddress.IPv6Loopback },
});
app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health");

// Minimal info endpoint. Deliberately not mapped at "/": a mapped route there would shadow
// wwwroot/index.html, which static files skip once endpoint routing has matched.
app.MapGet("/api/info", () => Results.Ok(new
{
    module = "dotnetcloud.photos",
    version = "2.0.0",
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
