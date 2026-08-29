# Client/Server Mediation Handoff

Last updated: 2026-08-29 (new Active Handoff: AI module REST API for the Android AI tab; previous Files gRPC handoff archived)

Purpose: shared handoff between client-side and server-side agents, mediated by user.

Archived context:

- Historical completed updates are in `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- Additional history remains available in git.

## Process Rules

**Agent autonomy (CRITICAL):**

- Both client and server agents work autonomously — they do NOT ask the moderator for context or permission.
- Agents pull the branch specified in the relay message, read the **Active Handoff** section, and execute the work described there independently.
- All actionable items, blockers, and technical details go directly in this document.
- **Current active branch:** `feature/android-ai-tab`

## Archived Handoff — SyncTray test machine: DB Outage SyncTray Simulation (plan §11.4) ✅ PASS

**Status:** completed ✅ (2026-08-24, client agent — `Windows11-DNC`)
**Branch:** `fix/database-offline-recovery`
**Canonical plan:** `docs/DB_OUTAGE_RESILIENCE_PLAN.md` §11.4 (SyncTray simulation)

### Client pre-requisite (done)
- SyncTray **0.4.07** rebuilt from HEAD (`cbddab37`) and installed to `C:\Program Files\DotNetCloud\DesktopClient\SyncTray` (updater 0.4.07 too; old 0.4.02 backed up to `SyncTray.bak-0.4.02`).
- Server verified Healthy before outage: `/health/ready` Healthy, `database` Healthy, 14/14 modules.
- SyncTray running, 1 account, token valid, sync idle (0 changes) → tray green.

### Outage phase (moderator: `sudo systemctl stop dotnetcloud`)
- ✅ Server confirmed down: `cloud.dotnetcloud.net:443` connection refused.
- ✅ **Tray → gray** within one backoff interval: `10:04:40 WRN Server unreachable while syncing context` → `SyncEngine` sets `SyncState.Offline` → tray `TrayState.Offline` (gray); tooltip "DotNetCloud Sync — server unreachable, retrying automatically".
- ✅ **Automatic retry/backoff**: SSE reconnects 2s → 4s → 8s → 16s → 32s → 60s (attempts 1–8), then holds at 60s.
- ✅ **"Sync now" fast-fail**: connection-refused fails fast; `TimeoutHandler` caps requests at 30s; sync pass observed failing in ~12s. No hang.

### Recovery phase (moderator: `sudo systemctl start dotnetcloud`)
- ✅ Server recovered: `/health/ready` Healthy, `database` Healthy, 14/14 modules.
- ✅ **Automatic recovery, no manual restart**: failing sync passes at 10:07:25/10:07:45 (server still down) → successful pass at 10:08:04 (`Sync pass complete, RemoteChanges=0, LocalQueued=0, LocalApplied=0`) → `SyncState.Idle`.
- ✅ SSE reconnected automatically at **10:08:52** (within the 60s backoff cap).
- ✅ Tray returned to **green/idle** (visually confirmed by moderator).

**Result: PASS** — no client regressions observed. Evidence in `%LOCALAPPDATA%\DotNetCloud\logs\sync-tray20260824.log`.

## Archived Handoff — Android test machine: DB Outage Android Simulation (plan §11.5) ✅ PASS

**Status:** completed ✅ (2026-08-24, client agent — `monolith`)
**Branch:** `fix/database-offline-recovery`
**Canonical plan:** `docs/DB_OUTAGE_RESILIENCE_PLAN.md` §11.5 (Android simulation)
**Prerequisite (DONE — server agent):** Server deploy verified on `cloud.kimball.home` (`https://cloud.dotnetcloud.net/`): resilience code live, `/health/ready` Healthy, `database` Healthy, 14/14 modules. §11.2/§11.3 + integration tests passed. SyncTray client simulation §11.4 → PASS (archived above).

### Client pre-requisite (done)
- Android **0.4.07** rebuilt from HEAD (`8bdc1f57`) arm64-debug and installed to physical phone (Samsung S24 Ultra, `R5CWC356B2K`). Logged in to `https://cloud.dotnetcloud.net/`, SignalR connected, channels loaded.
- Server verified Healthy before outage: `/health/ready` Healthy, `database` Healthy, 14/14 modules.

### Outage phase (moderator: `sudo systemctl stop dotnetcloud`)
- ✅ Server confirmed down: `cloud.dotnetcloud.net:443` connection refused.
- ✅ **Global red banner appeared** ("Can't reach server — showing cached data. Changes will be queued.") over the channel list.
- ✅ **Chat showed cached messages** (Test 6/7/8, Aug 5 "Posting remotely" msg, etc.) with the banner still visible.
- ✅ **Sending a message queued it**: `OFFLINE_QUEUE_TEST_1301` appeared in the list with "just now" + the "Message queued — will send when you're back online." banner.

### Recovery phase (moderator: `sudo systemctl start dotnetcloud`)
- ✅ Server recovered: `/health/ready` Healthy, `database` Healthy, 14/14 modules.
- ✅ **Banner cleared automatically** (≤ ~20 s probe interval; confirmed via UI dump — banner gone).
- ✅ **Queued message flushed**: `GetMessagesAsync` fetched `OFFLINE_QUEUE_TEST_1301` from the server; message shows as sent ("1m ago", no queued indicator).
- ✅ SignalR reconnected automatically (`JoinChannelGroupAsync` joined `chat-channel-…`).

### Client bugs found & fixed during the sim (committed on this branch)
1. **Android receiver/service `Name` bug → cold-start crash.** `CalendarBootReceiver`, `CalendarAlarmReceiver`, `FcmMessagingService`, `UnifiedPushReceiver` were declared both manually in `AndroidManifest.xml` (`.X` → `net.dotnetcloud.client.X`) and via `[BroadcastReceiver]`/`[Service]` attributes without explicit `Name`, so the generated Java class landed in a `crc…` package → `ClassNotFoundException` when Android instantiated it (the sticky `BOOT_COMPLETED` broadcast crashed every cold start). Fixed by adding `Name = "net.dotnetcloud.client.X"` to the attributes (mirrors the working `[Service(Name=…)]` pattern).
2. **Phase E banner overlay crashed launch.** Wrapping the `Shell` in a `Grid` violates MAUI's "Parent of a Page must also be a Page". Replaced with a native Android platform overlay on `Android.Resource.Id.Content` driven by `ConnectivityViewModel`, offset below the status bar (`ResolveStatusBarHeight`). Old `ConnectivityBannerView.xaml` deleted.

**Result: PASS** — no client regressions. Evidence: `dnc-banner-visible.png`, `dnc-message-queued.png`, `dnc-recovered.png` (on monolith), UI-dump text nodes, and logcat (`adb logcat`).

## Active Handoff

### Server: expose the AI module REST API for the Android AI tab (proxy + host auth + rename)

**Status:** pending (server agent — `cloud.kimball.home`)
**Branch:** `feature/android-ai-tab`
**From:** client agent (`monolith`), 2026-08-29
**Priority:** required — the Android AI tab (client work, Phases B–F) depends on this to work end-to-end.
**Canonical plan:** `docs/ANDROID_AI_TAB_PLAN.md` (Phase A).
**Target:** `cloud.kimball.home` (`https://cloud.dotnetcloud.net/`). **Do not target `mint22` — it is currently offline.**

**Context:** The Android client is getting an optional **AI Assistant** tab (mirrors the Blazor AI chat
page). It talks to the AI module over REST through Core.Server's module proxy, using the same pattern as
the Music tab. Three gaps block this today:

1. Core.Server's `MapModuleApiProxies` has no `api/v1/ai` route — the AI REST API is unreachable from clients.
2. The AI module host only configures cookie auth (no token introspection), so Bearer-token requests from
   mobile cannot be authenticated, and `AiChatController` has no `[Authorize]` (it falls back to a system
   caller context, so conversations would be shared across users).
3. The AI REST controller has no rename endpoint (rename only exists on gRPC).

**Required changes (server):**

1. `src/Core/DotNetCloud.Core.Server/Program.cs` — in `MapModuleApiProxies`, add to the `moduleMappings`
   dictionary:
   ```csharp
   ["api/v1/ai"] = "dotnetcloud.ai",
   ```
2. `src/Core/DotNetCloud.Core.Server/Program.cs` — in `app.UseResponseEnvelope(...)`, add `"/api/v1/ai/"` to
   `options.ExcludePaths` (the AI SSE stream must NOT be buffered/wrapped; same reason `/api/v1/music/` is
   already excluded).
3. `src/Modules/AI/DotNetCloud.Modules.AI.Host/Controllers/AiChatController.cs`:
   - Change `[Route("api/ai")]` → `[Route("api/v1/ai")]`.
   - Add `using Microsoft.AspNetCore.Authorization;` and `[Authorize]` on the class.
   - Replace `GetCallerContext()` so it throws on unauthenticated instead of falling back to a system
     context (mirror `MusicControllerBase.GetAuthenticatedCaller()` in
     `src/Modules/Music/DotNetCloud.Modules.Music.Host/Controllers/MusicControllerBase.cs`):
     ```csharp
     private CallerContext GetCallerContext()
     {
         if (User?.Identity?.IsAuthenticated != true)
             throw new UnauthorizedAccessException("Authentication is required.");

         var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
             ?? User.FindFirst("sub")?.Value;

         if (!Guid.TryParse(userIdClaim, out var userId))
             throw new UnauthorizedAccessException("Authenticated user identifier is invalid.");

         var roles = User.FindAll(System.Security.Claims.ClaimTypes.Role)
             .Select(c => c.Value)
             .Distinct(StringComparer.OrdinalIgnoreCase)
             .ToArray();

         return new CallerContext(userId, roles, CallerType.User);
     }
     ```
   - Add a rename endpoint (uses the existing `IAiChatService.RenameConversationAsync`):
     ```csharp
     /// <summary>Renames a conversation.</summary>
     [HttpPatch("conversations/{conversationId:guid}/title")]
     public async Task<IActionResult> RenameConversation(
         Guid conversationId,
         [FromBody] RenameConversationRequest request,
         CancellationToken cancellationToken)
     {
         var caller = GetCallerContext();
         var renamed = await _chatService.RenameConversationAsync(
             caller, conversationId, request.Title, cancellationToken);
         return renamed ? Ok(new { success = true }) : NotFound();
     }
     ```
     Plus a request DTO next to the other request types in the same file:
     ```csharp
     /// <summary>Request to rename a conversation.</summary>
     public sealed class RenameConversationRequest
     {
         /// <summary>The new title.</summary>
         public required string Title { get; set; }
     }
     ```
4. `src/Modules/AI/DotNetCloud.Modules.AI.Host/Program.cs` — port the Chat host auth (reference:
   `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Program.cs`):
   - Add `using DotNetCloud.Core.Auth.Authorization;` and `using DotNetCloud.Core.Auth.Introspection;`.
   - Call `builder.Services.AddTokenIntrospection();`.
   - Replace the cookie-only `builder.Services.AddAuthentication("Identity.Application").AddCookie(...)` block
     with the `DotNetCloud.Module` policy scheme: keep the existing `.AddCookie("Identity.Application", …)`
     options as-is, then chain `.AddIntrospection(IntrospectionAuthenticationExtensions.SchemeName)` and
     `.AddPolicyScheme("DotNetCloud.Module", "DotNetCloud.Module", …)` whose `ForwardDefaultSelector` returns the
     introspection scheme when the request has a `Bearer ` Authorization header, else `Identity.Application`.
   - Replace `builder.Services.AddAuthorization();` with
     `builder.Services.AddAuthorization(options => AuthorizationPolicies.Configure(options));` plus
     `builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();`.
   - Keep `app.UseAuthentication(); app.UseAuthorization();` in the pipeline (already present).

**Verify (on cloud.kimball.home):** with the AI module installed and Ollama reachable, confirm
`GET /api/v1/ai/models` returns 200 with a valid Bearer token and 401 without; a
create → send (stream) → list → rename → delete round-trip is per-user; and
`GET /api/v1/ai/health/ollama` returns 200 when healthy / 503 when not.

## Moderator Communication (Minimal)

**Moderator relays ONLY ONE OF THESE messages — nothing more:**

- `New handoff update for <target-machine>. Pull main and resume from 'Active Handoff' section.`
- `<Commit hash> — New handoff update for <target-machine>. Pull and check docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md Active Handoff.`

**No moderator task:** Moderator provides zero context, zero explanation. The handoff document has everything the receiving agent needs.

## Environment

| Role           | Machine              | Detail                                                                             |
| -------------- | -------------------- | ---------------------------------------------------------------------------------- |
| Server         | `cloud.kimball.home` | `https://cloud.dotnetcloud.net/` (production)                                      |
| Server         | `mint22`             | `https://mint22:5443/` (dev)                                                       |
| Client         | `Windows11-TestDNC`  | Sync dir: `C:\Users\benk\synctray`                                       |
| Client         | `mint-dnc-client`    | Linux Mint 22 validation host for desktop sync client implementation + E2E testing |
| Client         | `mint-OptiPlex-7010` | production client connected to `cloud.dotnetcloud.net`              |
| Android Client | `monolith`           | Android MAUI app development + emulator testing (Windows 11)                       |

## Key Carry-Forward Contracts

- Auth: Files module host uses a policy scheme (`DotNetCloud.Module`) that auto-selects between `OpenIddict.Validation.AspNetCore` (JWT Bearer) and `Identity.Application` (cookie) based on the `Authorization` header. Controllers use plain `[Authorize]`. All module hosts must follow this pattern.
- API envelope: middleware wraps responses; clients should unwrap via envelope helpers.
- Sync flow: changes -> tree -> reconcile -> chunk manifest -> chunk download -> file assembly.
- Desktop OAuth constant: `OAuthConstants.ClientId = "dotnetcloud-desktop"`.
- ✅ **SignalR channel group naming:** `chat-channel-{channelId}` (used by `ChatHub.ChannelGroup()`, `CoreHub.JoinGroupAsync()`, and Android `SignalRChatClient`).
- **Controller discovery:** Core.Server references Files.Host and Chat.Host via `ProjectReference`. ASP.NET Core auto-discovers controllers from referenced assemblies. Do NOT create duplicate controllers in Core.Server for routes already served by module Host assemblies.
- ✅ **Calendar event broadcasting pattern:** Follow `CalendarReminderEventHandler` (`CalendarReminderEventSubscriber` + `CalendarEventBroadcastHandler`) as the reference implementation. It calls `CoreCapabilitiesClient.BroadcastRealtimeEventAsync` for SignalR and `SendNotificationAsync` for FCM push.
- ✅ **DM notification flow:** `DmChannelCreatedEventHandler` subscribes to `ChannelCreatedEvent`. For `DirectMessage` channels only, it sends push via `IPushNotificationService` and raises `IChatMessageNotifier.DmChannelCreated` for in-process Blazor. `GlobalChatNotificationState` handles the Blazor-side toast. Android handles the push-side with 3 inline notification actions.

<!-- carry-forward contracts and old Android changes archived to CLIENT_SERVER_MEDIATION_ARCHIVE.md -->
