# Client/Server Mediation Handoff

Last updated: 2026-03-08 (server agent — Issues #18/#19 resolved, deployed)

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
- **Sync API response shape**: RESOLVED — sync endpoints return `Ok(data)` without `Envelope()` calls; `ResponseEnvelopeMiddleware` wraps all `/api/` responses automatically; client unwraps the envelope
- **Refresh token `invalid_grant`**: RESOLVED — ephemeral keys replaced with persistent RSA key files; tokens survive restarts
- **Sync bearer auth 403**: RESOLVED — added `[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]` to `FilesControllerBase`; unauthenticated requests now return 401 (not 403 ForbiddenException)
- **Sync API response envelope**: RESOLVED (client-side) — server's `ResponseEnvelopeMiddleware` wraps all `/api/` responses in `{"success":true,"data":...}`; client now unwraps the envelope before deserializing
- **Files API `userId` contract**: RESOLVED (Issue #18) — all `FilesController` endpoints now use `GetAuthenticatedCaller()` from bearer token claims; `[FromQuery] Guid userId` removed from all 20 endpoints
- **Files API response envelope**: RESOLVED (Issue #19) — `FilesController` endpoints now return raw payloads via `Ok(data)` instead of `Ok(Envelope(data))`; `ResponseEnvelopeMiddleware` handles wrapping automatically
- **Next milestone**: Client can now complete file download sync flow — sync changes/tree/reconcile (200) and files chunks/download endpoints no longer require `userId` or double-wrap responses

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

### Applied Fix 3: Persistent OpenIddict signing/encryption keys

**Status:** RESOLVED

**Root cause:** `AddEphemeralEncryptionKey()` and `AddEphemeralSigningKey()` generate new in-memory RSA keys on every server restart. After `redeploy-baremetal.sh`, OpenIddict cannot decrypt stored refresh token payloads → `invalid_grant` (400).

**What changed:**
- Created `OidcKeyManager` utility class that generates RSA-2048 keys and persists them as PEM files with owner-only permissions (600)
- `AuthServiceExtensions.cs` now loads persistent keys from `{DOTNETCLOUD_DATA_DIR}/oidc-keys/` instead of calling `AddEphemeralEncryptionKey()`/`AddEphemeralSigningKey()`
- Keys are generated once on first startup, then reused across all subsequent restarts
- Fixed config key name mismatch: `AccessTokenLifetime`/`RefreshTokenLifetime` → `AccessTokenLifetimeMinutes`/`RefreshTokenLifetimeDays` (old names were silently ignored)
- Set `RefreshTokenLifetimeDays` to 14 (was effectively 7 via default)
- Purged 20 orphaned tokens and 8 authorizations from DB that were encrypted with defunct ephemeral keys

**Key persistence verified:**
```
# Before restart
daf48fdccdd693ca  encryption-key.pem
ec42f03569d6e2c3  signing-key.pem
# After restart (same checksums)
daf48fdccdd693ca  encryption-key.pem
ec42f03569d6e2c3  signing-key.pem
```

**Files created:**
- `src/Core/DotNetCloud.Core.Auth/Security/OidcKeyManager.cs`

**Files updated:**
- `src/Core/DotNetCloud.Core.Auth/Extensions/AuthServiceExtensions.cs`
- `src/Core/DotNetCloud.Core.Server/appsettings.json`
- `src/Core/DotNetCloud.Core.Server/appsettings.Development.json`

### Applied Fix 4: Bearer auth on FilesControllerBase

**Status:** RESOLVED

**Root cause:** `FilesControllerBase` (parent of `SyncController` and `FilesController`) had no `[Authorize]` attribute. Without it, ASP.NET Core's authentication middleware never ran for these controllers. Additionally, the default auth scheme was `Identity.Application` (cookies), not OpenIddict bearer — so even a plain `[Authorize]` would have used cookie auth, ignoring the `Bearer` header.

**What changed:**
- Added `[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]` to `FilesControllerBase` in Core.Server
- Both `SyncController` and `FilesController` inherit this attribute from the base class
- Added `[Authorize]` to `FilesControllerBase` in Files.Host (for future standalone module use)
- `PublicShareController` in Files.Host already has `[AllowAnonymous]` — unaffected
- Unauthenticated requests now return **401** (OpenIddict validation challenge) instead of 403 (ForbiddenException from controller code)

**Behavior change:** Unauthenticated sync/files requests previously returned `403` with `{"code":"AUTH_FORBIDDEN","message":"Authentication is required."}`. They now return **401** (standard OpenIddict challenge). Client error handling should account for both 401 and 403.

**Files updated:**
- `src/Core/DotNetCloud.Core.Server/Controllers/FilesControllerBase.cs`
- `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/FilesControllerBase.cs`

### Applied Fix 5: Removed `userId` query parameter from all FilesController endpoints (Issue #18)

**Status:** RESOLVED

**What changed:** All 20 authenticated `FilesController` endpoints previously accepted `[FromQuery] Guid userId` and called `ToCaller(userId)`. This is the same pattern that was fixed on `SyncController` in Issue #11. All endpoints now call `GetAuthenticatedCaller()` to derive `CallerContext` from bearer token claims.

Additionally, `ResolvePublicLinkAsync` (public share link resolution) was given `[AllowAnonymous]` since it's a public endpoint that shouldn't require authentication — the base class `[Authorize]` attribute would have blocked unauthenticated access.

**Files updated:**
- `src/Core/DotNetCloud.Core.Server/Controllers/FilesController.cs`
- `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/FilesController.cs`
- `tests/DotNetCloud.Core.Server.Tests/Controllers/FilesControllerTests.cs` (updated all 23 test method calls, removed now-irrelevant `CallerUserDoesNotMatchAuthenticatedUser` test)

### Applied Fix 6: Removed Envelope() calls from all FilesController responses (Issue #19)

**Status:** RESOLVED

**What changed:** All `FilesController` endpoints previously called `Ok(Envelope(data))` which double-wrapped responses because `ResponseEnvelopeMiddleware` also wraps all `/api/` responses. All endpoints now return raw payloads: `Ok(data)`, `Created(url, data)`, or `Ok(new { ... })`.

**Files API responses now return (after middleware wrapping):**
```json
{"success":true,"data":[...]}
```

Instead of the previous double-wrap:
```json
{"success":true,"data":{"success":true,"data":[...]}}
```

**Files updated:**
- `src/Core/DotNetCloud.Core.Server/Controllers/FilesController.cs`
- `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/FilesController.cs`

### Validation Evidence (mint22)

- `dotnet build DotNetCloud.sln -c Release` -> success (0 errors, 0 warnings)
- `dotnet test tests/DotNetCloud.Core.Server.Tests/` -> **304 passed**
- `dotnet test tests/DotNetCloud.Modules.Files.Tests/` -> **513 passed**
- `dotnet test tests/DotNetCloud.Core.Auth.Tests/` -> **85 passed**
- Redeploy: `tools/redeploy-baremetal.sh` complete
- Health probe: `https://localhost:15443/health/live` -> `Healthy`

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
| 13 | Token refresh was a stub | `SyncEngine.RefreshAccessTokenAsync` had a comment "Token refresh is handled externally" and did nothing — expired tokens were never refreshed | Implemented actual refresh: calls `_api.RefreshTokenAsync()`, saves new tokens via `_tokenStore.SaveAsync()`, updates `_api.AccessToken` | 2026-03-08 |
| 14 | Missing `client_id` in refresh request | `RefreshTokenAsync` did not send `client_id` in the form body; OpenIddict requires it for public clients | Added `clientId` parameter to `IDotNetCloudApiClient.RefreshTokenAsync` and implementation; created `OAuthConstants.ClientId = "dotnetcloud-desktop"` | 2026-03-08 |
| 15 | `DateTime` serialization bug — tokens appear unexpired | `TokenInfo.ExpiresAt` was `DateTime`. After JSON roundtrip through `EncryptedFileTokenStore`, the `DateTimeKind` was lost (became `Unspecified`/`Local`), making `DateTime.UtcNow >= ExpiresAt` return `False` for genuinely expired tokens | Changed `ExpiresAt` from `DateTime` to `DateTimeOffset` across entire client chain (`TokenInfo`, `AddAccountRequest`, `AddAccountData` IPC model, `OAuth2Service`, `SyncEngine`, all tests) | 2026-03-08 |
| 16 | Refresh token `invalid_grant` | `AddEphemeralEncryptionKey()`/`AddEphemeralSigningKey()` generate new in-memory RSA keys on every server restart; OpenIddict cannot decrypt stored refresh token payloads after restart | Created `OidcKeyManager` to persist RSA keys as PEM files; replaced ephemeral with persistent keys; fixed config key name mismatch; increased refresh lifetime to 14 days; purged orphaned tokens | 2026-03-08 |
| 17 | Sync API returns 403 with valid bearer token | `SyncController` has no `[Authorize]` attribute, so ASP.NET Core auth middleware never runs; default auth scheme is `Identity.Application` (cookies) not OpenIddict bearer, so even with `[Authorize]` it would try cookie auth | Added `[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]` to `FilesControllerBase` (inherited by `SyncController` and `FilesController`); also added `[Authorize]` to Files.Host `FilesControllerBase` | 2026-03-08 |
| 18 | Files API returns 403 "Caller user ID does not match" | All 20 authenticated `FilesController` endpoints accept `[FromQuery] Guid userId` and call `ToCaller(userId)`. Client sends bearer token but no `userId` query param → server receives `userId=Guid.Empty` → doesn't match JWT `sub` claim | Changed all `FilesController` endpoints to use `GetAuthenticatedCaller()` (same fix as Issue #11). Removed `[FromQuery] Guid userId` from all endpoints. Added `[AllowAnonymous]` to `ResolvePublicLinkAsync`. Both `Core.Server` and `Files.Host` controllers updated. | 2026-03-08 |
| 19 | Files API responses double-envelope wrapped | `FilesController` endpoints call `Ok(Envelope(data))`, but `ResponseEnvelopeMiddleware` also wraps `/api/` responses → `{"success":true,"data":{"success":true,"data":...}}` | Removed `Envelope()` calls from all `FilesController` endpoints. Endpoints now return `Ok(data)` / `Created(url, data)`. Middleware handles wrapping automatically. Both `Core.Server` and `Files.Host` controllers updated. | 2026-03-08 |

## Current Verified State

### Client (Windows11-TestDNC, commit pending — envelope unwrap + Issue #17 verification)

**Build:** 0 errors, 0 warnings
**Tests:** 53 Core + 24 SyncService + 24 SyncTray = **101 passed**

**Client-side fixes applied this session:**

1. **Token refresh implementation** (`SyncEngine.cs`):
   - `RefreshAccessTokenAsync` now calls `_api.RefreshTokenAsync(refreshToken, clientId)`, saves refreshed tokens via `_tokenStore.SaveAsync()`, and sets `_api.AccessToken` to the new value
   - Includes diagnostic logging of token state before refresh attempts

2. **`client_id` in refresh request** (`DotNetCloudApiClient.cs`, `IDotNetCloudApiClient.cs`):
   - `RefreshTokenAsync` now accepts and sends `client_id` parameter (required by OpenIddict for public clients)
   - New `OAuthConstants.cs` file with `ClientId = "dotnetcloud-desktop"`

3. **`DateTime` → `DateTimeOffset` migration** (root cause of tokens appearing unexpired):
   - `TokenInfo.ExpiresAt`: `DateTime` → `DateTimeOffset`
   - `TokenInfo.IsExpired`: `DateTime.UtcNow` → `DateTimeOffset.UtcNow`
   - `AddAccountRequest.ExpiresAt`: `DateTime` → `DateTimeOffset`
   - `AddAccountData.ExpiresAt` (IPC protocol): `DateTime` → `DateTimeOffset`
   - `OAuth2Service.MapTokenResponse`: `DateTime.UtcNow` → `DateTimeOffset.UtcNow`
   - `SyncEngine` refresh path: `DateTime.UtcNow` → `DateTimeOffset.UtcNow`
   - All 5 test files updated to use `DateTimeOffset`

4. **Diagnostic response body in RefreshTokenAsync** (`DotNetCloudApiClient.cs`):
   - On non-success status, reads response body and includes it in the exception message for debugging

5. **Server response envelope unwrapping** (`DotNetCloudApiClient.cs`):
   - Added `ReadEnvelopeDataAsync<T>()` method that detects `{"success":true,"data":...}` envelope format
   - Extracts `.data` property for deserialization; falls back to root if no envelope detected
   - Applied to `GetAsync<T>`, `PostJsonAsync<T>`, `PutJsonAsync<T>` (all `/api/` endpoints)
   - NOT applied to `PostFormAsync<T>` (used for OAuth `/connect/token` which isn't envelope-wrapped)

**Issue #17 verification evidence (2026-03-08):**

Sync API now returns **200** with valid bearer token (was 403):
```
info: Token state for context 16ce0169-59b1-4895-b4d9-b5e07b8b433b: IsExpired=False, CanRefresh=True, ExpiresAt=03/08/2026 07:14:53 +00:00.
info: Start processing HTTP request GET https://mint22:15443/api/v1/files/sync/changes?*
info: Received HTTP response headers after 542.2632ms - 200
info: End processing HTTP request after 561.0684ms - 200
```

Sync changes deserialized successfully after envelope unwrapping. Sync flow progresses to tree and reconcile endpoints (both 200), then attempts file download via `GET api/v1/files/{nodeId}/chunks` which returns **403** (Issue #18).

**Issue #18 evidence (2026-03-08):**

```
info: Start processing HTTP request GET https://mint22:15443/api/v1/files/80147381-5315-4e66-879d-8e533d056ff9/chunks
info: Received HTTP response headers after 3.322ms - 403
fail: HTTP 403 on GET api/v1/files/{nodeId}/chunks. Body: {"success":false,"error":{"code":"AUTH_FORBIDDEN","message":"Caller user ID does not match the authenticated identity."}}
```

Root cause: `FilesController.GetChunkManifestAsync` accepts `[FromQuery] Guid userId` and calls `ToCaller(userId)`. Client doesn't send `userId` query parameter → server receives `Guid.Empty` → doesn't match JWT `sub` claim. Same pattern as Issue #11 (fixed for `SyncController`, not yet applied to `FilesController`).

### Server (mint22, Issues #18/#19 deployed)

- Health: `https://localhost:15443/health/live` → `Healthy`
- Build: 0 errors, 0 warnings
- Tests: 304 server + 85 auth + 513 files = **902 passed** (0 failures)
- `FilesController` endpoints no longer require `userId` query parameter — all use `GetAuthenticatedCaller()`
- `FilesController` endpoints return raw payloads — no `Envelope()` calls; middleware handles wrapping
- `ResolvePublicLinkAsync` has `[AllowAnonymous]` (public share links don't require auth)
- Persistent OIDC keys verified, config fixed, refresh token lifetime 14 days
- All sync and files endpoints use OpenIddict bearer auth via `FilesControllerBase`

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

Issues #18 and #19 need server-side fixes. These are the same patterns as Issues #11 and #12 (already fixed for `SyncController`), now applied to `FilesController`.

**Issue #18: `FilesController` endpoints require redundant `userId` query parameter**

All 20 authenticated endpoints in `FilesController` (both `Core.Server/Controllers/FilesController.cs` and `Files.Host/Controllers/FilesController.cs`) accept `[FromQuery] Guid userId` and call `ToCaller(userId)`. The client sends a bearer token but no `userId` query param → server receives `Guid.Empty` → comparison fails → 403.

**Requested fix (same as SyncController Issue #11):**
- Change all `FilesController` endpoints to use `GetAuthenticatedCaller()` instead of `ToCaller(userId)`
- Remove `[FromQuery] Guid userId` parameter from all endpoint signatures
- Apply to both `Core.Server/Controllers/FilesController.cs` and `Files.Host/Controllers/FilesController.cs`
- `GetAuthenticatedCaller()` already exists in `FilesControllerBase` and extracts user identity from the JWT `sub`/`NameIdentifier` claim

**Affected endpoints (all 20 authenticated ones):**
1. `GET api/v1/files` (ListChildren)
2. `GET api/v1/files/{nodeId}` (GetNode)
3. `POST api/v1/files/folders` (CreateFolder)
4. `PUT api/v1/files/{nodeId}/rename` (Rename)
5. `PUT api/v1/files/{nodeId}/move` (Move)
6. `POST api/v1/files/{nodeId}/copy` (Copy)
7. `DELETE api/v1/files/{nodeId}` (Delete)
8. `POST api/v1/files/{nodeId}/favorite` (ToggleFavorite)
9. `GET api/v1/files/favorites` (ListFavorites)
10. `GET api/v1/files/recent` (ListRecent)
11. `GET api/v1/files/search` (Search)
12. `POST api/v1/files/upload/initiate` (InitiateUpload)
13. `PUT api/v1/files/upload/{sessionId}/chunks/{chunkHash}` (UploadChunk)
14. `POST api/v1/files/upload/{sessionId}/complete` (CompleteUpload)
15. `DELETE api/v1/files/upload/{sessionId}` (CancelUpload)
16. `GET api/v1/files/upload/{sessionId}` (GetUploadSession)
17. `GET api/v1/files/{nodeId}/download` (Download)
18. `GET api/v1/files/{nodeId}/chunks` (GetChunkManifest)
19. `GET api/v1/files/chunks/{chunkHash}` (DownloadChunkByHash)
20. `GET api/v1/files/shared-with-me` (SharedWithMe)

**Issue #19: `FilesController` endpoints double-envelope responses**

`FilesController` endpoints call `Ok(Envelope(data))`, but `ResponseEnvelopeMiddleware` also wraps all `/api/` responses → double envelope: `{"success":true,"data":{"success":true,"data":...}}`.

**Requested fix (same as SyncController Issue #12):**
- Change all `Ok(Envelope(data))` calls to `Ok(data)` in `FilesController`
- The `ResponseEnvelopeMiddleware` already handles wrapping automatically
- Apply to both `Core.Server/Controllers/FilesController.cs` and `Files.Host/Controllers/FilesController.cs`

**Client evidence:**
- Sync endpoints return HTTP 200 ✓ (Issue #17 verified resolved)
- Sync changes/tree/reconcile deserialization works after client-side envelope unwrapping
- File download endpoints return HTTP 403 ✗ due to Issue #18
- Token state is valid (not expired, can refresh)
- Client build: 0 errors, 0 warnings; Tests: 101 passed
- Client commit hash will be provided after push

### Request Back
- Confirmation that `FilesController` uses `GetAuthenticatedCaller()` (no `userId` query param)
- Confirmation that `FilesController` returns `Ok(data)` (no `Envelope()` calls)
- Build + test results
- Commit hash
- Verification: `curl -k -H "Authorization: Bearer <token>" "https://localhost:15443/api/v1/files/{nodeId}/chunks"` returns 200 (not 403)
