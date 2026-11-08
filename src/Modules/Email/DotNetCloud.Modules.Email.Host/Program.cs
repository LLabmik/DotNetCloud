using DotNetCloud.Core.Data.Extensions;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Grpc;
using DotNetCloud.Core.Security;
using DotNetCloud.Modules.Email;
using DotNetCloud.Modules.Email.Data;
using DotNetCloud.Modules.Email.Host.Controllers;
using DotNetCloud.Modules.Email.Host.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;
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
    .SetApplicationName("DotNetCloud.Modules.Email")
    .PersistKeysToFileSystem(new DirectoryInfo(dpKeysPath));

// Identity (F4/F5): the core reverse-proxy strips the browser's session cookie and mints a
// short-lived, module-audience assertion instead. The module therefore authenticates no user of its
// own — no cookie scheme, no shared key ring — and every request's acting user comes from
// IModuleUserAssertionAccessor (see EmailControllerBase).
builder.Services.AddModuleUserAssertionAccessor();

// Register the gRPC-backed audit logger (SOC 2 CC4) — routes to Core.Server.
builder.Services.AddAuditLogger();

// Real-time search indexing bridge — forwards SearchIndexRequestEvent to Core.Server.
builder.Services.AddSearchIndexBridge();

// Register the module as singleton
builder.Services.AddSingleton<EmailModule>();

// File validation service for upload security
builder.Services.AddSingleton<IFileValidationService, FileValidationService>();

// Register EF Core with SQL Server or PostgreSQL based on configuration. A sandboxed module is
// handed a scoped connection string (its own principal, its own schema, search_path pinned); only a
// legacy launch falls back to the shared config.json.
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
        "The Email module requires a database connection string and provider. " +
        "These are provided via config.json (DOTNETCLOUD_CONFIG_DIR) when launched by the core server.");
}

var provider = ResolveDatabaseProvider(dbProvider);

// Register naming strategy so EmailDbContext uses correct table/column names
// (snake_case for PostgreSQL, PascalCase for SQL Server).
if (string.Equals(dbProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddSingleton<ITableNamingStrategy, PostgreSqlNamingStrategy>();
else
    builder.Services.AddSingleton<ITableNamingStrategy, SqlServerNamingStrategy>();

builder.Services.AddDbContext<EmailDbContext>(options =>
    DbResiliencePolicy.Configure(
        options,
        provider,
        connectionString,
        provider == DatabaseProvider.SqlServer ? "DotNetCloud.Modules.Email.Data.SqlServer" : null));

// In-process event bus for standalone operation
builder.Services.AddSingleton<IEventBus, InProcessEventBus>();

// Register all business-logic services
builder.Services.AddEmailServices(builder.Configuration);

// Lets a core-initiated operation resolve the user the core signed an assertion for (account sync).
builder.Services.AddModuleUserAssertionAccessor();

// gRPC
builder.Services.AddGrpc(options =>
    options.Interceptors.Add<DotNetCloud.Core.Grpc.ModulePeerAuthenticationInterceptor>());
builder.Services.AddControllers(options =>
    // A request without a verified assertion must be a 401, never a 500 (see the filter's remarks).
    options.Filters.Add<EmailExceptionFilter>());
builder.Services.AddHealthChecks()
    .AddCheck<EmailHealthCheck>("email_module");

// Account erasure (F12): this module owns a deleted user's accounts, messages and rules, so it
// owns their deletion. The core calls EraseUserData and retries until the module acknowledges.
builder.Services.AddModuleUserErasureHandler<EmailUserErasureHandler>();

var app = builder.Build();

// Adopt the core-signed end-user assertion before anything can call back into the core.
app.UseModuleUserAssertion();

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
            var db = scope.ServiceProvider.GetRequiredService<EmailDbContext>();
            await EmailDbInitializer.InitializeAsync(db, initLogger, CancellationToken.None);
            break;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            initLogger.LogWarning(ex,
                "Email database initialization attempt {Attempt}/{MaxAttempts} failed. Retrying in {Delay}s...",
                attempt, maxAttempts, delay.TotalSeconds);
            await Task.Delay(delay);
            delay *= 2;
        }
        catch (Exception ex)
        {
            initLogger.LogError(ex,
                "Email database initialization failed after {MaxAttempts} attempts. " +
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

// Serve this module's own web UI (phase A5). The core frames /apps/dotnetcloud.email/ and proxies
// it here, so the page and its assets are plain static files of this host — index.html is the
// default document, and every reference in it is relative because the proxy strips the prefix.
app.UseDefaultFiles();
app.UseStaticFiles();

// Map gRPC services
app.MapGrpcService<EmailGrpcService>();
app.MapGrpcService<EmailLifecycleService>();

// Map REST API controllers
// Trust X-Forwarded-Proto from the YARP proxy so host-derived URLs match the public origin.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
    KnownProxies = { System.Net.IPAddress.Loopback, System.Net.IPAddress.IPv6Loopback },
});
app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health");

// Minimal info endpoint
app.MapGet("/api/info", () => Results.Ok(new
{
    module = "dotnetcloud.email",
    version = "1.1.0",
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
