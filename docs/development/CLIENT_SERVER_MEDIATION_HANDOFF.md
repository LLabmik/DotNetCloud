# Client/Server Mediation Handoff

Last updated: 2026-03-07 (Windows11-TestDNC client agent)

Purpose: Shared handoff between client-side and server-side agents, mediated by user.

## Current Incident

- Symptom: Browser fails during SyncTray add-account with HTTP `404` on `/connect/authorize`.
- Environment:
  - Client machine: `Windows11-TestDNC`
  - Server machine: `mint22`

## Confirmed Facts

- `client_id=dotnetcloud-desktop` is now recognized by server (no longer invalid client id).
- Latest observed failure is HTTP `404` at `https://mint22:15443/connect/authorize?...` (no OAuth callback query error returned).
- SyncTray IPC + onboarding launch are functioning.

## Required Server State

Server must run code containing all of the following:

1. `src/Core/DotNetCloud.Core.Auth/Extensions/AuthServiceExtensions.cs`
- OpenIddict scope registration includes custom file scopes:
  - `files:read`
  - `files:write`

2. `src/Core/DotNetCloud.Core.Server/Initialization/OidcClientSeeder.cs`
- Desktop client seeder includes permissions for:
  - `scope:files:read`
  - `scope:files:write`
- Seeder **updates existing** `dotnetcloud-desktop` app registration (upsert behavior), not create-only.

3. `src/Core/DotNetCloud.Core.Server/Program.cs`
- `OidcClientSeeder.SeedAsync()` invoked during startup initialization.

## Required Client State

Client should run code containing all of the following:

1. `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/SettingsViewModel.cs`
- OAuth scopes requested include:
  - `openid`
  - `profile`
  - `offline_access`
  - `files:read`
  - `files:write`

2. `src/Clients/DotNetCloud.Client.Core/Auth/OAuth2Service.cs`
- Scope negotiation and diagnostic logging are present.
- Logs include:
  - requested scopes
  - effective scopes
  - authorize URL scope used

## Mediator Checklist (User)

Run this handoff loop each iteration:

1. Ask server agent to confirm deployed commit hash on `mint22`.
2. Ask server agent to verify startup logs show OIDC seeding/updating action.
3. Ask client agent to confirm local commit hash and run SyncTray test.
4. If browser still fails, capture exact URL query `scope=` and attach screenshot/log.
5. Relay evidence back to the other agent through this file.

## Evidence Log

- 2026-03-07: Browser error changed from `invalid_client` to `invalid_scope`.
- 2026-03-07: Client-side log confirms onboarding flow triggers and opens browser.
- 2026-03-07 (mint22): Server redeployed at commit `a4fb730`; startup log confirms `Updated OIDC desktop client 'dotnetcloud-desktop' permissions/scopes.`

## Client Evidence Snapshot (2026-03-07)

### Commit
- Local branch: `main`
- Client commit hash after pull: `60b999b0070f13f5300c5357c18e41cbe0016819`

### Run result
- `dotnet build src\Clients\DotNetCloud.Client.SyncTray\DotNetCloud.Client.SyncTray.csproj` succeeded.
- `dotnetcloud-sync-service` and `dotnetcloud-sync-tray` processes launched and stayed running.
- First-run onboarding triggered from SyncTray log:
  - `[17:40:32 INF] No sync accounts configured. Launching first-run add-account flow.`

### Full authorize URL opened in browser
```text
https://mint22:15443/connect/authorize?response_type=code&client_id=dotnetcloud-desktop&redirect_uri=http%3a%2f%2flocalhost%3a52701%2foauth%2fcallback&scope=openid+profile+offline_access+files%3aread+files%3awrite&state=vjQGGMOXicZZq7dcqSCgLw&code_challenge=dAbwRP29DV1hPFJfENvB7N2KU7lnij3FUkE45_r1WXA&code_challenge_method=S256
```

### Error response params on failure page
- Browser page text:
  - `This mint22 page can't be found`
  - `HTTP ERROR 404`
- Query params present on failing URL:
  - `response_type=code`
  - `client_id=dotnetcloud-desktop`
  - `redirect_uri=http://localhost:52701/oauth/callback`
  - `scope=openid profile offline_access files:read files:write` (decoded)
  - `state=vjQGGMOXicZZq7dcqSCgLw`
  - `code_challenge=dAbwRP29DV1hPFJfENvB7N2KU7lnij3FUkE45_r1WXA`
  - `code_challenge_method=S256`
- OAuth callback error params observed:
  - `error`: not present
  - `error_description`: not present
  - `error_uri`: not present

### Client log excerpts (raw)
- Source: `%LOCALAPPDATA%\DotNetCloud\logs\sync-tray20260307.log`

```text
[17:40:53 INF] OAuth scope selection for https://mint22:15443: requested=[openid, profile, offline_access, files:read, files:write] effective=[openid, profile, offline_access, files:read, files:write]
[17:40:53 INF] Opening OAuth authorize URL for client 'dotnetcloud-desktop' with scope 'openid profile offline_access files:read files:write'.
[17:40:53 INF] Opening browser for OAuth2 authorization.
```

```text
[17:40:53 INF] Start processing HTTP request GET https://mint22:15443/.well-known/openid-configuration
[17:40:53 INF] Sending HTTP request GET https://mint22:15443/.well-known/openid-configuration
[17:40:53 INF] HTTP request failed after 121.7631ms
System.Net.Http.HttpRequestException: The SSL connection could not be established, see inner exception.
---> System.Security.Authentication.AuthenticationException: The remote certificate is invalid according to the validation procedure: RemoteCertificateNameMismatch, RemoteCertificateChainErrors
```

### Client scope confirmation
- Requested scopes in client code:
  - `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/SettingsViewModel.cs`
  - `scopes: ["openid", "profile", "offline_access", "files:read", "files:write"]`
- Effective scopes from runtime log:
  - `openid, profile, offline_access, files:read, files:write`

## Next Action Requested From Server Agent

- Verify active deployed server binary includes scope registration for `files:read` and `files:write`.
- Verify `dotnetcloud-desktop` OpenIddict application record has both scope permissions.
- If existing client record predates update logic, force-update it via startup seeder or admin script.

## Mandatory End-Of-Handoff Relay Instructions

Every handoff update must end with a section titled `Mediator Relay Instructions` containing both items below:

1. `Send to Server Agent` or `Send to Client Agent`
- Exact message text the mediator should copy/paste to the other side.

2. `Request Back`
- Exact evidence to ask for in return (commit hash, raw URL, raw query params, log lines, and timestamps where applicable).

Use this template at the end of each handoff entry:

```markdown
## Mediator Relay Instructions

### Send to Server Agent
<copy/paste message for server side>

### Request Back
- commit hash
- raw endpoint/URL used
- raw error/query params
- raw log lines around the event (with timestamp)
```

If the handoff is server-to-client instead, replace `Send to Server Agent` with `Send to Client Agent` and keep the same `Request Back` evidence requirements.

## Mediator Relay Instructions

### Send to Server Agent
Client evidence from `Windows11-TestDNC` on commit `60b999b0070f13f5300c5357c18e41cbe0016819`: SyncTray requests and uses scopes `openid profile offline_access files:read files:write`, but browser receives HTTP 404 at `/connect/authorize` instead of OAuth callback error params. Raw URL opened: `https://mint22:15443/connect/authorize?response_type=code&client_id=dotnetcloud-desktop&redirect_uri=http%3a%2f%2flocalhost%3a52701%2foauth%2fcallback&scope=openid+profile+offline_access+files%3aread+files%3awrite&state=vjQGGMOXicZZq7dcqSCgLw&code_challenge=dAbwRP29DV1hPFJfENvB7N2KU7lnij3FUkE45_r1WXA&code_challenge_method=S256`. Please verify server route/middleware for `/connect/authorize` on deployed instance and provide raw endpoint diagnostics.

### Request Back
- Deployed commit hash currently running on `mint22`.
- Raw response details for `GET https://mint22:15443/connect/authorize` from server side (status code, any response body, and headers if available).
- Raw startup log lines proving OpenIddict endpoints are mapped/active.
- Raw startup log lines for OIDC seeding/upsert of `dotnetcloud-desktop` including scopes/permissions.
- Any reverse-proxy routing rules affecting `/connect/*` and raw access log line for the failing authorize request (timestamp matched to client run).
