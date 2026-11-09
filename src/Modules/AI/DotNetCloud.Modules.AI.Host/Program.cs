using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Grpc;
using DotNetCloud.Modules.AI;
using DotNetCloud.Modules.AI.Data;
using DotNetCloud.Modules.AI.Host.Controllers;
using DotNetCloud.Modules.AI.Host.Services;
using DotNetCloud.Modules.AI.Services;
using DotNetCloud.Modules.Sdk;
using DotNetCloud.Modules.Sdk.Api;
using DotNetCloud.Modules.Sdk.Testing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Bind the endpoint the supervisor assigned: a Unix socket, a named pipe or loopback TCP.
// Without this the host is not addressable by ProcessSupervisor at all.
DotNetCloud.Core.Grpc.ModuleGrpcEndpointBinding.ConfigureModuleGrpcEndpoint(builder);

// Legacy shared config (DOTNETCLOUD_CONFIG_DIR) is only present when the supervisor still launches
// this module in compatibility mode (ProcessSupervisor:AllowLegacyAccess). A sandboxed module gets
// the non-secret topology in module.json instead.
var configDir = Environment.GetEnvironmentVariable("DOTNETCLOUD_CONFIG_DIR");
if (!string.IsNullOrEmpty(configDir))
{
    var configPath = Path.Combine(configDir, "config.json");
    if (File.Exists(configPath))
        builder.Configuration.AddJsonFile(configPath, optional: true, reloadOnChange: false);
}

var moduleConfigDir = Environment.GetEnvironmentVariable("DOTNETCLOUD_MODULE_CONFIG_DIR");
if (!string.IsNullOrEmpty(moduleConfigDir))
{
    var moduleJsonPath = Path.Combine(moduleConfigDir, "module.json");
    if (File.Exists(moduleJsonPath))
        builder.Configuration.AddJsonFile(moduleJsonPath, optional: false, reloadOnChange: false);
}

// DataProtection keys are private to the module (F1/F4). A sandboxed host must never share the
// core's key ring: sharing it would let the module mint core cookies, and the core's rotation would
// silently break the module.
var moduleDataDir = Environment.GetEnvironmentVariable("DOTNETCLOUD_MODULE_DATA_DIR");
var dpKeysPath = !string.IsNullOrWhiteSpace(moduleDataDir)
    ? Path.Combine(moduleDataDir, "data-protection-keys")
    : Path.Combine(builder.Environment.ContentRootPath, "data-protection-keys");
Directory.CreateDirectory(dpKeysPath);
builder.Services.AddDataProtection()
    .SetApplicationName("DotNetCloud.Modules.AI")
    .PersistKeysToFileSystem(new DirectoryInfo(dpKeysPath));

// Identity (F4/F5): the core reverse-proxy strips the browser's session cookie and mints a
// short-lived, module-audience assertion instead. The module therefore authenticates no user of its
// own — no cookie scheme, no shared key ring — and every request's acting user comes from
// IModuleUserAssertionAccessor (see AiChatController).
builder.Services.AddModuleUserAssertionAccessor();

// Register the gRPC-backed audit logger (SOC 2 CC4) — routes to Core.Server.
builder.Services.AddAuditLogger();

// Register the module as singleton
builder.Services.AddSingleton<AiModule>();

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
        "The AI module requires a database connection string and provider. " +
        "Sandboxed hosts receive them from ProcessSupervisor; legacy hosts read them from " +
        "config.json (DOTNETCLOUD_CONFIG_DIR).");
}

var isPostgreSql = string.Equals(dbProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase)
    || string.Equals(dbProvider, "Postgres", StringComparison.OrdinalIgnoreCase);

// The model is schema-neutral on purpose: the provisioned connection string pins search_path
// (PostgreSQL) or the user's default schema (SQL Server), so unqualified DDL — including the
// migration-history table — lands in the schema this module owns. The naming strategy is still
// needed for the provider-specific sequential-Guid defaults.
builder.Services.AddSingleton<ITableNamingStrategy>(
    isPostgreSql ? new PostgreSqlNamingStrategy() : new SqlServerNamingStrategy());

builder.Services.AddDbContext<AiDbContext>(options =>
{
    if (isPostgreSql)
    {
        options.UseNpgsql(connectionString, npgsql =>
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory"));
    }
    else
    {
        options.UseSqlServer(connectionString, sql =>
        {
            sql.EnableRetryOnFailure(maxRetryCount: 3);
            sql.CommandTimeout(30);
            sql.MigrationsAssembly("DotNetCloud.Modules.AI.Data.SqlServer");
            sql.MigrationsHistoryTable("__EFMigrationsHistory");
        });
    }
});

// Core API access goes through the SDK: one client, one credential, the canonical module_api
// surface. Module settings (provider, model, API key) live in the core's module settings plane —
// administered through the core admin API — and are resolved by IAiSettingsProvider at request time.
ModuleEnvironment.TryFromEnvironment(out var moduleEnvironment);
if (moduleEnvironment is not null)
{
    builder.Services.AddSingleton(moduleEnvironment);
    builder.Services.AddSingleton<IDncModuleApi>(_ => new DncModuleApiClient(moduleEnvironment));
}
else
{
    // Local development without a supervisor: the in-memory harness keeps the module runnable and
    // every setting falls back to appsettings.json.
    builder.Services.AddSingleton<IDncModuleApi, InMemoryDncModuleApi>();
}

builder.Services.AddScoped<IAiSettingsProvider, AiSettingsProvider>();

// Register all business-logic services (completion queue, Ollama HTTP client, chat service).
builder.Services.AddAiServices(builder.Configuration);

// gRPC
builder.Services.AddGrpc(options =>
    options.Interceptors.Add<DotNetCloud.Core.Grpc.ModulePeerAuthenticationInterceptor>());
builder.Services.AddControllers(options =>
    // A request without a verified assertion must be a 401, never a 500.
    options.Filters.Add<AiExceptionFilter>());
builder.Services.AddHealthChecks()
    .AddCheck<AiHealthCheck>("ai_module");

// Account erasure (F12): the module owns the conversations of a deleted account, so it owns their
// deletion. The core calls EraseUserData and retries until the module acknowledges.
builder.Services.AddModuleUserErasureHandler<AiUserErasureHandler>();

var app = builder.Build();

// Adopt the core-signed end-user assertion before anything can call back into the core.
app.UseModuleUserAssertion();

// Self-migrate on startup (dataStore: "isolated" means the module owns its schema). Retries with
// exponential backoff so a database that is briefly unavailable does not crash the module — it
// continues in degraded mode and resumes once the database recovers.
{
    const int maxAttempts = 5;
    var delay = TimeSpan.FromSeconds(2);
    var initLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("AiDbInit");

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AiDbContext>();
            await AiDbInitializer.InitializeAsync(db, initLogger);
            break;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            initLogger.LogWarning(ex,
                "AI database migration attempt {Attempt}/{MaxAttempts} failed. Retrying in {Delay}s...",
                attempt, maxAttempts, delay.TotalSeconds);
            await Task.Delay(delay);
            delay *= 2;
        }
        catch (Exception ex)
        {
            initLogger.LogError(ex,
                "AI database migration failed after {MaxAttempts} attempts. " +
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

// Serve this module's own web UI (phase A5). The core frames /apps/dotnetcloud.ai/ and proxies it
// here, so the page and its assets are plain static files of this host — index.html is the default
// document, and every reference in it is relative because the proxy strips the prefix.
app.UseDefaultFiles();
app.UseStaticFiles();

// Map gRPC services
app.MapGrpcService<AiGrpcService>();
app.MapGrpcService<AiLifecycleService>();

// Map REST API controllers
app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health");

// Liveness/version probe for operators. Deliberately NOT "/", which belongs to the module's own UI
// document (UseDefaultFiles serves wwwroot/index.html there).
app.MapGet("/api/info", () => Results.Ok(new
{
    module = "dotnetcloud.ai",
    version = "2.0.0",
    status = "running"
}));

app.Run();

/// <summary>Marker class for integration test host reference.</summary>
public partial class Program;
