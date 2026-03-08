# Client/Server Mediation Handoff

Last updated: 2026-03-08 (mint22 server agent, sync contract fixes applied)

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
- **Sync API `userId` contract**: RESOLVED — server now derives caller user ID from bearer token claims (`sub`/`nameidentifier`) for all sync endpoints
- **Sync API response shape**: RESOLVED — sync endpoints now return raw payloads (`[]`/object) instead of envelope wrappers
- **Next milestone**: Client runs end-to-end sync verification against latest server commit and reports HTTP status/body evidence

## Server Resolution (Latest)

### Applied Fix 1: Removed `userId` query parameter requirement on sync endpoints

**Status:** RESOLVED

**What changed:** `SyncController` no longer accepts `[FromQuery] Guid userId` on `changes`, `tree`, or `reconcile`. All actions now call `GetAuthenticatedCaller()` and derive user context from bearer token claims.

**Current sync endpoint shapes:**
```
GET api/v1/files/sync/changes?since=2025-03-08T00:00:00.0000000Z
GET api/v1/files/sync/tree
GET api/v1/files/sync/tree?folderId={id}
POST api/v1/files/sync/reconcile  (body only)
```

**Files updated:**
- `src/Core/DotNetCloud.Core.Server/Controllers/FilesControllerBase.cs`
- `src/Core/DotNetCloud.Core.Server/Controllers/SyncController.cs`
- `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/FilesControllerBase.cs`
- `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/SyncController.cs`

### Applied Fix 2: Removed envelope wrapper from sync responses

**Status:** RESOLVED

**What changed:** `SyncController` now returns raw payloads via `Ok(changes)`, `Ok(tree)`, and `Ok(result)` instead of `Ok(Envelope(...))`.

**Sync responses now return:**
```json
[
  { "nodeId": "...", "name": "...", "isDeleted": false, ... }
]
```

### Validation Evidence (mint22)

- `dotnet build DotNetCloud.sln -c Release` -> success (0 errors, 0 warnings)
- `dotnet test tests/DotNetCloud.Core.Server.Tests/DotNetCloud.Core.Server.Tests.csproj -c Release --no-build` -> **305 passed**
- `dotnet test tests/DotNetCloud.Modules.Files.Tests/DotNetCloud.Modules.Files.Tests.csproj -c Release --no-build` -> **513 passed**
- Redeploy: `tools/redeploy-baremetal.sh` complete
- Health probe: `https://localhost:15443/health/live` -> `Healthy`
- Unauthenticated sync endpoints continue to return `403` (expected bearer requirement)
- Authenticated sync probe with live bearer token is pending client relay evidence

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
| 11 | Sync calls required `userId` query parameter | Server controller bound `userId` and rejected client calls that relied on bearer identity | Derived `CallerContext` from bearer claims and removed `userId` query requirement on sync endpoints | 2026-03-08 |
| 12 | Sync response deserialization mismatch | Server returned envelope-wrapped sync payloads; client expects raw JSON payloads | Changed sync responses from `Ok(Envelope(...))` to `Ok(...)` on all sync endpoints | 2026-03-08 |

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

### Server (mint22, commit pending push from this update)

- Health: `https://localhost:15443/health/live` → `Healthy`
- Discovery: `userinfo_endpoint` advertised
- Sync endpoints: no `userId` query parameter required; all return raw payloads
- Unauthenticated sync endpoints still return `403` (correctly require bearer token)
- Build: 0 errors, 0 warnings
- Tests: 305 server tests passed, 513 files-module tests passed

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

No additional server action pending for this relay. Server-side sync contract fixes are implemented and validated locally (build/tests/redeploy/health all green). Awaiting client end-to-end sync evidence on latest `main`.

### Request Back
- none (waiting for client verification relay)
```

## Mediator Relay Instructions

### Send to Client Agent
Server-side sync API contract fixes are now implemented and deployed on `mint22`.

What changed:
- `userId` query parameter requirement was removed from sync endpoints.
- Server now derives caller identity from bearer token claims (`sub`/`nameidentifier`).
- Sync responses are now raw JSON payloads (no `{ success, data }` envelope).

Please pull latest `main`, then run end-to-end sync verification from the desktop client. Use:
- `GET /api/v1/files/sync/changes?since=...` (no `userId` query parameter)
- with bearer token from persisted account context.

Expected success shape: HTTP `200` with a raw JSON array body (possibly empty `[]`).

### Request Back
- Client commit hash after pull.
- Raw SyncEngine log lines showing the sync poll request and response (with timestamps).
- HTTP status code from the `/api/v1/files/sync/changes` call.
- Raw response body sample from `/api/v1/files/sync/changes` (even if empty `[]`).
- Any errors or unexpected behavior during sync.
