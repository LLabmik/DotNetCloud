# Client/Server Mediation Handoff

Last updated: 2026-03-08 (client agent, sync contract analysis)

Purpose: Shared handoff between client-side and server-side agents, mediated by user.

> **Note:** This document was compressed on 2026-03-08 to remove resolved debugging evidence.
> Full history is preserved in git (commits up to `a52d16d`).

## Process Rule (Mediator)

- All technical findings, raw evidence, and debugging conclusions must be written to this handoff document and pushed to `main`.
- Do not rely on chat-only technical status; chat to mediator should be minimal relay text only.
- Mediator role is relay-only (commit notifications and cross-agent request forwarding), not technical interpretation.

## Current Status

- **OAuth flow**: RESOLVED — authorize → login → callback → token exchange HTTP 200
- **JWT claims**: RESOLVED — access tokens are now unencrypted JWS with `sub` (user GUID), `name`, `preferred_username`, `email`
- **UserInfo endpoint**: RESOLVED — `/connect/userinfo` registered, advertised in discovery, returns DB-authoritative claims
- **Account persistence**: RESOLVED — `contexts.json` has real `UserId` GUID and proper `DisplayName`
- **Sync endpoints**: RESOLVED — `/api/v1/files/sync/{changes,tree,reconcile}` now mapped and require bearer auth (was 404)
- **TLS cert bypass for sync client**: RESOLVED — `DotNetCloudSync` named HttpClient now uses same `OAuthHttpClientHandlerFactory` cert bypass (was missing, would fail on self-signed cert)
- **Next milestone**: Fix client/server sync API contract mismatches (see **Active Blockers** below), then verify end-to-end sync pass

## Active Blockers (Must Fix Before Sync Works)

### Blocker 1: `userId` Query Parameter Mismatch

**Severity:** BLOCKING — sync calls will return 403

**Problem:** `SyncController.cs` on the server requires `[FromQuery] Guid userId` on all three endpoints (`changes`, `tree`, `reconcile`). The client's `DotNetCloudApiClient.cs` does NOT send `userId` in the query string. ASP.NET Core model binding will set `userId = Guid.Empty`, then `FilesControllerBase.ToCaller()` compares `authenticatedUserId != Guid.Empty` → throws `ForbiddenException` → HTTP 403.

**Client URLs (what it sends):**
```
GET api/v1/files/sync/changes?since=2025-03-08T00:00:00.0000000Z
GET api/v1/files/sync/tree
GET api/v1/files/sync/tree?folderId={id}
POST api/v1/files/sync/reconcile  (body only)
```

**Server expects:**
```
GET api/v1/files/sync/changes?since=...&userId={guid}
GET api/v1/files/sync/tree?userId={guid}
POST api/v1/files/sync/reconcile?userId={guid}
```

**Recommended server-side fix:** Extract `userId` from the bearer token's `sub` claim directly in the controller actions, instead of requiring it as a query parameter. The bearer token already contains the authenticated user identity. This is more secure (no client-supplied userId to validate), simpler (fewer query params), and matches standard REST API patterns. Concretely:

```csharp
// BEFORE (current):
[HttpGet("changes")]
public Task<IActionResult> GetChangesAsync(
    [FromQuery] DateTime since,
    [FromQuery] Guid? folderId,
    [FromQuery] Guid userId) => ExecuteAsync(async () =>
{
    var changes = await _syncService.GetChangesSinceAsync(since, folderId, ToCaller(userId));
    return Ok(Envelope(changes));
});

// AFTER (recommended):
[HttpGet("changes")]
public Task<IActionResult> GetChangesAsync(
    [FromQuery] DateTime since,
    [FromQuery] Guid? folderId) => ExecuteAsync(async () =>
{
    var caller = GetAuthenticatedCaller(); // new helper — extracts userId from bearer sub claim
    var changes = await _syncService.GetChangesSinceAsync(since, folderId, caller);
    return Ok(Envelope(changes));
});
```

Add to `FilesControllerBase`:
```csharp
protected CallerContext GetAuthenticatedCaller()
{
    if (User?.Identity?.IsAuthenticated != true)
        throw new ForbiddenException("Authentication is required.");

    var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
    if (!Guid.TryParse(claimValue, out var userId))
        throw new ForbiddenException("Authenticated user identifier is invalid.");

    var roles = User.FindAll(ClaimTypes.Role)
        .Select(c => c.Value)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    return new CallerContext(userId, roles, CallerType.User);
}
```

Apply the same change to all three `SyncController` actions: `GetChangesAsync`, `GetTreeAsync`, `ReconcileAsync`.

### Blocker 2: Response Envelope Mismatch

**Severity:** BLOCKING — sync deserialization will fail

**Problem:** `SyncController` wraps all responses in `Envelope()` → `{ success: true, data: [...] }`. The client's `DotNetCloudApiClient.GetAsync<T>()` calls `response.Content.ReadFromJsonAsync<T>()` expecting a raw array/object directly (e.g., `List<SyncChangeResponse>`), not wrapped in an envelope.

**Server currently returns:**
```json
{
  "success": true,
  "data": [
    { "nodeId": "...", "name": "...", "isDeleted": false, ... }
  ]
}
```

**Client expects (deserializes as `List<SyncChangeResponse>`):**
```json
[
  { "nodeId": "...", "name": "...", "isDeleted": false, ... }
]
```

**Recommended server-side fix:** Return raw data from `SyncController` actions (these are client-facing sync endpoints, not browser-facing UI APIs). Change `Ok(Envelope(data))` to `Ok(data)` in all three `SyncController` actions:

```csharp
// BEFORE:
return Ok(Envelope(changes));

// AFTER:
return Ok(changes);
```

**Alternative client-side fix (if server wants to keep envelopes):** Add an `EnvelopeResponse<T>` wrapper class to the client and unwrap in `GetAsync<T>`. But this adds complexity for no benefit on machine-to-machine sync APIs.

## Environment

- Client machine: `Windows11-TestDNC`
- Server machine: `mint22`
- Server URL: `https://mint22:15443/`
- Client sync directory: `C:\Users\benk\Documents\synctray`

## Required Server State

1. **`AuthServiceExtensions.cs`** — OpenIddict scopes: `openid`, `profile`, `email`, `offline_access`, `files:read`, `files:write`. Access token encryption disabled (`DisableAccessTokenEncryption()`). UserInfo endpoint registered.
2. **`OidcClientSeeder.cs`** — `dotnetcloud-desktop` app registration with upsert behavior, permissions for `files:read`/`files:write`.
3. **`OpenIddictEndpointsExtensions.cs`** — Authorize endpoint looks up `ApplicationUser`, sets OIDC claims (`sub`, `name`, `preferred_username`, `email`). UserInfo endpoint returns DB-authoritative claims.
4. **`SyncController.cs`** — In `Core.Server/Controllers/`, provides `GET changes`, `GET tree`, `POST reconcile` at `/api/v1/files/sync/`.
5. **`Program.cs`** — `OidcClientSeeder.SeedAsync()` called during startup.

## Required Client State

1. **`SettingsViewModel.cs`** — OAuth scopes: `openid`, `profile`, `offline_access`, `files:read`, `files:write`
2. **`OAuth2Service.cs`** — Scope negotiation, diagnostic logging, TLS bypass for local/LAN self-signed certs

## Resolved Issues (Compressed)

| # | Issue | Root Cause | Fix | Resolved |
|---|-------|-----------|-----|----------|
| 1 | `invalid_client` on authorize | `dotnetcloud-desktop` not registered | Added OIDC client seeder with upsert | 2026-03-07 |
| 2 | `invalid_scope` on authorize | `files:read`/`files:write` not registered | Added scope registration + client permissions | 2026-03-07 |
| 3 | `404` on `GET /connect/authorize` | Only `POST` mapped | Changed to `GET`+`POST` mapping | 2026-03-07 |
| 4 | Login redirect to wrong path | `/login` instead of `/auth/login` | Corrected redirect path | 2026-03-07 |
| 5 | Placeholder JSON on authenticated authorize | Not calling OpenIddict `SignIn` | Reworked passthrough to issue `SignIn` | 2026-03-07 |
| 6 | TLS errors on token exchange | Self-signed cert not trusted by client | Client-side bypass for local/LAN hosts | 2026-03-07 |
| 7 | Token JSON field mapping | Snake_case `access_token` etc. not mapped | Client DTO mapping + typed HttpClient | 2026-03-07 |
| 8 | `UserId = Guid.Empty` | Access tokens encrypted (JWE); no OIDC claims; no userinfo endpoint | `DisableAccessTokenEncryption()`, DB claim lookup, userinfo registration | 2026-03-08 |
| 9 | Sync endpoints `404` | `SyncController` in `Files.Host` (not loaded) | Added `SyncController` to `Core.Server` | 2026-03-08 |
| 10 | TLS errors on sync API calls | `DotNetCloudSync` named HttpClient had no cert bypass (only OAuth client had it) | Added `ConfigurePrimaryHttpMessageHandler(OAuthHttpClientHandlerFactory.CreateHandler)` to named client registration | 2026-03-08 |

## Current Verified State

### Client (Windows11-TestDNC, commit pending — TLS fix)

```json
{
  "Id": "b44b9f3f-fc25-45ae-9e7b-c3dca382f83d",
  "ServerBaseUrl": "https://mint22:15443",
  "UserId": "019cc1ac-da42-737c-b0ab-d0f2ecca8019",
  "DisplayName": "testdude@llabmik.net @ mint22",
  "AccountKey": "https://mint22:15443:019cc1ac-da42-737c-b0ab-d0f2ecca8019",
  "FullScanInterval": "00:05:00",
  "RegisteredAt": "2026-03-08T04:43:45.0755872Z"
}
```

**Client code changes (this commit):**
- `SyncServiceExtensions.cs` — Added `ConfigurePrimaryHttpMessageHandler(OAuthHttpClientHandlerFactory.CreateHandler)` to the `DotNetCloudSync` named HttpClient registration. This applies the same self-signed cert bypass used by OAuth2Service to all sync API HTTP calls.
- Build: 0 errors, 0 warnings. 24 SyncService tests pass.

### Server (mint22, commit `a52d16d`)

- Health: `https://localhost:15443/health/live` → `Healthy`
- Discovery: `userinfo_endpoint` advertised
- Sync endpoints: all return `403` without auth (correctly require bearer token)
- Build: 0 errors, 0 warnings. 305 server tests passed.

## Mediator Checklist (User)

1. Relay commit notification to the other agent.
2. Ask receiving agent to pull, build, and test.
3. Relay evidence back through this file.

## Mandatory End-Of-Handoff Relay Template

```markdown
## Mediator Relay Instructions

### Send to [Server|Client] Agent
<message text>

### Request Back
- commit hash
- raw endpoint/URL used
- raw error/query params
- raw log lines around the event (with timestamp)
```

---

## Mediator Relay Instructions

### Send to Server Agent

Two blocking contract mismatches found between client and server sync API:

**Fix 1 (REQUIRED): Remove `userId` query parameter from `SyncController` actions.**
The client sends bearer tokens with `sub` claim but does NOT send `userId` in query strings. The server's `SyncController` binds `userId = Guid.Empty` → `ToCaller()` throws → 403.

Fix: Add a `GetAuthenticatedCaller()` helper to `FilesControllerBase` that extracts userId from the bearer token's `sub` claim. Then update all three `SyncController` actions to use it instead of `[FromQuery] Guid userId`. See detailed code in "Active Blockers > Blocker 1" section above.

**Fix 2 (REQUIRED): Return raw data from `SyncController` (no envelope wrapper).**
The client's `DotNetCloudApiClient` deserializes responses as raw types (e.g. `List<SyncChangeResponse>`), not `{ success: true, data: [...] }`. The `Envelope()` wrapper in the current controller will cause deserialization to fail silently (all fields null/default) or throw.

Fix: Change `Ok(Envelope(changes))` to `Ok(changes)` in all three `SyncController` actions. The sync API is machine-to-machine (desktop clients), not browser-facing — envelopes add no value here.

After fixing: rebuild, run tests, push to main.

### Request Back
- commit hash
- confirmation that `GET /api/v1/files/sync/changes?since=2020-01-01T00:00:00Z` with a valid bearer token returns a raw JSON array (not envelope-wrapped)
- HTTP status code of the above request
- raw response body sample
```

## Mediator Relay Instructions

### Send to Client Agent
Sync endpoints were returning `404` because the `SyncController` only existed in the `Files.Host` assembly (not loaded by the server process). Fixed by adding a `SyncController` to the server project directly. All three sync endpoints are now live (`/api/v1/files/sync/changes`, `/api/v1/files/sync/tree`, `/api/v1/files/sync/reconcile`). They correctly return `403` when called without a bearer token.

Server redeployed on `mint22` at commit `a52d16d`. Please pull latest `main`, then run an end-to-end sync test. The SyncEngine should be able to call `GET /api/v1/files/sync/changes?since=...&userId=...` with the bearer token from the persisted account context. Even if the response is an empty changes list, a successful HTTP `200` confirms the full auth+sync pipeline is working.

### Request Back
- Client commit hash after pull.
- Raw SyncEngine log lines showing the sync poll request and response (with timestamps).
- HTTP status code from the `/api/v1/files/sync/changes` call.
- If sync poll succeeds: sample response body (even if empty `[]`).
- Any errors or unexpected behavior during sync.
