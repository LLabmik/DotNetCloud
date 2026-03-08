# Client/Server Mediation Handoff

Last updated: 2026-03-08 (client agent, client-side token fixes + refresh token blocker)

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
- **Next milestone**: Server investigates refresh token `invalid_grant` — client token refresh code is confirmed working but the server rejects the stored refresh token as invalid

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
| 13 | Token refresh was a stub | `SyncEngine.RefreshAccessTokenAsync` had a comment "Token refresh is handled externally" and did nothing — expired tokens were never refreshed | Implemented actual refresh: calls `_api.RefreshTokenAsync()`, saves new tokens via `_tokenStore.SaveAsync()`, updates `_api.AccessToken` | 2026-03-08 |
| 14 | Missing `client_id` in refresh request | `RefreshTokenAsync` did not send `client_id` in the form body; OpenIddict requires it for public clients | Added `clientId` parameter to `IDotNetCloudApiClient.RefreshTokenAsync` and implementation; created `OAuthConstants.ClientId = "dotnetcloud-desktop"` | 2026-03-08 |
| 15 | `DateTime` serialization bug — tokens appear unexpired | `TokenInfo.ExpiresAt` was `DateTime`. After JSON roundtrip through `EncryptedFileTokenStore`, the `DateTimeKind` was lost (became `Unspecified`/`Local`), making `DateTime.UtcNow >= ExpiresAt` return `False` for genuinely expired tokens | Changed `ExpiresAt` from `DateTime` to `DateTimeOffset` across entire client chain (`TokenInfo`, `AddAccountRequest`, `AddAccountData` IPC model, `OAuth2Service`, `SyncEngine`, all tests) | 2026-03-08 |

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

## Current Blocker: Refresh Token Invalid on Server

**Issue #16 (OPEN):** Server returns `invalid_grant` / "The specified token is invalid." when client attempts to refresh an expired access token.

**Request to server agent:**

1. **Check OpenIddict refresh token lifetime configuration.** The original tokens were issued around `2026-03-08 05:43 UTC`. By `2026-03-08 22:00+ UTC` (~17 hours later), the refresh token is rejected. What is the configured refresh token lifetime? For a desktop sync client, refresh tokens should have a much longer lifetime (days/weeks) or use sliding expiration.

2. **Check if `offline_access` scope grants are configured correctly.** The client requests `offline_access` scope. Verify:
   - `offline_access` is in the registered scopes for the `dotnetcloud-desktop` application
   - The authorization endpoint actually issues refresh tokens when `offline_access` is requested
   - Refresh token rolling/reuse policy (if rolling, the old token is revoked after first use — but the client never got to use it before the access token expired)

3. **Check if the token was stored/persisted in the OpenIddict token store.** Query the `OpenIddictTokens` table for tokens associated with the test user (`019cc1ac-da42-737c-b0ab-d0f2ecca8019`) and check their status, expiry, and revocation state.

4. **Consider increasing refresh token lifetime** for the `dotnetcloud-desktop` client. Recommended: 14-30 days with absolute expiration, sliding expiration enabled. A 5-minute sync polling interval means the access token will expire frequently and refresh must be reliable.

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

Client-side token handling is now fully fixed and verified (101 tests pass, build clean). Three bugs were found and resolved:

1. **Token refresh was a stub** — now fully implemented
2. **Missing `client_id` in refresh request** — now included
3. **`DateTime` serialization lost UTC kind** — migrated to `DateTimeOffset` (this was the root cause of `IsExpired` returning `False` for genuinely expired tokens, which caused the original 403)

With these fixes, the client correctly detects the expired access token and attempts a refresh. However, the server returns **400 `invalid_grant`** with `"The specified token is invalid."` when the client sends the stored refresh token.

**Server action required:**
- Investigate OpenIddict refresh token lifetime configuration
- Check if `offline_access` scope is properly granted and refresh tokens are actually being issued
- Query `OpenIddictTokens` table for the test user's token status
- Consider setting refresh token lifetime to 14-30 days for the `dotnetcloud-desktop` client (desktop sync clients need long-lived refresh tokens)
- After fix: no client-side changes needed — the client refresh code is ready and will work once the server accepts the refresh token

**User note:** The user reported they were NOT prompted for re-login when the tray attempted re-authentication. This may indicate the server has a session cookie from the original login that auto-approves without showing a login form, but the resulting tokens still have the same short refresh token lifetime.

### Request Back
- OpenIddict refresh token lifetime configuration (current value)
- `OpenIddictTokens` table query results for user `019cc1ac-da42-737c-b0ab-d0f2ecca8019` (token status, expiry, type)
- Whether `offline_access` scope is being granted and refresh tokens issued
- Configuration change applied (if any) with commit hash
- Confirmation that `POST /connect/token` with `grant_type=refresh_token` returns 200 for a valid refresh token
