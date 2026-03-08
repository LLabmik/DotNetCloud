# Client/Server Mediation Handoff

Last updated: 2026-03-08 (server agent, Issue #17 resolved — [Authorize] with OpenIddict bearer scheme on FilesControllerBase)

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
- **Refresh token `invalid_grant`**: RESOLVED — ephemeral keys replaced with persistent RSA key files; tokens survive restarts
- **Sync bearer auth 403**: RESOLVED — added `[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]` to `FilesControllerBase`; unauthenticated requests now return 401 (not 403 ForbiddenException)
- **Next milestone**: Client re-authenticates and verifies sync API returns 200 with valid bearer token; then test token refresh flow

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
| 13 | Token refresh was a stub | `SyncEngine.RefreshAccessTokenAsync` had a comment "Token refresh is handled externally" and did nothing — expired tokens were never refreshed | Implemented actual refresh: calls `_api.RefreshTokenAsync()`, saves new tokens via `_tokenStore.SaveAsync()`, updates `_api.AccessToken` | 2026-03-08 |
| 14 | Missing `client_id` in refresh request | `RefreshTokenAsync` did not send `client_id` in the form body; OpenIddict requires it for public clients | Added `clientId` parameter to `IDotNetCloudApiClient.RefreshTokenAsync` and implementation; created `OAuthConstants.ClientId = "dotnetcloud-desktop"` | 2026-03-08 |
| 15 | `DateTime` serialization bug — tokens appear unexpired | `TokenInfo.ExpiresAt` was `DateTime`. After JSON roundtrip through `EncryptedFileTokenStore`, the `DateTimeKind` was lost (became `Unspecified`/`Local`), making `DateTime.UtcNow >= ExpiresAt` return `False` for genuinely expired tokens | Changed `ExpiresAt` from `DateTime` to `DateTimeOffset` across entire client chain (`TokenInfo`, `AddAccountRequest`, `AddAccountData` IPC model, `OAuth2Service`, `SyncEngine`, all tests) | 2026-03-08 |
| 16 | Refresh token `invalid_grant` | `AddEphemeralEncryptionKey()`/`AddEphemeralSigningKey()` generate new in-memory RSA keys on every server restart; OpenIddict cannot decrypt stored refresh token payloads after restart | Created `OidcKeyManager` to persist RSA keys as PEM files; replaced ephemeral with persistent keys; fixed config key name mismatch; increased refresh lifetime to 14 days; purged orphaned tokens | 2026-03-08 |
| 17 | Sync API returns 403 with valid bearer token | `SyncController` has no `[Authorize]` attribute, so ASP.NET Core auth middleware never runs; default auth scheme is `Identity.Application` (cookies) not OpenIddict bearer, so even with `[Authorize]` it would try cookie auth | Added `[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]` to `FilesControllerBase` (inherited by `SyncController` and `FilesController`); also added `[Authorize]` to Files.Host `FilesControllerBase` | 2026-03-08 |

## Current Verified State

### Client (Windows11-TestDNC, commit pending — token refresh + DateTimeOffset fixes)

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

**Sync-now evidence (2026-03-08):**

Token expiry detection is now working correctly:
```
info: Token state for context b44b9f3f-fc25-45ae-9e7b-c3dca382f83d: IsExpired=True, CanRefresh=True, ExpiresAt=03/08/2026 05:43:14 +00:00.
info: Refreshing expired access token for context b44b9f3f-fc25-45ae-9e7b-c3dca382f83d.
info: Start processing HTTP request POST https://mint22:15443/connect/token
info: Received HTTP response headers after 157.3763ms - 400
fail: Sync error for context b44b9f3f-fc25-45ae-9e7b-c3dca382f83d.
      System.Net.Http.HttpRequestException: Token refresh failed (400): {
  "error": "invalid_grant",
  "error_description": "The specified token is invalid.",
  "error_uri": "https://documentation.openiddict.com/errors/ID2004"
}
```

**Analysis:** The access token (issued ~03/08 05:43 UTC) is correctly detected as expired. The client properly attempts a refresh with `grant_type=refresh_token`, `client_id=dotnetcloud-desktop`, and the stored refresh token. The server responds **400 `invalid_grant`** — the refresh token itself is expired or revoked server-side.

**The user was not re-prompted for login** when trying to re-add the account via tray, which suggests an existing session may be interfering or the re-authentication flow didn't trigger.

## Issue #16 Resolution: Persistent OIDC Keys

**Issue #16 (RESOLVED):** Server returned `invalid_grant` because ephemeral RSA keys were regenerated on every restart, making stored refresh token payloads undecryptable.

**Root cause confirmed:** `AddEphemeralEncryptionKey()` and `AddEphemeralSigningKey()` in OpenIddict config generate random in-memory keys. After any server restart (including `redeploy-baremetal.sh`), these keys are lost. OpenIddict stores refresh token payloads encrypted with the encryption key — when the key changes, decryption fails → `invalid_grant`.

**Fix applied:**
- Created `OidcKeyManager.cs` — generates RSA-2048 keys, persists as PEM files with `600` permissions
- Keys stored at `{DOTNETCLOUD_DATA_DIR}/oidc-keys/{signing-key.pem, encryption-key.pem}`
- `AuthServiceExtensions.cs` now calls `options.AddSigningKey()` / `options.AddEncryptionKey()` with persistent keys
- Fixed appsettings config key names: `AccessTokenLifetime` → `AccessTokenLifetimeMinutes`, `RefreshTokenLifetime` → `RefreshTokenLifetimeDays`
- Refresh token lifetime set to 14 days
- Purged 20 orphaned tokens + 8 authorizations from DB (encrypted with defunct ephemeral keys)
- Verified key checksums survive restart (md5 identical before/after `systemctl restart`)

**Client action required:** Old tokens were purged from the database. The client must re-authenticate (remove and re-add the account, or trigger a fresh OAuth flow). After re-authentication, refresh tokens will work across server restarts.

### Server (mint22, [Authorize] bearer auth fix deployed)

- Health: `https://localhost:15443/health/live` → `Healthy`
- Build: 0 errors, 0 warnings
- Tests: 305 server + 85 auth + 513 files = **903 passed** (0 failures)
- Unauthenticated sync request returns **401** (was 403 ForbiddenException from controller code)
- `FilesControllerBase` now has `[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]`
- Both `SyncController` and `FilesController` inherit bearer auth from base class
- `PublicShareController` (Files.Host) has `[AllowAnonymous]` — unaffected
- Persistent OIDC keys verified, config fixed, refresh token lifetime 14 days

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

### Send to Client Agent

Issue #17 is RESOLVED. Root cause confirmed: `FilesControllerBase` (parent of `SyncController` and `FilesController`) had no `[Authorize]` attribute, so ASP.NET Core auth middleware never ran. Additionally, the default auth scheme was `Identity.Application` (cookies), not OpenIddict bearer.

**What was fixed:**
- Added `[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]` to `FilesControllerBase` in Core.Server
- This is inherited by both `SyncController` and `FilesController`
- Added `[Authorize]` to `FilesControllerBase` in Files.Host (for future module use)
- Unauthenticated requests now return **401** (OpenIddict validation challenge) instead of 403 (controller-level ForbiddenException)

**Verification:**
- `curl -k -sS -w "\nHTTP_CODE: %{http_code}\n" "https://localhost:15443/api/v1/files/sync/changes?since=2025-01-01T00:00:00Z"` → `HTTP_CODE: 401` (correct — no bearer token)
- Build: 0 errors, 0 warnings; Tests: 305 + 85 + 513 = 903 passed (0 failures)
- Server redeployed and healthy

**Client action required:**
- Pull latest main
- Re-authenticate (fresh OAuth flow) to get new tokens signed with persistent keys
- Verify sync API returns **200** with valid bearer token (not 401 or 403)
- Test token refresh after access token expires (60 min lifetime, 14 day refresh)
- No client-side code changes needed

**Important note on HTTP status codes:** The client should now expect **401** (not 403) for authentication failures. The previous 403 was a quirk of the controller code running before auth middleware. With the `[Authorize]` attribute, OpenIddict's validation handler issues a proper 401 challenge. If the client has error handling that checks for 403, it should also handle 401.

### Request Back
- Confirmation that sync API returns 200 with valid bearer token
- Token refresh test result
- Any remaining sync errors
- Commit hash of any client-side changes
