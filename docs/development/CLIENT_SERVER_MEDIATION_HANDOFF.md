# Client/Server Mediation Handoff

Last updated: 2026-03-08 (compressed — all 22 issues resolved, end-to-end sync verified)

Purpose: Shared handoff between client-side and server-side agents, mediated by user.

> **Note:** This document was compressed on 2026-03-08 to remove resolved debugging evidence and verbose fix details.
> Full history is preserved in git (commits up to `6a9ccb0`).

## Process Rule (Mediator)

- All technical findings, raw evidence, and debugging conclusions must be written to this handoff document and pushed to `main`.
- Do not rely on chat-only technical status; chat to mediator should be minimal relay text only.
- Mediator role is relay-only (commit notifications and cross-agent request forwarding), not technical interpretation.

## Current Status

**MILESTONE: END-TO-END FILE SYNC WITH DIRECTORY HIERARCHY** — All 22 issues resolved. Full sync flow verified: OAuth login → token exchange → sync changes → tree → reconcile → chunk manifest → chunk download → file assembly. 7 files synced into correct directories (`clients/`, `Finance/`, `Pictures/`, `Test/`, root).

No open issues. No blockers.

## Current Verified State

### Server (mint22)

- **Build:** 0 errors, 0 warnings
- **Tests:** 304 server + 85 auth + 513 files = **902 passed**
- **Health:** `https://localhost:15443/health/live` → `Healthy`
- **Auth:** Persistent OIDC RSA keys (`oidc-keys/`), 14-day refresh token lifetime, OpenIddict bearer auth on all files/sync endpoints
- **API contracts:**
  - All sync/files endpoints derive caller from bearer token claims (`GetAuthenticatedCaller()`) — no `userId` query param
  - All endpoints return raw payloads (`Ok(data)`) — `ResponseEnvelopeMiddleware` handles wrapping
  - `SyncController.since` parameter converted to UTC kind before EF Core query
  - `ExecuteAsync` has general exception handler returning structured JSON on unhandled errors
  - `ResolvePublicLinkAsync` has `[AllowAnonymous]` for public share links

### Client (Windows11-TestDNC)

- **Build:** 0 errors, 0 warnings
- **Tests:** 53 Core + 24 SyncService + 24 SyncTray = **101 passed**
- **Sync:** 7 files synced into correct subdirectories with full directory hierarchy
- **Auth:** Token refresh implemented with `client_id`, `DateTimeOffset` for expiry, envelope unwrapping on all API responses
- **Sync engine:** Fetches folder tree → builds `nodeId→relativePath` map → creates directories → places files in correct paths

## Environment

- Client machine: `Windows11-TestDNC`
- Server machine: `mint22`
- Server URL: `https://mint22:15443/`
- Client sync directory: `C:\Users\benk\Documents\synctray`

## Required Server State

1. **`AuthServiceExtensions.cs`** — OpenIddict scopes: `openid`, `profile`, `email`, `offline_access`, `files:read`, `files:write`. Access token encryption disabled (`DisableAccessTokenEncryption()`). UserInfo endpoint registered. Persistent RSA keys from `{DOTNETCLOUD_DATA_DIR}/oidc-keys/`.
2. **`OidcClientSeeder.cs`** — `dotnetcloud-desktop` app registration with upsert behavior, permissions for `files:read`/`files:write`.
3. **`OpenIddictEndpointsExtensions.cs`** — Authorize endpoint looks up `ApplicationUser`, sets OIDC claims (`sub`, `name`, `preferred_username`, `email`). UserInfo endpoint returns DB-authoritative claims.
4. **`SyncController.cs`** — In `Core.Server/Controllers/`, provides `GET changes`, `GET tree`, `POST reconcile` at `/api/v1/files/sync/`. Converts `since` to UTC kind.
5. **`FilesControllerBase.cs`** — `[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]`. `ExecuteAsync` with general exception handler. `GetAuthenticatedCaller()` derives `CallerContext` from bearer claims.
6. **`Program.cs`** — `OidcClientSeeder.SeedAsync()` called during startup.

## Required Client State

1. **`SettingsViewModel.cs`** — OAuth scopes: `openid`, `profile`, `offline_access`, `files:read`, `files:write`
2. **`OAuth2Service.cs`** — Scope negotiation, diagnostic logging, TLS bypass for local/LAN self-signed certs
3. **`DotNetCloudApiClient.cs`** — Envelope unwrapping via `ReadEnvelopeDataAsync<T>()` on all API responses. `RefreshTokenAsync` sends `client_id`.
4. **`SyncEngine.cs`** — `RefreshAccessTokenAsync` calls `_api.RefreshTokenAsync()`. `ApplyRemoteChangesAsync` builds `nodeId→relativePath` map from folder tree. `DateTimeOffset` for token expiry.

## Resolved Issues (All 22)

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
| 10 | TLS errors on sync API calls | `DotNetCloudSync` named HttpClient had no cert bypass | Added cert bypass to named client registration | 2026-03-08 |
| 11 | Sync calls required `userId` query param | Server controller bound `userId` from query string | Derived `CallerContext` from bearer claims; removed `userId` param | 2026-03-08 |
| 12 | Sync response deserialization mismatch | Server returned envelope-wrapped sync payloads | Changed sync responses from `Ok(Envelope(...))` to `Ok(...)` | 2026-03-08 |
| 13 | Token refresh was a stub | `RefreshAccessTokenAsync` did nothing | Implemented actual refresh: API call → save tokens → update accessor | 2026-03-08 |
| 14 | Missing `client_id` in refresh request | OpenIddict requires `client_id` for public clients | Added `clientId` parameter to `RefreshTokenAsync` | 2026-03-08 |
| 15 | `DateTime` serialization bug — tokens appear unexpired | `DateTimeKind` lost after JSON roundtrip | Changed `ExpiresAt` from `DateTime` to `DateTimeOffset` across client chain | 2026-03-08 |
| 16 | Refresh token `invalid_grant` | Ephemeral RSA keys regenerated on every restart | Created `OidcKeyManager` for persistent PEM key files; fixed config key names | 2026-03-08 |
| 17 | Sync API returns 403 with valid bearer token | No `[Authorize]` attribute; default auth scheme was cookies | Added OpenIddict bearer `[Authorize]` to `FilesControllerBase` | 2026-03-08 |
| 18 | Files API returns 403 "Caller user ID does not match" | 20 `FilesController` endpoints used `[FromQuery] Guid userId` | Changed all to `GetAuthenticatedCaller()`; removed `userId` param | 2026-03-08 |
| 19 | Files API responses double-envelope wrapped | `Ok(Envelope(data))` + `ResponseEnvelopeMiddleware` | Removed `Envelope()` calls; middleware handles wrapping | 2026-03-08 |
| 20 | Sync changes endpoint returns 500 | `since` parsed as `DateTime Kind=Unspecified`; Npgsql rejects for `timestamptz` | `DateTime.SpecifyKind(since, DateTimeKind.Utc)`; added general exception handler | 2026-03-08 |
| 21 | Chunk manifest deserialization failure | Server returns `string[]`; client expected object with `Chunks`+`TotalSize` | Client deserializes `List<string>` and maps to `ChunkManifestResponse` | 2026-03-08 |
| 22 | Sync flattens directory structure | `ResolveLocalPathAsync` used filename only, ignoring `ParentId` | Client fetches folder tree, builds `nodeId→path` map, creates dirs before files | 2026-03-08 |

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
