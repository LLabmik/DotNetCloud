# Notification Bell — Read-State Fix + Real-Time Auto-Check Plan

**Date:** 2026-08-18
**Branch:** `fix/bell-notifications`
**Scope:** Fix 9 silent "mark-as-read / update" persistence bugs caused by a global `NoTracking` EF Core setting, then add real-time notification auto-check to the bell (SignalR push + 5-minute polling fallback).

---

## 1. Background

Two user-reported problems:

1. **"Mark all as read" doesn't stick.** Selecting "Mark all read" clears the dropdown, but after a page refresh the notifications come back.
2. **The bell doesn't auto-check for new notifications.** It only refreshes on first render or when opened.

Investigation found problem #1 is one instance of a **wider bug**: `CoreDbContext` is configured with global `NoTracking`, so several "load entity → modify property → `SaveChangesAsync()`" code paths silently write **nothing** to the database.

---

## 2. Root cause (single cause, many symptoms)

File: `src/Core/DotNetCloud.Core.Data/Extensions/DataServiceExtensions.cs`

```csharp
services.AddDbContext<CoreDbContext>((sp, options) =>
{
    ConfigureDbContext(options, provider, connectionString);
}, ServiceLifetime.Transient);
```

And inside `ConfigureDbContext`:

```csharp
options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
```

Because tracking is disabled globally, any LINQ query that loads entities **without** `.AsTracking()` returns **untracked** entities. Setting properties on untracked entities and calling `SaveChangesAsync()` produces **zero UPDATE statements**. The call "succeeds" (returns normally / 204), so nothing errors — the change is simply lost.

**What still works** (do not change these): `Add`, `AddRange`, `Update`, `Attach`, `Remove`, `RemoveRange` — these explicitly attach entities. This is why notification _creation_ works but notification _mark-as-read_ does not.

**Important:** Module `DbContext`s (Files, Chat, Calendar, etc.) do **NOT** set `NoTracking` — they are unaffected. Only `CoreDbContext`-backed services are affected.

---

## 3. Confirmed bug sites (9 total — fix ALL of them)

| #   | File                                                                 | Method               | Broken behavior                                                           |
| --- | -------------------------------------------------------------------- | -------------------- | ------------------------------------------------------------------------- |
| 1   | `src/Core/DotNetCloud.Core.Server/Services/NotificationService.cs`   | `MarkReadAsync`      | Single notification read flag never persists                              |
| 2   | `src/Core/DotNetCloud.Core.Server/Services/NotificationService.cs`   | `MarkAllReadAsync`   | THE REPORTED BUG — all-read never persists                                |
| 3   | `src/Core/DotNetCloud.Core.Server/Services/AdminModuleService.cs`    | `StartModuleAsync`   | Module `Status = "Enabled"` never persists                                |
| 4   | `src/Core/DotNetCloud.Core.Server/Services/AdminModuleService.cs`    | `StopModuleAsync`    | Module `Status = "Disabled"` never persists                               |
| 5   | `src/Core/DotNetCloud.Core.Auth/Services/MfaService.cs`              | `UseBackupCodeAsync` | Used backup code never marked used → **reusable backup codes (security)** |
| 6   | `src/Core/DotNetCloud.Core.Auth/Capabilities/GroupManagerService.cs` | `UpdateGroupAsync`   | Group name/description changes never persist                              |
| 7   | `src/Core/DotNetCloud.Core.Auth/Capabilities/GroupManagerService.cs` | `DeleteGroupAsync`   | Soft delete (`IsDeleted`/`DeletedAt`) never persists                      |
| 8   | `src/Core/DotNetCloud.Core.Auth/Capabilities/TeamManagerService.cs`  | `UpdateTeamAsync`    | Team name/description changes never persist                               |
| 9   | `src/Core/DotNetCloud.Core.Auth/Capabilities/TeamManagerService.cs`  | `DeleteTeamAsync`    | Soft delete never persists                                                |

---

## 4. Step 1 — Fix all 9 sites (add `.AsTracking()`)

The fix for every site is identical in nature: add `.AsTracking()` to the load query so the entity is tracked, making `SaveChangesAsync()` actually emit the UPDATE.

**Do NOT** use `ExecuteUpdateAsync` (the InMemory-based test infrastructure does not support it).
**Do NOT** flip the global `NoTracking` default (reads already use `.AsNoTracking()`; flipping it would be a risky broad refactor).

### 4.1 `NotificationService.cs`

Current `MarkReadAsync`:

```csharp
var notification = await _db.Notifications
    .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, cancellationToken);
```

Change to:

```csharp
var notification = await _db.Notifications
    .AsTracking()
    .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, cancellationToken);
```

Current `MarkAllReadAsync`:

```csharp
var unread = await _db.Notifications
    .Where(n => n.UserId == userId && n.ReadAtUtc == null)
    .ToListAsync(cancellationToken);
```

Change to:

```csharp
var unread = await _db.Notifications
    .AsTracking()
    .Where(n => n.UserId == userId && n.ReadAtUtc == null)
    .ToListAsync(cancellationToken);
```

Leave the rest of the method bodies unchanged (the `foreach` setting `ReadAtUtc` and the `SaveChangesAsync` stay the same).

### 4.2 `AdminModuleService.cs`

In `StartModuleAsync`, change:

```csharp
var module = await _dbContext.InstalledModules
    .FirstOrDefaultAsync(m => m.ModuleId == moduleId, cancellationToken);
```

to:

```csharp
var module = await _dbContext.InstalledModules
    .AsTracking()
    .FirstOrDefaultAsync(m => m.ModuleId == moduleId, cancellationToken);
```

In `StopModuleAsync`, make the same change (add `.AsTracking()` to the `FirstOrDefaultAsync` query).

Do **NOT** change `RestartModuleAsync` (it does not modify an entity), `GrantCapabilityAsync` (uses `Add`), or `RevokeCapabilityAsync` (uses `Remove` — attaches correctly).

### 4.3 `MfaService.cs`

In `UseBackupCodeAsync`, change:

```csharp
var backupCode = await _dbContext.UserBackupCodes
    .FirstOrDefaultAsync(c => c.UserId == userId && c.CodeHash == hash && !c.IsUsed);
```

to:

```csharp
var backupCode = await _dbContext.UserBackupCodes
    .AsTracking()
    .FirstOrDefaultAsync(c => c.UserId == userId && c.CodeHash == hash && !c.IsUsed);
```

Do **NOT** change `GenerateBackupCodesAsync` or `DisableMfaAsync` (they use `RemoveRange`, which attaches correctly).

### 4.4 `GroupManagerService.cs`

In `UpdateGroupAsync`, change:

```csharp
var group = await _dbContext.Groups
    .Include(g => g.Members)
    .FirstOrDefaultAsync(g => g.Id == groupId && !g.IsDeleted, cancellationToken);
```

to:

```csharp
var group = await _dbContext.Groups
    .AsTracking()
    .Include(g => g.Members)
    .FirstOrDefaultAsync(g => g.Id == groupId && !g.IsDeleted, cancellationToken);
```

In `DeleteGroupAsync`, change:

```csharp
var group = await _dbContext.Groups
    .FirstOrDefaultAsync(g => g.Id == groupId && !g.IsDeleted, cancellationToken);
```

to:

```csharp
var group = await _dbContext.Groups
    .AsTracking()
    .FirstOrDefaultAsync(g => g.Id == groupId && !g.IsDeleted, cancellationToken);
```

Do **NOT** change `CreateGroupAsync`, `AddMemberAsync` (use `Add`), or `RemoveMemberAsync` (uses `Remove`).

### 4.5 `TeamManagerService.cs`

In `UpdateTeamAsync`, change:

```csharp
var team = await _dbContext.Teams
    .Include(t => t.Members)
    .FirstOrDefaultAsync(t => t.Id == teamId && !t.IsDeleted, cancellationToken);
```

to:

```csharp
var team = await _dbContext.Teams
    .AsTracking()
    .Include(t => t.Members)
    .FirstOrDefaultAsync(t => t.Id == teamId && !t.IsDeleted, cancellationToken);
```

In `DeleteTeamAsync`, change:

```csharp
var team = await _dbContext.Teams
    .FirstOrDefaultAsync(t => t.Id == teamId && !t.IsDeleted, cancellationToken);
```

to:

```csharp
var team = await _dbContext.Teams
    .AsTracking()
    .FirstOrDefaultAsync(t => t.Id == teamId && !t.IsDeleted, cancellationToken);
```

Do **NOT** change `CreateTeamAsync`, `AddMemberAsync` (use `Add`), or `RemoveMemberAsync` (uses `Remove`).

---

## 5. Step 2 — Regression tests

**Critical test-infra fact:** The existing tests build `CoreDbContext` with `UseInMemoryDatabase(...)` and **default tracking**, which does **not** reproduce the bug. Every regression test below MUST configure `UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)` to mirror production.

**Critical InMemory fact:** The in-memory database is keyed by name. To verify persistence, create a **second** `CoreDbContext` reusing the **same database name** (a "verify" context).

### 5.1 Worked example — `NotificationServiceTests` (NEW file)

Create `tests/DotNetCloud.Core.Server.Tests/Services/NotificationServiceTests.cs`.

```csharp
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Notifications;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Server.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Services;

[TestClass]
public class NotificationServiceTests
{
    private const string DbName = "NotificationServiceTests"; // reuse across contexts

    private static CoreDbContext CreateContext() =>
        new(
            new DbContextOptionsBuilder<CoreDbContext>()
                .UseInMemoryDatabase(DbName)
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
                .Options,
            new PostgreSqlNamingStrategy());

    [TestInitialize]
    public void Setup()
    {
        using var db = CreateContext();
        db.Database.EnsureDeleted(); // clear shared in-memory db between tests
    }

    private static NotificationService CreateService(CoreDbContext db) =>
        new(db, Mock.Of<DotNetCloud.Core.Events.IEventBus>());

    private static Notification SeedNotification(CoreDbContext db, Guid userId)
    {
        var n = new Notification
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            SourceModuleId = "test",
            Type = DotNetCloud.Core.DTOs.NotificationType.Info,
            Title = "Test",
            CreatedAtUtc = DateTime.UtcNow,
            ReadAtUtc = null
        };
        db.Notifications.Add(n);
        db.SaveChanges();
        return n;
    }

    [TestMethod]
    public async Task MarkAllReadAsync_PersistsReadState()
    {
        var userId = Guid.CreateVersion7();
        using (var seed = CreateContext())
        {
            SeedNotification(seed, userId);
            SeedNotification(seed, userId);
            await CreateService(seed).MarkAllReadAsync(userId);
        }

        using var verify = CreateContext();
        var unread = await verify.Notifications.CountAsync(n => n.UserId == userId && n.ReadAtUtc == null);
        Assert.AreEqual(0, unread);
    }

    [TestMethod]
    public async Task MarkReadAsync_PersistsReadState()
    {
        var userId = Guid.CreateVersion7();
        Guid notificationId;
        using (var seed = CreateContext())
        {
            notificationId = SeedNotification(seed, userId).Id;
            await CreateService(seed).MarkReadAsync(notificationId, userId);
        }

        using var verify = CreateContext();
        var n = await verify.Notifications.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == notificationId);
        Assert.IsNotNull(n);
        Assert.IsNotNull(n!.ReadAtUtc);
    }
}
```

Notes:

- `NotificationService` is `internal` — accessible because `DotNetCloud.Core.Server.csproj` has `<InternalsVisibleTo Include="DotNetCloud.Core.Server.Tests" />`.
- `NotificationService` constructor: `(CoreDbContext db, IEventBus eventBus)`. The mark methods don't use the event bus, so `Mock.Of<IEventBus>()` is fine. `IEventBus` here is `DotNetCloud.Core.Events.IEventBus` (the event-bus namespace), **not** the capabilities one — check the `using` above.
- Verify the namespace of `Notification` entity: `DotNetCloud.Core.Data.Entities.Notifications`.

### 5.2 Extend `MfaServiceTests` (backup code single-use)

File: `tests/DotNetCloud.Core.Auth.Tests/Services/MfaServiceTests.cs` (already exists).

Add a new test method (do NOT modify the existing `Setup`; build local NoTracking contexts inside the test). Use the same "shared database name + verify context" pattern as 5.1.

Test outline:

1. Seed a `UserBackupCode` with `IsUsed = false` and a known `CodeHash` (use the service's private `HashCode` is private — instead compute `SHA256` of `code.ToUpperInvariant()` bytes in the test, or call `GenerateBackupCodesAsync` to seed, then read the plaintext codes returned).
2. Call `UseBackupCodeAsync(userId, code)` → expect `true`.
3. Verify via a fresh context that the matching `UserBackupCode` now has `IsUsed == true` and `UsedAt != null`.
4. (Optional) Call `UseBackupCodeAsync` again with the same code → expect `false`.

Simplest seeding approach: call `GenerateBackupCodesAsync(userId)` first, capture `response.Codes[0]`, then `UseBackupCodeAsync(userId, response.Codes[0])`.

### 5.3 Extend `GroupManagerServiceTests` (update + soft delete persist)

File: `tests/DotNetCloud.Core.Auth.Tests/Capabilities/GroupManagerServiceTests.cs` (already exists).

Add new test methods with local NoTracking contexts:

1. `UpdateGroupAsync_PersistsNameAndDescription`: seed an org + group (via `CreateGroupAsync` or direct `Add`), call `UpdateGroupAsync(groupId, "New Name", "New Desc")`, verify via fresh context that `Name == "New Name"` and `Description == "New Desc"`.
2. `DeleteGroupAsync_PersistsSoftDelete`: seed org + group, call `DeleteGroupAsync(groupId)` → `true`, verify via fresh context that `IsDeleted == true` and `DeletedAt != null`.

### 5.4 Extend `TeamManagerServiceTests` (update + soft delete persist)

There is **no existing** `TeamManagerServiceTests.cs`. Create it at
`tests/DotNetCloud.Core.Auth.Tests/Capabilities/TeamManagerServiceTests.cs`, modeled on `GroupManagerServiceTests.cs` but for `TeamManagerService`, and include the two persistence tests from 5.3 (update + soft delete).

Note `TeamManagerService.CreateTeamAsync(organizationId, name, description, createdByUserId, ct)` also inserts the creator as a `TeamMember` — you can seed teams directly via `_dbContext.Teams.Add(...)` instead to keep the test simple.

### 5.5 `AdminModuleServiceTests` (start/stop status persists)

There is **no existing** test. Create `tests/DotNetCloud.Core.Server.Tests/Services/AdminModuleServiceTests.cs`.

`AdminModuleService` constructor: `(CoreDbContext dbContext, IProcessSupervisor processSupervisor, ILogger<AdminModuleService> logger)`. Mock `IProcessSupervisor` with `Moq` — `StartModuleAsync` and `StopModuleAsync` should return `Task.CompletedTask`. It is `internal` (covered by `InternalsVisibleTo`).

Tests:

1. `StartModuleAsync_PersistsEnabledStatus`: seed an `InstalledModule` with `Status = "Disabled"`, call `StartModuleAsync(moduleId, ct)` → `true`, verify via fresh context that `Status == "Enabled"`.
2. `StopModuleAsync_PersistsDisabledStatus`: seed an `InstalledModule` with `Status = "Enabled"` and `IsRequired = false` (required modules throw), call `StopModuleAsync(...)` → `true`, verify `Status == "Disabled"`.

`InstalledModule` entity is in `DotNetCloud.Core.Data.Entities.Modules`; `IProcessSupervisor` is in `DotNetCloud.Core.Modules.Supervisor`.

---

## 6. Step 3 — Real-time SignalR client (server-circuit)

The server already broadcasts `notification.created` (payload = `NotificationDto`) to the recipient via `RealtimeNotificationChannel` → `IRealtimeBroadcaster` → `CoreHub`, mapped at `/hubs/core`. We only need a client listener.

### 6.1 Add the SignalR client package

In `src/Core/DotNetCloud.Core.Server/DotNetCloud.Core.Server.csproj`, add to the existing `<ItemGroup>` of `PackageReference`s:

```xml
<PackageReference Include="Microsoft.AspNetCore.SignalR.Client" />
```

(The version is already pinned in `Directory.Packages.props`.)

### 6.2 New interface (shared with the Blazor component)

The bell component lives in `DotNetCloud.UI.Web`, which references `DotNetCloud.UI.Web.Client` but **not** `DotNetCloud.Core.Server`. So the interface must live in an assembly the bell can see.

Create `src/UI/DotNetCloud.UI.Web.Client/Services/IRealtimeNotificationClient.cs`:

```csharp
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.UI.Web.Client.Services;

/// <summary>
/// Receives real-time notification events for the web UI.
/// </summary>
public interface IRealtimeNotificationClient
{
    /// <summary>Raised when a new notification is created for the current user.</summary>
    event Action<NotificationDto>? NotificationCreated;

    /// <summary>
    /// Starts the real-time connection. Safe to call multiple times; no-ops if
    /// already started or if no auth cookie is available.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);
}
```

### 6.3 New shared TLS callback helper

Create `src/Core/DotNetCloud.Core.Server/Middleware/LoopbackCertificateValidator.cs`:

```csharp
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace DotNetCloud.Core.Server.Middleware;

/// <summary>
/// Shared TLS validation callback for loopback HTTP clients.
/// </summary>
internal static class LoopbackCertificateValidator
{
    /// <summary>
    /// Accepts TLS errors only when the sole issue is a hostname mismatch
    /// (e.g. connecting to localhost with a cert for cloud.dotnetcloud.net).
    /// </summary>
    public static bool Validate(
        object sender,
        X509Certificate? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors)
    {
        return sslPolicyErrors == SslPolicyErrors.None
            || sslPolicyErrors == SslPolicyErrors.RemoteCertificateNameMismatch;
    }
}
```

(There is an identical private method `AcceptLoopbackCertificate` in `Program.cs` — leave it as-is; do not refactor it.)

### 6.4 New realtime client implementation

Create `src/Core/DotNetCloud.Core.Server/RealTime/RealtimeNotificationClient.cs`:

```csharp
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Server.Middleware;
using DotNetCloud.UI.Web.Client.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.RealTime;

/// <summary>
/// Server-circuit SignalR client that listens for <c>notification.created</c> events
/// on the CoreHub and raises them for Blazor components (e.g. the notification bell).
/// Authenticates by forwarding the circuit's captured auth cookie over HTTP transports.
/// </summary>
internal sealed class RealtimeNotificationClient : IRealtimeNotificationClient, IAsyncDisposable
{
    private readonly CookieCaptureStore _cookieStore;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RealtimeNotificationClient> _logger;

    private HubConnection? _hub;

    public RealtimeNotificationClient(
        CookieCaptureStore cookieStore,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        ILogger<RealtimeNotificationClient> logger)
    {
        _cookieStore = cookieStore;
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public event Action<NotificationDto>? NotificationCreated;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_hub is not null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_cookieStore.CookieHeader))
        {
            _logger.LogDebug("No auth cookie captured; skipping realtime notification connection.");
            return;
        }

        var httpsPort = _configuration.GetValue<int>("httpsPort", 5443);
        var hubUrl = $"https://localhost:{httpsPort}/hubs/core";

        _hub = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                // HTTP transports only: the forwarded auth cookie authenticates the
                // connection. WebSockets would bypass HttpMessageHandlerFactory.
                options.Transports = HttpTransportType.ServerSentEvents | HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ =>
                {
                    var inner = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = LoopbackCertificateValidator.Validate
                    };

                    return new CookieForwardingHandler(_cookieStore, _httpContextAccessor)
                    {
                        InnerHandler = inner
                    };
                };
            })
            .WithAutomaticReconnect()
            .Build();

        _hub.On<NotificationDto>("notification.created", notification =>
            NotificationCreated?.Invoke(notification));

        try
        {
            await _hub.StartAsync(cancellationToken);
            _logger.LogInformation("Realtime notification connection started.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start realtime notification connection.");
            await _hub.DisposeAsync();
            _hub = null;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
        {
            await _hub.DisposeAsync();
            _hub = null;
        }
    }
}
```

Notes:

- `CookieCaptureStore`, `CookieForwardingHandler`, and `LoopbackCertificateValidator` are all `internal` to Core.Server — this class is in the same assembly, so it can use them.
- The service must be **scoped** so it shares the circuit's `CookieCaptureStore` and forwards the correct user's cookie.

### 6.5 Register the service

In `src/Core/DotNetCloud.Core.Server/Program.cs`, add the registration right after the `DotNetCloudApiClient` typed HttpClient registration (which ends around the block with comment "Typed HttpClient for server prerendering of client components (NotificationBell, etc.)").

Add:

```csharp
builder.Services.AddScoped<DotNetCloud.UI.Web.Client.Services.IRealtimeNotificationClient, DotNetCloud.Core.Server.RealTime.RealtimeNotificationClient>();
```

Use fully-qualified type names as shown — this avoids needing to add new `using` directives.

---

## 7. Step 4 — Wire the bell component

File: `src/UI/DotNetCloud.UI.Web/Components/Shared/NotificationBell.razor`

### 7.1 Directives

At the top of the file, keep the existing `@using` lines and add one `@inject` plus `@implements IAsyncDisposable`.

Current top:

```razor
@using DotNetCloud.Core.DTOs
@using DotNetCloud.UI.Web.Client.Services
@inject DotNetCloudApiClient ApiClient
@inject NavigationManager Navigation
```

Change to:

```razor
@using DotNetCloud.Core.DTOs
@using DotNetCloud.UI.Web.Client.Services
@inject DotNetCloudApiClient ApiClient
@inject IRealtimeNotificationClient RealtimeClient
@inject NavigationManager Navigation
@implements IAsyncDisposable
```

### 7.2 Replace the entire `@code` block

Replace the whole `@code { ... }` block with the version below. (The markup above `@code` is unchanged.)

```razor
@code {
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

    private bool _isOpen;
    private bool _isLoading;
    private int _unreadCount;
    private readonly List<NotificationDto> _notifications = [];

    private CancellationTokenSource? _pollCts;
    private Task? _pollTask;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        RealtimeClient.NotificationCreated += OnNotificationCreated;

        try { await RealtimeClient.StartAsync(); }
        catch { /* silent — polling fallback still works */ }

        await RefreshUnreadCountAsync();
        StartPolling();
    }

    private void StartPolling()
    {
        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _pollCts = new CancellationTokenSource();
        _pollTask = PollLoopAsync(_pollCts.Token);
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await RefreshFromServerAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the component is disposed.
        }
    }

    private void OnNotificationCreated(NotificationDto notification)
    {
        _ = RefreshFromServerAsync();
    }

    private async Task RefreshFromServerAsync()
    {
        await RefreshUnreadCountAsync();

        if (_isOpen)
        {
            _isLoading = true;
            try
            {
                var latest = await ApiClient.GetUnreadNotificationsAsync(25);
                _notifications.Clear();
                _notifications.AddRange(latest);
            }
            catch { /* silent */ }
            finally
            {
                _isLoading = false;
            }
        }
    }

    private async Task ToggleDropdown()
    {
        _isOpen = !_isOpen;

        if (_isOpen)
        {
            _isLoading = true;
            try
            {
                _notifications.Clear();
                _notifications.AddRange(await ApiClient.GetUnreadNotificationsAsync(25));
            }
            finally
            {
                _isLoading = false;
            }
        }
    }

    private async Task RefreshUnreadCountAsync()
    {
        try { _unreadCount = await ApiClient.GetUnreadNotificationCountAsync(); }
        catch { /* silent */ }
    }

    private async Task OpenNotificationAsync(NotificationDto notification)
    {
        await ApiClient.MarkNotificationReadAsync(notification.Id);
        _notifications.RemoveAll(n => n.Id == notification.Id);
        await RefreshUnreadCountAsync();

        Navigation.NavigateTo(string.IsNullOrWhiteSpace(notification.ActionUrl)
            ? "/"
            : notification.ActionUrl);
    }

    private async Task MarkAllReadAsync()
    {
        await ApiClient.MarkAllNotificationsReadAsync();
        _notifications.Clear();
        await RefreshUnreadCountAsync();
    }

    public async ValueTask DisposeAsync()
    {
        RealtimeClient.NotificationCreated -= OnNotificationCreated;

        _pollCts?.Cancel();

        if (_pollTask is not null)
        {
            try { await _pollTask; }
            catch { /* ignore */ }
        }

        _pollCts?.Dispose();
    }
}
```

Behavior summary:

- On first interactive render: subscribe to SignalR, start the connection, refresh the badge, start the 5-minute polling loop.
- New notification (SignalR) or poll tick → refresh the badge; if the dropdown is open, re-fetch the list.
- `DisposeAsync` unsubscribes and stops the polling loop.
- `MarkAllReadAsync` now actually persists because of Step 1.

---

## 8. Step 5 — Build and test

Run in a terminal from the repo root:

```bash
dotnet build DotNetCloud.CI.slnf -c Release
```

Then run the affected test projects:

```bash
dotnet test tests/DotNetCloud.Core.Server.Tests -c Release
dotnet test tests/DotNetCloud.Core.Auth.Tests -c Release
dotnet test tests/DotNetCloud.Integration.Tests -c Release --filter Notifications
```

Expect: build 0 errors; all new/updated regression tests pass.

---

## 9. Step 6 — Manual verification checklist

1. Open the bell, click **Mark all read**, then refresh the page → list stays empty, badge gone.
2. Click a single notification (navigates away), then go back → that notification stays read.
3. Rename a group and a team in the admin UI → names persist after refresh.
4. Soft-delete a group and a team → they stay deleted.
5. Consume an MFA backup code → it cannot be used a second time.
6. Start/stop a module in the admin UI → the status persists after a server restart.
7. Trigger a new notification (e.g., share a file with the current user) with the bell visible → badge updates within a few seconds (SignalR). If SignalR is unavailable, it updates within 5 minutes (polling).

---

## 10. Step 7 — Update tracking docs (repo policy)

After implementation completes, use **targeted edits** (not full-file replacement) to update:

1. `docs/IMPLEMENTATION_CHECKLIST.md` — mark any notification/realtime task as `✓` (or add one if missing).
2. `docs/MASTER_PROJECT_PLAN.md` — update the Quick Status Summary and the relevant step's `**Status:**`, `**Deliverables:**`, and `**Notes:**`.

Checkbox style: use `✓` (done) and `☐` (pending) — never `[x]` / `[ ]`.

---

## 11. Gotchas / do-nots

- Do **not** use `ExecuteUpdateAsync` — InMemory test factory does not support it.
- Do **not** flip the global `NoTracking` default.
- Do **not** change `Remove`/`RemoveRange`/`Add` call sites — they already work.
- In tests, always configure `UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)` or the test will not catch the regression.
- In tests, reuse the **same** InMemory database name between the "seed" context and the "verify" context.
- SignalR connection MUST use `HttpTransportType.ServerSentEvents | HttpTransportType.LongPolling` (not WebSockets) so the forwarded auth cookie is applied.
- `TeamManagerServiceTests.cs` and `AdminModuleServiceTests.cs` do not exist yet — create them.
- `NotificationService`, `AdminModuleService`, `RealtimeNotificationClient`, and the middleware classes are `internal` — tests for them must live in `DotNetCloud.Core.Server.Tests` (covered by `InternalsVisibleTo`) and `DotNetCloud.Core.Auth` classes are public (tests live in `DotNetCloud.Core.Auth.Tests`).
