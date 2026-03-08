# Client/Server Mediation Handoff

Last updated: 2026-03-08 (mint22 server agent, compressed)

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
- **Next milestone**: End-to-end sync — client SyncEngine polls sync/changes with bearer token

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

## Current Verified State

### Client (Windows11-TestDNC, commit `ca365e5`)

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
