# Database / Server Outage Resilience — Implementation Plan

**Branch:** `fix/database-offline-recovery`
**Status:** Implemented (2026-08-23) — all phases A–G code complete, builds pass, targeted tests pass. Manual live-outage simulations pending (see §12).
**Scope:** DotNetCloud Core.Server, process-isolated module hosts, Android client, SyncTray desktop client.

---

## 1. Goal

When the database server (PostgreSQL/SQL Server) or the DotNetCloud service goes offline:

1. **Core.Server** keeps the process alive, fails fast (HTTP 503) instead of hanging requests, reports the outage through `/health/ready`, and **automatically reconnects** when the DB returns.
2. **Module hosts** (Files, Chat, etc.) fail fast, stay running, and report `Degraded` via their health check — they must not crash-loop.
3. **Android** detects "server unreachable" (distinct from "device has internet"), shows a global offline banner, serves cached reads, queues writes, and auto-flushes the queue on recovery.
4. **SyncTray** classifies connectivity failures as `Offline` (gray tray icon), backs off exponentially instead of hammering, and auto-recovers.

> Design decisions (confirmed with user): server uses fail-fast 503 + auto-recover; module hosts included; Android gets a global offline banner + cache/queue.

---

## 2. Root Causes (evidence)

| #   | Component          | Problem                                                                                                                                                                                                                                                                                                        |
| --- | ------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | Core.Server        | No runtime DB health check registered. `DatabaseHealthCheck`/`AddDatabaseHealthCheck` exist in `DotNetCloud.Core.ServiceDefaults` but are never called from `Program.cs`. `/health/ready`'s predicate includes a `database` tag, but no check with that tag is registered → readiness never reflects DB state. |
| 2   | Core.Server        | EF retry is weak: `EnableRetryOnFailure(maxRetryCount: 3)` + `CommandTimeout(30)` in every DbContext. A dead DB makes each request block ~30s × retries; requests stack up → "lock up". No circuit breaker / 503 short-circuit.                                                                                |
| 3   | Core.Server        | Startup has retry (`InitializeDatabaseAsync`, 5 attempts) but **runtime** has no reconnect mechanism.                                                                                                                                                                                                          |
| 4   | Module hosts       | `Program.cs` registers DbContext with plain `UseNpgsql(connectionString)` / `UseSqlServer(connectionString)` — no retry, no command timeout.                                                                                                                                                                   |
| 5   | Android            | `ConnectivityMonitorService` only checks MAUI `Connectivity` (device internet). A reachable internet + down server = "online" → calls hang.                                                                                                                                                                    |
| 6   | Android + SyncTray | `OAuthHttpClientHandlerFactory.CreatePooledHandler()` sets no `ConnectTimeout` (default 100 s) and no per-request timeout → requests to a dead/unresponsive server hang.                                                                                                                                       |
| 7   | SyncTray           | `SyncEngine.SyncAsync` classifies every failure as `SyncState.Error`; `TrayViewModel.UpdateAggregateState` never maps `SyncState.Offline`/`TrayState.Offline` (both enums already exist but are unused). Periodic scan interval is fixed (up to 5 min when SSE is connected).                                  |

---

## 3. Implementation Order & Dependencies

```
Phase A  DbResiliencePolicy helper            (standalone)
Phase D  Client HTTP timeouts                  (standalone, needed by E & F)
Phase B  Server availability state + health + gate + reconnect  (depends on A)
Phase C  Module hosts                          (depends on A)
Phase E  Android                               (depends on D)
Phase F  SyncTray                              (depends on D)
Phase G  Docs + tracking                       (last)
```

Build + test after **each phase**. Do not commit until all phases build and targeted tests pass.

---

## 4. Phase A — Centralized DB Resilience Policy (Server)

### 4.1 New file: `src\Core\DotNetCloud.Core.Data\Extensions\DbResiliencePolicy.cs`

Create with this exact content:

```csharp
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Data.Extensions;

/// <summary>
/// Centralized database resilience configuration used by every EF Core DbContext
/// in the platform (Core, modules, CLI, server). Keeps retry counts, retry delays,
/// and command timeouts consistent so a transient database outage fails fast and
/// recovers automatically instead of hanging requests.
/// </summary>
public static class DbResiliencePolicy
{
    /// <summary>Maximum number of transient retry attempts per command.</summary>
    public const int MaxRetryCount = 5;

    /// <summary>Upper bound for the exponential retry delay.</summary>
    public static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(2);

    /// <summary>EF Core command timeout (per query/command execution).</summary>
    public static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Applies the standard provider-specific resilience settings to a DbContext
    /// options builder. Call this for every relational DbContext registration.
    /// </summary>
    /// <param name="options">The options builder being configured.</param>
    /// <param name="provider">Resolved <see cref="DatabaseProvider"/>.</param>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="migrationsAssembly">Optional migrations assembly (SQL Server only).</param>
    public static void Configure(
        DbContextOptionsBuilder options,
        DatabaseProvider provider,
        string connectionString,
        string? migrationsAssembly = null)
    {
        switch (provider)
        {
            case DatabaseProvider.PostgreSQL:
                options.UseNpgsql(connectionString, npgsql =>
                {
                    npgsql.EnableRetryOnFailure(
                        maxRetryCount: MaxRetryCount,
                        maxRetryDelay: MaxRetryDelay);
                    npgsql.CommandTimeout((int)CommandTimeout.TotalSeconds);
                });
                break;

            case DatabaseProvider.SqlServer:
                options.UseSqlServer(connectionString, sql =>
                {
                    sql.EnableRetryOnFailure(
                        maxRetryCount: MaxRetryCount,
                        maxRetryDelay: MaxRetryDelay);
                    sql.CommandTimeout((int)CommandTimeout.TotalSeconds);

                    if (!string.IsNullOrEmpty(migrationsAssembly))
                        sql.MigrationsAssembly(migrationsAssembly);
                });
                break;

            default:
                throw new ArgumentException($"Unsupported database provider: {provider}", nameof(provider));
        }
    }
}
```

> `DatabaseProvider` lives in the `DotNetCloud.Core.Data.Infrastructure` namespace. If the file does not resolve it, add `using DotNetCloud.Core.Data.Infrastructure;`.

### 4.2 Rewire existing registrations to use the helper

Delete the duplicated inline `EnableRetryOnFailure`/`CommandTimeout` blocks and call `DbResiliencePolicy.Configure(...)` instead, preserving each site's extra options.

**4.2.1 `src\Core\DotNetCloud.Core.Data\Extensions\DataServiceExtensions.cs`**

Current private method `ConfigureDbContext` (lines ~55–85). Replace its body with:

```csharp
    private static void ConfigureDbContext(
        DbContextOptionsBuilder options,
        DatabaseProvider provider,
        string connectionString)
    {
        DbResiliencePolicy.Configure(
            options,
            provider,
            connectionString,
            provider == DatabaseProvider.SqlServer ? "DotNetCloud.Core.Data.SqlServer" : null);

        // Common options
        options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        options.EnableDetailedErrors();
    }
```

(Remove the old `switch (provider) { ... }` entirely.)

**4.2.2 `src\Core\DotNetCloud.Core.Data\Extensions\ModuleDbContextConfiguration.cs`**

Replace the `switch` inside `Configure` with:

```csharp
    public static void Configure(
        DbContextOptionsBuilder options,
        DatabaseProvider provider,
        string connectionString,
        string? migrationsAssembly = null)
    {
        DbResiliencePolicy.Configure(options, provider, connectionString, migrationsAssembly);

        // Suppress pending model changes warning for modules that don't have
        // a dedicated SQL Server migrations assembly.
        options.ConfigureWarnings(warnings =>
        {
            warnings.Ignore(RelationalEventId.PendingModelChangesWarning);
        });
    }
```

(Keep the `using Microsoft.EntityFrameworkCore.Diagnostics;` for `RelationalEventId`.)

**4.2.3 `src\Core\DotNetCloud.Core.Data\Context\DefaultDbContextFactory.cs`**

Replace `ConfigureDbContextOptions`'s `switch` with:

```csharp
    private void ConfigureDbContextOptions(DbContextOptionsBuilder<CoreDbContext> options)
    {
        DbResiliencePolicy.Configure(
            options,
            _provider,
            _connectionString,
            _provider == DatabaseProvider.SqlServer ? "DotNetCloud.Core.Data.SqlServer" : null);
    }
```

**4.2.4 `src\Core\DotNetCloud.Core.Server\Program.cs` — private method `ConfigureModuleDbContext` (lines ~755–790)**

Replace its `switch` with:

```csharp
    private static void ConfigureModuleDbContext(
        DbContextOptionsBuilder options,
        DatabaseProvider provider,
        string connectionString)
    {
        DbResiliencePolicy.Configure(options, provider, connectionString);
    }
```

**4.2.5 `src\CLI\DotNetCloud.CLI\Infrastructure\ServiceProviderFactory.cs` (around lines 90–160)**

Find the `UseNpgsql`/`UseSqlServer` configuration lambda (used for both the main context and the per-module `AddDbContext` calls) and replace each with `DbResiliencePolicy.Configure(options, provider, connectionString, migrationsAssemblyOrNull)`. Preserve any existing `MigrationsAssembly` values by passing them as the 4th argument.

> If the CLI project does not reference `DotNetCloud.Core.Data.Extensions`, it already references `DotNetCloud.Core.Data` (it uses `CoreDbContext`), so the helper is available.

---

## 5. Phase B — Server: Availability State + Health Check + 503 Gate + Reconnect

All new server files go in `src\Core\DotNetCloud.Core.Server\`.

### 5.1 New file: `Services\DatabaseConnectivityState.cs`

```csharp
namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Holds the cached database availability determined by the background
/// <see cref="DatabaseReconnectMonitor"/>. Consumers (health check, middleware)
/// read this instead of probing the DB on every request.
/// </summary>
public sealed class DatabaseConnectivityState
{
    private volatile bool _isAvailable = true;

    /// <summary>Whether the database is currently reachable.</summary>
    public bool IsAvailable => _isAvailable;

    /// <summary>Raised when availability transitions between up and down.</summary>
    public event EventHandler? AvailabilityChanged;

    /// <summary>Updates availability and raises <see cref="AvailabilityChanged"/> on change.</summary>
    public void SetAvailable(bool available)
    {
        var previous = _isAvailable;
        _isAvailable = available;
        if (previous != available)
            AvailabilityChanged?.Invoke(this, EventArgs.Empty);
    }
}
```

### 5.2 New file: `Services\DbConnectionFactory.cs`

Implements the existing `DotNetCloud.Core.ServiceDefaults.HealthChecks.IDbConnectionFactory` interface.

```csharp
using System.Data.Common;
using DotNetCloud.Core.Data.Infrastructure;
using DotNetCloud.Core.ServiceDefaults.HealthChecks;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Creates raw ADO.NET connections for health probes using the configured
/// provider and connection string.
/// </summary>
public sealed class DbConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;
    private readonly DatabaseProvider _provider;

    public DbConnectionFactory(string connectionString, DatabaseProvider provider)
    {
        _connectionString = connectionString;
        _provider = provider;
    }

    /// <inheritdoc />
    public Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        DbConnection connection = _provider == DatabaseProvider.SqlServer
            ? new Microsoft.Data.SqlClient.SqlConnection(_connectionString)
            : new Npgsql.NpgsqlConnection(_connectionString);

        return Task.FromResult(connection);
    }
}
```

### 5.3 New file: `Services\DatabaseReconnectMonitor.cs`

```csharp
using DotNetCloud.Core.ServiceDefaults.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Background service that periodically probes the database and updates
/// <see cref="DatabaseConnectivityState"/>. This is the automatic reconnect
/// mechanism: when the DB comes back, the state flips to available and the
/// 503 gate re-opens without a process restart.
/// </summary>
public sealed class DatabaseReconnectMonitor : BackgroundService
{
    private static readonly TimeSpan DownPollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan UpPollInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly DatabaseConnectivityState _state;
    private readonly ILogger<DatabaseReconnectMonitor> _logger;

    public DatabaseReconnectMonitor(
        IDbConnectionFactory connectionFactory,
        DatabaseConnectivityState state,
        ILogger<DatabaseReconnectMonitor> logger)
    {
        _connectionFactory = connectionFactory;
        _state = state;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var available = await ProbeAsync(stoppingToken).ConfigureAwait(false);
            var was = _state.IsAvailable;
            _state.SetAvailable(available);

            if (was && !available)
                _logger.LogCritical("Database connectivity lost. Requests requiring the database will return 503 until it recovers.");
            else if (!was && available)
                _logger.LogInformation("Database connectivity restored. Resuming normal operation.");

            await Task.Delay(available ? UpPollInterval : DownPollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task<bool> ProbeAsync(CancellationToken stoppingToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        cts.CancelAfter(ProbeTimeout);

        try
        {
            await using var connection = await _connectionFactory.CreateConnectionAsync(cts.Token).ConfigureAwait(false);
            await connection.OpenAsync(cts.Token).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cts.Token).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Database availability probe failed.");
            return false;
        }
    }
}
```

### 5.4 New file: `HealthChecks\DatabaseAvailabilityHealthCheck.cs`

```csharp
using DotNetCloud.Core.Server.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DotNetCloud.Core.Server.HealthChecks;

/// <summary>
/// Health check that reports the cached database availability (updated by
/// <see cref="DatabaseReconnectMonitor"/>). Cheap — no live DB query per probe.
/// Registered with the "database" tag so it is included in /health and /health/ready.
/// </summary>
internal sealed class DatabaseAvailabilityHealthCheck : IHealthCheck
{
    private readonly DatabaseConnectivityState _state;

    public DatabaseAvailabilityHealthCheck(DatabaseConnectivityState state)
    {
        _state = state;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var result = _state.IsAvailable
            ? HealthCheckResult.Healthy("Database is reachable.")
            : HealthCheckResult.Unhealthy("Database is unreachable.");

        return Task.FromResult(result);
    }
}
```

### 5.5 New file: `Middleware\DatabaseUnavailableMiddleware.cs`

```csharp
using System.Text.Json;
using DotNetCloud.Core.Server.Services;

namespace DotNetCloud.Core.Server.Middleware;

/// <summary>
/// Returns HTTP 503 quickly for database-dependent requests while the database
/// is unavailable, instead of letting them block on connection timeouts.
/// Health, metrics, root-CA and static asset paths are served normally.
/// </summary>
public sealed class DatabaseUnavailableMiddleware
{
    private readonly RequestDelegate _next;
    private readonly DatabaseConnectivityState _state;

    private static readonly HashSet<string> AllowlistedPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/health",      // /health, /health/live, /health/ready
        "/metrics",     // Prometheus scrape endpoint
        "/root-ca.crt", // self-signed CA download
        "/_framework",  // Blazor static assets
        "/favicon.ico",
    };

    public DatabaseUnavailableMiddleware(
        RequestDelegate next,
        DatabaseConnectivityState state)
    {
        _next = next;
        _state = state;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_state.IsAvailable && RequiresDatabase(context.Request.Path))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/json";
            context.Response.Headers.RetryAfter = "5";

            var payload = JsonSerializer.Serialize(new
            {
                success = false,
                code = "DATABASE_UNAVAILABLE",
                message = "The server database is temporarily unavailable. Please try again shortly."
            });

            await context.Response.WriteAsync(payload, context.RequestAborted);
            return;
        }

        await _next(context);
    }

    private static bool RequiresDatabase(PathString path)
    {
        var value = path.Value ?? string.Empty;
        foreach (var prefix in AllowlistedPrefixes)
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }
}
```

### 5.6 Wire everything in `src\Core\DotNetCloud.Core.Server\Program.cs`

**5.6.1 In `ConfigureServices`, immediately after the `builder.Services.AddDotNetCloudDbContext(connectionString, provider);` line (around line 305), add:**

```csharp
        // Database availability tracking (health check + 503 gate + auto-reconnect).
        builder.Services.AddSingleton<IDbConnectionFactory>(
            new DbConnectionFactory(connectionString, provider));
        builder.Services.AddSingleton<DatabaseConnectivityState>();
        builder.Services.AddHostedService<DatabaseReconnectMonitor>();
        builder.Services.AddHealthChecks()
            .Add(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckRegistration(
                "database",
                sp => new DatabaseAvailabilityHealthCheck(
                    sp.GetRequiredService<DatabaseConnectivityState>()),
                failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                tags: ["database"]));
```

`IDbConnectionFactory` is `DotNetCloud.Core.ServiceDefaults.HealthChecks.IDbConnectionFactory` — `Program.cs` already imports `DotNetCloud.Core.ServiceDefaults.HealthChecks`.

**5.6.2 In `ConfigurePipeline`, insert the gate right after `app.UseAntiforgery();` and before `app.MapOpenIddictEndpoints();` (around lines 870–875):**

```csharp
        app.UseAntiforgery();

        // Fail fast (503) when the database is unavailable instead of hanging
        // requests. Health/metrics/static assets were mapped earlier and stay reachable.
        app.UseMiddleware<DatabaseUnavailableMiddleware>();

        // Map OpenIddict endpoints
        app.MapOpenIddictEndpoints();
```

> Why this position: health checks (`MapDotNetCloudHealthChecks`), root-CA, metrics, OpenAPI, and static assets are mapped **before** this point, so those endpoints never reach the gate. OpenIddict/controllers/SignalR/gRPC/Blazor/module proxies are after it and get 503 during an outage.

> Known limitation (acceptable): the internal module gRPC branch (`MapWhen` for `application/grpc`) runs before the gate. Internal gRPC callers (module hosts) already receive errors and should surface them as `Degraded`; this does not affect client-facing HTTP.

---

## 6. Phase C — Module Hosts: Use the Resilience Policy + DB-Aware Health

### 6.1 Update every module host `Program.cs`

For each host under `src\Modules\<Module>\DotNetCloud.Modules.<Module>.Host\Program.cs`, find the `AddDbContext<XxxDbContext>(options => { if provider == PostgreSql ... else ... })` block and replace the inline provider branching with a call to `DbResiliencePolicy.Configure`.

**Reference pattern (Files module — `src\Modules\Files\DotNetCloud.Modules.Files.Host\Program.cs`, around lines 140–155). Replace:**

```csharp
builder.Services.AddDbContext<FilesDbContext>(options =>
{
    if (string.Equals(dbProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase))
        options.UseNpgsql(connectionString);
    else
        options.UseSqlServer(connectionString, sql => sql.MigrationsAssembly("DotNetCloud.Modules.Files.Data.SqlServer"));
});
```

**with:**

```csharp
var resolvedProvider = ResolveDatabaseProvider(dbProvider);
builder.Services.AddDbContext<FilesDbContext>(options =>
    DbResiliencePolicy.Configure(
        options,
        resolvedProvider,
        connectionString,
        resolvedProvider == DatabaseProvider.SqlServer ? "DotNetCloud.Modules.Files.Data.SqlServer" : null));
```

Add a local static helper at the bottom of the file (above `public partial class Program` or as a top-level local function):

```csharp
static DatabaseProvider ResolveDatabaseProvider(string? configured) =>
    DatabaseProviderConfiguration.TryParseConfiguredProvider(configured ?? string.Empty, out var provider)
        ? provider
        : throw new InvalidOperationException($"Unsupported database provider '{configured}'.");
```

Add these usings if not already present:

```csharp
using DotNetCloud.Core.Data.Extensions;
using DotNetCloud.Core.Data.Infrastructure;
```

**Hosts to update (enumerate with `Get-ChildItem src\Modules -Recurse -Filter Program.cs | Where-Object { $_.FullName -match '\\.Host\\Program\\.cs$' }`):**

Files, Chat, Calendar, Notes, Music, Photos, Video, AI, Bookmarks, Email, Tracks, Search, Contacts, About — approximately 14 hosts. For each, the migrations assembly name follows the existing `UseSqlServer(..., sql => sql.MigrationsAssembly("DotNetCloud.Modules.Xxx.Data.SqlServer"))` value. Do not invent migration assembly names; keep whatever is currently passed (or `null` if none).

### 6.2 Make module health checks DB-aware and exception-safe

For each module host's health check class (e.g. `FilesHealthCheck` registered as `"files_module"` in Files Host):

- Ensure the `CheckHealthAsync` method wraps its work in `try/catch` and returns `HealthCheckResult.Unhealthy(...)` (or `Degraded`) on exception — **never rethrows**. A throwing health check can surface as an unhandled error and crash the module process, which triggers supervisor restart loops.
- If the health check does not currently touch the DB, add a cheap `SELECT 1` probe (reuse `DbResiliencePolicy`-configured DbContext or a raw connection via the same connection string). A failed DB probe → `Unhealthy`.

The `ProcessSupervisor` already treats a failed module health check as `Degraded` (it only restarts on process exit), so this keeps modules alive during a DB outage.

### 6.3 Verify no module host hard-exits on DB-down at startup

Module hosts must not call `Environment.Exit`/`throw` out of `Main` when the DB is unreachable at startup (Files does not — it registers services then starts). If any host performs eager DB initialization that throws, wrap it in the same retry/backoff pattern as Core.Server's `InitializeDatabaseAsync` **and continue running in degraded mode** (start the HTTP/gRPC listener anyway).

---

## 7. Phase D — Shared Client HTTP Hardening (Fail Fast)

### 7.1 `src\Clients\DotNetCloud.Client.Core\Auth\OAuthHttpClientHandlerFactory.cs`

In `CreatePooledHandler()`, add `ConnectTimeout`:

```csharp
        return new SocketsHttpHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            MaxConnectionsPerServer = 16,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            EnableMultipleHttp2Connections = true,
            KeepAlivePingDelay = TimeSpan.FromSeconds(30),
            SslOptions = CreatePermissiveSslOptions(),
        };
```

### 7.2 New file: `src\Clients\DotNetCloud.Client.Core\Api\TimeoutHandler.cs`

```csharp
namespace DotNetCloud.Client.Core.Api;

/// <summary>
/// Enforces a "time-to-first-byte" timeout around the inner handler chain.
/// Because callers use <see cref="HttpCompletionOption.ResponseHeadersRead"/>,
/// the timeout covers connection + headers only; streaming upload/download
/// bodies are NOT cancelled, so large transfers are unaffected.
/// </summary>
public sealed class TimeoutHandler : DelegatingHandler
{
    private readonly TimeSpan _timeout;

    /// <summary>Creates a handler with the given timeout (default 30 s).</summary>
    public TimeoutHandler(TimeSpan? timeout = null)
    {
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_timeout);

        try
        {
            return await base.SendAsync(request, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TaskCanceledException(
                $"The request timed out after {_timeout.TotalSeconds:0}s waiting for the server to respond.");
        }
    }
}
```

### 7.3 Apply the handler

**7.3.1 `src\Clients\DotNetCloud.Client.Core\ClientCoreServiceExtensions.cs`** — in `AddDotNetCloudClientCore`:

Register the handler and add it to the `DotNetCloudApiClient` and `IOAuth2Service` typed clients. Order matters: the FIRST `AddHttpMessageHandler` becomes the OUTERMOST handler.

```csharp
        services.AddTransient<TimeoutHandler>();
        services.AddTransient<CorrelationIdHandler>();
        services.AddHttpClient<DotNetCloudApiClient>()
            .AddHttpMessageHandler<TimeoutHandler>()
            .ConfigurePrimaryHttpMessageHandler(OAuthHttpClientHandlerFactory.CreatePooledHandler)
            .AddHttpMessageHandler<CorrelationIdHandler>();
        services.AddTransient<IDotNetCloudApiClient, DotNetCloudApiClient>();

        services.AddHttpClient<IOAuth2Service, OAuth2Service>()
            .AddHttpMessageHandler<TimeoutHandler>()
            .ConfigurePrimaryHttpMessageHandler(OAuthHttpClientHandlerFactory.CreateHandler);
```

**7.3.2 `src\Clients\DotNetCloud.Client.Core\SyncContextManagerExtensions.cs`** — the `"DotNetCloudSync"` named client (used by SyncTray's per-context API clients). Use a 60 s time-to-first-byte so large folder-tree/reconcile calls aren't cut off:

```csharp
        services.AddTransient<TimeoutHandler>();
        services.AddTransient<CorrelationIdHandler>();
        // ... existing DeviceIdentityHandler registration unchanged ...
        services.AddHttpClient("DotNetCloudSync")
            .AddHttpMessageHandler(sp => new TimeoutHandler(TimeSpan.FromSeconds(60)))
            .ConfigurePrimaryHttpMessageHandler(OAuthHttpClientHandlerFactory.CreateHandler)
            .AddHttpMessageHandler<CorrelationIdHandler>()
            .AddHttpMessageHandler<DeviceIdentityHandler>();
```

**7.3.3 `src\Clients\DotNetCloud.Client.Core\Sync\SyncContextManager.cs`** — the custom throttled pipeline in `CreateEngine` (bandwidth-limit path, around lines 730–760). Wrap the existing `ThrottledHttpHandler` chain with a `TimeoutHandler`:

```csharp
            var throttledHandler = new ThrottledHttpHandler(uploadBytes, downloadBytes)
            {
                InnerHandler = new DeviceIdentityHandler(...)
                {
                    InnerHandler = new CorrelationIdHandler(...)
                    {
                        InnerHandler = OAuthHttpClientHandlerFactory.CreateHandler()
                    }
                }
            };
            httpClient = new HttpClient(new TimeoutHandler(TimeSpan.FromSeconds(60))
            {
                InnerHandler = throttledHandler
            })
            {
                BaseAddress = new Uri(registration.ServerBaseUrl.TrimEnd('/') + '/')
            };
```

> Do NOT set `HttpClient.Timeout` anywhere — it would abort long streaming downloads. Only the time-to-first-byte handler is safe for file transfer clients.

### 7.4 `src\Clients\DotNetCloud.Client.Core\Api\DotNetCloudApiClient.cs` — retry tweaks

In `SendWithRetryAsync`, the current catch is:

```csharp
            catch (HttpRequestException ex) when (attempt < MaxRetries)
            {
                _logger.LogWarning(ex, "HTTP request failed (attempt {Attempt}/{Max}), retrying.", attempt + 1, MaxRetries);
                await DelayAsync(attempt, null, cancellationToken);
                continue;
            }
```

Replace with (adds timeout-as-transient and respects caller cancellation):

```csharp
            catch (Exception ex) when (attempt < MaxRetries
                && !cancellationToken.IsCancellationRequested
                && (ex is HttpRequestException or TaskCanceledException))
            {
                _logger.LogWarning(ex, "HTTP request failed (attempt {Attempt}/{Max}), retrying.", attempt + 1, MaxRetries);
                await DelayAsync(attempt, null, cancellationToken);
                continue;
            }
```

The existing 5xx retry already handles the server's 503 (it retries 3× with short backoff then returns the 503, which callers surface as an error). Optionally honor a `Retry-After` header on 503: if `response.Headers.RetryAfter` is present, use its delay in `DelayAsync` — this is an enhancement, not required.

---

## 8. Phase E — Android: Server Reachability + Global Banner + Queue Flush

### 8.1 New file: `src\Clients\DotNetCloud.Client.Android\Services\IServerReachabilityService.cs`

```csharp
namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Tracks whether the configured DotNetCloud server is reachable, distinct from
/// device internet connectivity (a phone can have internet while the server is down).
/// </summary>
public interface IServerReachabilityService
{
    /// <summary>Whether the active server responded to a liveness ping recently.</summary>
    bool IsServerOnline { get; }

    /// <summary>Raised whenever online status transitions.</summary>
    event Action? AvailabilityChanged;

    /// <summary>Starts periodic probing. Safe to call multiple times.</summary>
    void Start();
}
```

### 8.2 New file: `src\Clients\DotNetCloud.Client.Android\Services\ServerReachabilityService.cs`

```csharp
using DotNetCloud.Client.Core.Auth;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Probes the active server's <c>/health/live</c> endpoint periodically.
/// Combines device connectivity and server reachability into a single signal.
/// </summary>
internal sealed class ServerReachabilityService : IServerReachabilityService, IDisposable
{
    private static readonly TimeSpan OnlineInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan OfflineInterval = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    private readonly IServerConnectionStore _serverStore;
    private readonly IConnectivityMonitor _connectivity;
    private readonly ILogger<ServerReachabilityService> _logger;
    private readonly HttpClient _http;

    private bool _isServerOnline;
    private bool _started;
    private CancellationTokenSource? _cts;

    public ServerReachabilityService(
        IServerConnectionStore serverStore,
        IConnectivityMonitor connectivity,
        ILogger<ServerReachabilityService> logger)
    {
        _serverStore = serverStore;
        _connectivity = connectivity;
        _logger = logger;

        // Permissive TLS for self-signed local/private hosts.
        _http = new HttpClient(OAuthHttpClientHandlerFactory.CreateHandler());
        _http.Timeout = ProbeTimeout;
    }

    public bool IsServerOnline => _isServerOnline;

    public event Action? AvailabilityChanged;

    public void Start()
    {
        if (_started)
            return;
        _started = true;
        _cts = new CancellationTokenSource();

        // Immediately reflect device offline.
        if (!_connectivity.IsOnline)
        {
            _isServerOnline = false;
            AvailabilityChanged?.Invoke();
        }

        _ = Task.Run(() => RunAsync(_cts.Token));
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var was = _isServerOnline;

            if (!_connectivity.IsOnline)
            {
                Set(false);
            }
            else
            {
                Set(await ProbeAsync(ct).ConfigureAwait(false));
            }

            var interval = _isServerOnline ? OnlineInterval : OfflineInterval;
            try
            {
                await Task.Delay(interval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task<bool> ProbeAsync(CancellationToken ct)
    {
        var active = _serverStore.GetActive();
        if (active is null)
            return false;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(ProbeTimeout);

        try
        {
            var url = $"{active.ServerBaseUrl.TrimEnd('/')}/health/live";
            using var response = await _http.GetAsync(url, cts.Token).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Server reachability probe failed for {ServerUrl}.", active.ServerBaseUrl);
            return false;
        }
    }

    private void Set(bool online)
    {
        if (_isServerOnline == online)
            return;
        _isServerOnline = online;
        _logger.LogInformation("Server reachability changed: {State}.", online ? "online" : "offline");
        AvailabilityChanged?.Invoke();
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _http.Dispose();
    }
}
```

### 8.3 Global offline banner

**8.3.1 New file: `src\Clients\DotNetCloud.Client.Android\ViewModels\ConnectivityViewModel.cs`**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using DotNetCloud.Client.Android.Services;

namespace DotNetCloud.Client.Android.ViewModels;

/// <summary>
/// Drives the global "server offline" banner.
/// </summary>
public partial class ConnectivityViewModel : ObservableObject
{
    private readonly IServerReachabilityService _reachability;

    [ObservableProperty]
    private bool _isServerOffline;

    public ConnectivityViewModel(IServerReachabilityService reachability)
    {
        _reachability = reachability;
        IsServerOffline = !reachability.IsServerOnline;
        _reachability.AvailabilityChanged += OnAvailabilityChanged;
    }

    private void OnAvailabilityChanged() =>
        IsServerOffline = !_reachability.IsServerOnline;
}
```

**8.3.2 New file: `src\Clients\DotNetCloud.Client.Android\Views\ConnectivityBannerView.xaml`**

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentView
    x:Class="DotNetCloud.Client.Android.Views.ConnectivityBannerView"
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
    IsVisible="{Binding IsServerOffline}"
    InputTransparent="True">

    <Border Background="#B91C1C"
            StrokeThickness="0"
            Padding="12,6">
        <Label Text="Can't reach server — showing cached data. Changes will be queued."
               TextColor="#FFFFFF"
               FontSize="12"
               HorizontalOptions="Center" />
    </Border>

</ContentView>
```

**8.3.3 New code-behind: `src\Clients\DotNetCloud.Client.Android\Views\ConnectivityBannerView.xaml.cs`**

```csharp
namespace DotNetCloud.Client.Android.Views;

public partial class ConnectivityBannerView : ContentView
{
    public ConnectivityBannerView()
    {
        InitializeComponent();
    }
}
```

**8.3.4 Overlay the banner over the Shell in `src\Clients\DotNetCloud.Client.Android\App.xaml.cs`**

Modify `CreateWindow` to wrap the `AppShell` in a root `Grid` with the banner pinned to the top:

```csharp
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var shell = new AppShell();

        var connectivity = Ioc.Default.GetService<ConnectivityViewModel>();
        var banner = new Views.ConnectivityBannerView
        {
            BindingContext = connectivity,
            VerticalOptions = LayoutOptions.Start,
            HorizontalOptions = LayoutOptions.Fill,
        };

        var root = new Grid
        {
            Children = { shell, banner }
        };

        var window = new Window(root);

        // ... keep the existing window.Destroying handler unchanged ...

        return window;
    }
```

> `Shell.Current` continues to resolve because `AppShell` is still in the visual tree. If (unexpectedly) navigation breaks, fall back to adding `ConnectivityBannerView` inside `MainPage.xaml` as a top overlay instead of wrapping the Shell.

Add the required using to `App.xaml.cs`:

```csharp
using DotNetCloud.Client.Android.ViewModels;
```

### 8.4 Register services in `src\Clients\DotNetCloud.Client.Android\MauiProgram.cs`

Add under the "Offline queue / sync" section:

```csharp
        builder.Services.AddSingleton<IServerReachabilityService, ServerReachabilityService>();
        builder.Services.AddSingleton<ConnectivityViewModel>();
        builder.Services.AddTransient<TimeoutHandler>();
```

Add the `TimeoutHandler` to every typed HttpClient **as the first `AddHttpMessageHandler` call** (so it is outermost and covers the auth-refresh handler too). Example for the chat client — apply the same pattern to `IFileRestClient`, `IMusicRestClient`, `IAlbumArtCache`, `IThumbnailCache`, `ICalendarRestClient`, `INotesRestClient`, `IClientUpdateService`:

```csharp
        builder.Services.AddHttpClient<IChatRestClient, HttpChatRestClient>()
            .AddHttpMessageHandler<TimeoutHandler>()
            .AddHttpMessageHandler<AuthenticatedHttpClientHandler>()
            .ConfigurePrimaryHttpMessageHandler(DotNetCloud.Client.Core.Auth.OAuthHttpClientHandlerFactory.CreateHandler);
```

(Current order is `AddHttpMessageHandler<AuthenticatedHttpClientHandler>()` first — move `TimeoutHandler` before it.)

### 8.5 Start reachability monitoring from `App`

In `src\Clients\DotNetCloud.Client.Android\App.xaml.cs`, `OnStart()`, after `await _offlineSync.StartAsync();`, add:

```csharp
        try
        {
            Ioc.Default.GetService<IServerReachabilityService>()?.Start();
        }
        catch (Exception ex)
        {
            Log.Warn("DotNetCloud", $"Reachability start failed: {ex.Message}");
        }
```

Add `using DotNetCloud.Client.Android.Services;` if not present.

### 8.6 Flush the offline queue on server recovery + periodic retry

In `src\Clients\DotNetCloud.Client.Android\Services\OfflineSyncService.cs`:

- Inject `IServerReachabilityService` into the constructor.
- In `StartAsync`, subscribe `_reachability.AvailabilityChanged += OnAvailabilityChanged;`.
- In `OnAvailabilityChanged`, if `_reachability.IsServerOnline`, call `_ = FlushAllAsync();`.
- Add a periodic timer (e.g. `PeriodicTimer` or `Timer` every 30 s) that calls `FlushAllAsync()` only when `await _queue.CountAsync() > 0` and `_reachability.IsServerOnline`. Stop the timer on `Dispose` (add `IDisposable` implementation if not present).

This ensures queued chat/notes/calendar operations flush automatically after the server comes back, even without a SignalR reconnect.

### 8.7 Harden view models against connectivity errors

In each view model's load/refresh method, ensure the call is wrapped in try/catch, the loading spinner/flag is reset in `finally`, the error is surfaced with `ApiExceptionHelper.GetUserFriendlyMessage(ex)` (already exists), and cached data is left intact. Target files:

- `src\Clients\DotNetCloud.Client.Android\ViewModels\ChannelListViewModel.cs`
- `src\Clients\DotNetCloud.Client.Android\ViewModels\MessageListViewModel.cs`
- `src\Clients\DotNetCloud.Client.Android\ViewModels\FileBrowserViewModel.cs`
- `src\Clients\DotNetCloud.Client.Android\ViewModels\CalendarViewModel.cs`
- `src\Clients\DotNetCloud.Client.Android\ViewModels\NotesViewModel.cs`
- `src\Clients\DotNetCloud.Client.Android\ViewModels\MusicViewModel.cs`

Do not auto-navigate to Login on these errors — only the `AuthenticatedHttpClientHandler` does that for genuine 401s.

### 8.8 SignalR manual reconnect

In `src\Clients\DotNetCloud.Client.Android\Chat\SignalRChatClient.cs`, the `_hub.Closed` handler currently only logs. After the built-in `WithAutomaticReconnect([0,2,5,15])` retries are exhausted, schedule a manual reconnect with backoff, gated by `IServerReachabilityService.IsServerOnline`:

```csharp
        _hub.Closed += async error =>
        {
            _logger.LogWarning(error, "SignalR connection closed. Scheduling reconnect.");
            await ScheduleReconnectAsync();
        };
```

Add a helper (private, in the same class):

```csharp
    private async Task ScheduleReconnectAsync()
    {
        if (_reconnecting) return;
        _reconnecting = true;
        try
        {
            var delay = TimeSpan.FromSeconds(5);
            while (serverBaseUrl is not null && !string.IsNullOrEmpty(serverBaseUrl))
            {
                try
                {
                    await Task.Delay(delay).ConfigureAwait(false);
                    if (_reachability is null || _reachability.IsServerOnline)
                    {
                        await ConnectAsync(serverBaseUrl, token: default).ConfigureAwait(false);
                        _logger.LogInformation("SignalR reconnected after retry.");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "SignalR reconnect attempt failed; retrying.");
                }
                delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, TimeSpan.FromMinutes(2).Ticks));
            }
        }
        finally
        {
            _reconnecting = false;
        }
    }
```

Store the last-used `serverBaseUrl` and the access token in fields when `ConnectAsync(serverBaseUrl, accessToken, ...)` is first called so the reconnect can reuse them. Add `IServerReachabilityService _reachability` via constructor injection (registering the singleton already done in step 8.4).

> If `SignalRChatClient` is constructed before DI is fully available, resolve `IServerReachabilityService` lazily from `Ioc.Default` inside the loop instead.

---

## 9. Phase F — SyncTray: Offline Classification + Backoff + Tray State

### 9.1 `src\Clients\DotNetCloud.Client.Core\Sync\SyncEngine.cs` — classify offline

In `SyncAsync`, the current catch block (around lines 370–407):

```csharp
        catch (OperationCanceledException)
        {
            syncTimer.Stop();
            _logger.LogInformation("Sync cancelled for context {ContextId}.", context.Id);
            SetState(SyncState.Idle, context);
        }
        catch (Exception ex)
        {
            syncTimer.Stop();
            if (IsDiskFullException(ex))
            {
                ...
            }
            else
            {
                _logger.LogError(ex, "Sync error for context {ContextId}.", context.Id);
                _lastError = ex.Message;
            }

            SetState(SyncState.Error, context);
        }
```

Change to:

```csharp
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            syncTimer.Stop();
            _logger.LogInformation("Sync cancelled for context {ContextId}.", context.Id);
            SetState(SyncState.Idle, context);
        }
        catch (Exception ex)
        {
            syncTimer.Stop();
            if (IsDiskFullException(ex))
            {
                _logger.LogError(ex,
                    "Disk full while syncing context {ContextId}. Pausing sync until user intervention.",
                    context.Id);

                _paused = true;
                if (_watcher is not null)
                    _watcher.EnableRaisingEvents = false;

                _lastError = "Disk full: local storage is out of space. Free disk space, then resume sync.";
                SetState(SyncState.Error, context);
            }
            else if (IsConnectivityException(ex))
            {
                _logger.LogWarning(ex, "Server unreachable while syncing context {ContextId}.", context.Id);
                _lastError = "Server unreachable. Retrying automatically.";
                SetState(SyncState.Offline, context);
            }
            else
            {
                _logger.LogError(ex, "Sync error for context {ContextId}.", context.Id);
                _lastError = ex.Message;
                SetState(SyncState.Error, context);
            }
        }
```

Add the helper method (near `IsDiskFullException`):

```csharp
    private static bool IsConnectivityException(Exception ex)
    {
        // OperationCanceledException here is a timeout (user cancellation is
        // filtered in the catch above), so treat it as a connectivity failure.
        return ex is HttpRequestException
            or TaskCanceledException
            or System.Net.Sockets.SocketException
            or IOException io when io.InnerException is System.Net.Sockets.SocketException;
    }
```

> `TaskCanceledException` derives from `OperationCanceledException`; the `when (cancellationToken.IsCancellationRequested)` filter ensures genuine cancellations still go to `Idle`, while timeouts fall through to `Offline`.

### 9.2 `SyncEngine.cs` — exponential backoff in `RunPeriodicScanAsync`

Current method (around lines 2979–3010). Change to add backoff state:

```csharp
    private async Task RunPeriodicScanAsync(SyncContext context, CancellationToken cancellationToken)
    {
        var interval = _pollingFallback ? TimeSpan.FromSeconds(30) : context.FullScanInterval;
        var offlineBackoff = TimeSpan.FromSeconds(15);
        const TimeSpan maxOfflineBackoff = TimeSpan.FromMinutes(5);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, cancellationToken);

                if (_streamListener?.IsConnected == true)
                    interval = TimeSpan.FromMinutes(5);
                else
                    interval = _pollingFallback ? TimeSpan.FromSeconds(30) : context.FullScanInterval;

                _logger.LogDebug("Periodic full scan triggered for context {ContextId}.", context.Id);
                await SyncAsync(context, cancellationToken);

                // On success, reset the offline backoff window.
                offlineBackoff = TimeSpan.FromSeconds(15);

                // If the pass ended offline, retry sooner, doubling the delay up to a cap.
                if (_state == SyncState.Offline)
                {
                    interval = offlineBackoff;
                    offlineBackoff = TimeSpan.FromTicks(
                        Math.Min(offlineBackoff.Ticks * 2, maxOfflineBackoff.Ticks));
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Periodic scan failed for context {ContextId}.", context.Id);
            }
        }
    }
```

### 9.3 `src\Clients\DotNetCloud.Client.SyncTray\ViewModels\TrayViewModel.cs` — map Offline state

`OnSyncProgress` already copies `e.Status.State.ToString()` into `vm.State` (so `"Offline"` flows in automatically). Only `UpdateAggregateState` and the tooltip need changes.

In `UpdateAggregateState` (around lines 920–940), change:

```csharp
        bool hasError = _accountList.Any(a => a.State == "Error");
        bool isSyncing = _accountList.Any(a => a.State == "Syncing");
        bool allPaused = _accountList.All(a => a.State == "Paused");
        bool hasConflicts = _conflictCount > 0;

        OverallState = hasError ? TrayState.Error
            : hasConflicts ? TrayState.Conflict
            : isSyncing ? TrayState.Syncing
            : allPaused ? TrayState.Paused
            : TrayState.Idle;
```

to:

```csharp
        bool hasError = _accountList.Any(a => a.State == "Error");
        bool hasOffline = _accountList.Any(a => a.State == "Offline");
        bool isSyncing = _accountList.Any(a => a.State == "Syncing");
        bool allPaused = _accountList.All(a => a.State == "Paused");
        bool hasConflicts = _conflictCount > 0;

        OverallState = hasError ? TrayState.Error
            : hasOffline ? TrayState.Offline
            : hasConflicts ? TrayState.Conflict
            : isSyncing ? TrayState.Syncing
            : allPaused ? TrayState.Paused
            : TrayState.Idle;
```

In the tooltip switch (immediately below), add an `Offline` case:

```csharp
            TrayState.Offline => "DotNetCloud Sync \u2014 server unreachable, retrying automatically",
```

The tray icon rendering already has a gray `TrayState.Offline` icon (see `TrayIconManager.CreateStatusIcon`), so no icon change is required.

---

## 10. Phase G — Documentation & Tracking

After all phases build and tests pass:

1. Update `docs\IMPLEMENTATION_CHECKLIST.md` — mark completed items with `✓` and pending with `☐` (targeted edits only, preserve Git history).
2. Update `docs\MASTER_PROJECT_PLAN.md` — Quick Status Summary table + any relevant step sections (targeted edits only).
3. Keep this file as the canonical implementation record.

---

## 11. Verification

### 11.1 Build & test (after each phase)

```powershell
dotnet build
dotnet test tests\DotNetCloud.Core.Server.Tests\
dotnet test tests\DotNetCloud.Client.Android.Tests\
```

### 11.2 Server outage simulation (Core.Server)

1. With the DB running, start Core.Server.
2. Stop the database (e.g. `docker compose stop db` or stop the Postgres/SQL Server service).
3. Assert:
   - `Invoke-RestMethod http://localhost:5080/health/live` → `Healthy` (process alive).
   - `Invoke-RestMethod http://localhost:5080/health/ready` → `Unhealthy` with an `entries.database.status` of `Unhealthy`.
   - An API call that hits the DB (e.g. `GET /api/v1/files/quota` with auth) returns HTTP **503** with body `{"success":false,"code":"DATABASE_UNAVAILABLE",...}` in under ~2 s (not a hang).
4. Start the database again.
5. Assert within ~10 s (one reconnect poll interval):
   - `/health/ready` → `Healthy`.
   - The same API call → `200` again — **without restarting the service**.

### 11.3 Module host simulation

With the DB down, `dotnetcloud-module files` must keep running (process stays up) and its health check must report `Degraded`/`Unhealthy` via the supervisor aggregate. After DB recovery it returns to healthy — no manual restart, no crash-loop.

### 11.4 SyncTray simulation

1. Run SyncTray with an active account and the server up → tray is green/idle.
2. Stop the DotNetCloud service.
3. Assert: within one backoff interval (≤ ~30 s) the tray turns **gray** with tooltip "server unreachable, retrying automatically"; "Sync now" returns quickly with a toast (no multi-minute hang).
4. Start the service: tray returns to idle/syncing automatically within the backoff interval.

### 11.5 Android simulation (emulator or device via adb)

1. Deploy a debug arm64 build (see repo memory for exact adb/build commands).
2. Stop the DotNetCloud service (keep device internet on).
3. Assert: the global red banner appears; opening chat shows cached messages; sending a message queues it (existing "queued" banner).
4. Start the service: banner clears automatically (≤ ~20 s), queued messages flush.

---

## 12. Summary Checklist

- ✓ Phase A — `DbResiliencePolicy` + rewire all DbContext registrations.
- ✓ Phase B — `DatabaseConnectivityState`, `DbConnectionFactory`, `DatabaseReconnectMonitor`, `DatabaseAvailabilityHealthCheck`, `DatabaseUnavailableMiddleware` + Program.cs wiring.
- ✓ Phase C — All module hosts use `DbResiliencePolicy`; module health checks DB-aware/exception-safe.
- ✓ Phase D — `ConnectTimeout`, `TimeoutHandler`, handler wiring, `SendWithRetryAsync` tweaks.
- ✓ Phase E — `ServerReachabilityService`, `ConnectivityViewModel`, banner overlay, MauiProgram registrations, queue flush triggers, SignalR reconnect, view-model hardening.
- ✓ Phase F — SyncEngine offline classification + backoff; TrayViewModel offline mapping/tooltip.
- ✓ Phase G — Update `IMPLEMENTATION_CHECKLIST.md` and `MASTER_PROJECT_PLAN.md`.
- ✓ `dotnet build` passes for Core.Data, Core.Server, CLI, Client.Core, SyncTray, all module hosts, and Android (arm64); targeted tests pass (Files 760, Chat 1311, SyncTray 130, Client.Core 293/296 pass — 3 pre-existing environmental failures).
- ✓ Manual outage simulations (server 503/recover §11.2, module host degraded §11.3, SyncTray gray icon §11.4, Android banner/queue flush §11.5) — ALL PASSED on 2026-08-24. Android §11.5 required two client fixes (committed): receiver/service `Name` registration (cold-start `ClassNotFoundException`) and a platform-level banner overlay (the MAUI Shell-in-Grid wrap crashes launch). See `CLIENT_SERVER_MEDIATION_HANDOFF.md` archived Android handoff for evidence.
