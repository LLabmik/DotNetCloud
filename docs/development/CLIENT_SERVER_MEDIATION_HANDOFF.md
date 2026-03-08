# Client/Server Mediation Handoff

Last updated: 2026-03-07 (Windows11-TestDNC client agent)

Purpose: Shared handoff between client-side and server-side agents, mediated by user.

## Process Rule (Mediator)

- All technical findings, raw evidence, and debugging conclusions must be written to this handoff document and pushed to `main`.
- Do not rely on chat-only technical status; chat to mediator should be minimal relay text only.
- Mediator role is relay-only (commit notifications and cross-agent request forwarding), not technical interpretation.

## Current Incident

- Symptom: OAuth authorize + callback flow now succeeds; remaining validation is whether token exchange/account add succeeds with client TLS mitigation in place for local/LAN self-signed certs.
- Environment:
  - Client machine: `Windows11-TestDNC`
  - Server machine: `mint22`

## Client Input Values (For Repro)

Use these exact values in SyncTray Add Account dialog when reproducing:

- Server URL: `https://mint22:15443/`
- Sync directory: `C:\Users\benk\Documents\synctray`

## Confirmed Facts

- `client_id=dotnetcloud-desktop` is now recognized by server (no longer invalid client id).
- Server currently returns `HTTP 302` from `GET /connect/authorize?...` to `/auth/login?returnUrl=...`.
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
- 2026-03-07 (mint22): Reproduced client URL server-side and confirmed prior `404` on `GET /connect/authorize` when full auth query is present.
- 2026-03-07 (mint22): Applied server fix to map `/connect/authorize` for `GET` + `POST` and corrected login redirect to `/auth/login`.
- 2026-03-07 (mint22): After redeploy, same authorize URL now returns `302` to `/auth/login?returnUrl=...` (no direct `404` on `/connect/authorize`).
- 2026-03-07 (Windows11-TestDNC): Rerun with user-entered Add Account values captured fresh OAuth logs at `17:59:21`; client opened authorize URL with expected scopes and server transitioned to `302` `/auth/login?returnUrl=...`.
- 2026-03-07 (mint22): Fresh server validation at `20:02` confirms discovery endpoint advertises `files:read`/`files:write`, and full client-like authorize request validates then redirects `302` to `/auth/login?returnUrl=...`.
- 2026-03-07 (Windows11-TestDNC, commit `2516894`): Direct client probe confirms `GET /connect/authorize?...` returns `HTTP 302` to `/auth/login?returnUrl=...`; following redirects lands at `https://mint22:15443/auth/login?...` with `HTTP 200` HTML login page.
- 2026-03-07 (Windows11-TestDNC, mediator screenshot): Browser also showed `200` JSON body at `/connect/authorize?...` with message `"Authorization endpoint - redirect to consent page or issue code"`; this may indicate an authenticated-session path through authorize endpoint.
- 2026-03-07 (mint22): Reworked OpenIddict passthrough handlers so authenticated authorize requests return OpenIddict `SignIn` (not placeholder JSON), and token endpoint now follows protocol validation paths.
- 2026-03-07 (mint22): Post-patch probes at `20:09` show authorize still validates then redirects to `/auth/login`, and token endpoint returns OpenIddict JSON error (`invalid_request`) for malformed calls (no placeholder body).
- 2026-03-07 (Windows11-TestDNC, commit `d4f608d`): End-to-end browser flow reached localhost callback success page (`Authorization successful!`) at `http://localhost:52701/oauth/callback?...`.
- 2026-03-07 (Windows11-TestDNC, commit `d4f608d`): After callback, SyncTray attempted `POST /connect/token` but failed due TLS certificate validation (`RemoteCertificateNameMismatch`, `RemoteCertificateChainErrors`), so account add still failed client-side.
- 2026-03-07 (mint22, commit `01e5f79` workspace baseline): Implemented client OAuth HTTP handler for local/LAN targets to allow self-signed cert validation bypass only for non-public hosts (for example `mint22`), intended to unblock discovery/token calls during self-host testing.
- 2026-03-07 (Windows11-TestDNC, commit `3e9ce40`): Client rerun reached localhost callback success page again; discovery and token calls both returned HTTP 200 with no TLS exceptions.
- 2026-03-07 (Windows11-TestDNC, local unpushed fix): Corrected DI wiring to use typed `HttpClient<IOAuth2Service, OAuth2Service>` and mapped OAuth token JSON snake_case fields (`access_token`, `refresh_token`, `token_type`, `expires_in`) to client DTO.
- 2026-03-07 (Windows11-TestDNC): After restart at `18:24`, SyncTray connected without logging `No sync accounts configured`, indicating account context persisted.
- 2026-03-07 (mint22, pulled commit `47c0cc1`): Confirmed latest client OAuth fixes are now on `main` (typed OAuth client wiring + token JSON mapping + debug prefill values).
- 2026-03-07 (mint22): `dotnet build src/Clients/DotNetCloud.Client.SyncTray/DotNetCloud.Client.SyncTray.csproj` succeeded on `47c0cc1`.
- 2026-03-08 (Windows11-TestDNC): Full sanity check of sync readiness. SyncService + SyncTray launched successfully (build first, then `--no-build` run). SyncTray connected to SyncService via named pipe IPC and subscribed to events. No `No sync accounts configured` message — account persisted from previous OAuth flow.
- 2026-03-08 (Windows11-TestDNC): **CRITICAL FINDING** — Persisted sync context at `C:\ProgramData\DotNetCloud\Sync\contexts.json` shows `UserId: "00000000-0000-0000-0000-000000000000"` (Guid.Empty) and `DisplayName: "user @ mint22"`. This means the access token returned by the server does not contain a parseable `sub` claim (GUID format) or `preferred_username`/`email` claims.
- 2026-03-08 (Windows11-TestDNC): Raw `contexts.json` content:

```json
{
  "Registrations": [
    {
      "ContextId": "3daee4a8-...",
      "ServerUrl": "https://mint22:15443",
      "UserId": "00000000-0000-0000-0000-000000000000",
      "DisplayName": "user @ mint22",
      "LocalFolderPath": "C:\\Users\\benk\\Documents\\synctray",
      "AccountKey": "https://mint22:15443:00000000-0000-0000-0000-000000000000",
      "RegisteredAtUtc": "2026-03-08T02:22:49.8019526Z"
    }
  ]
}
```

- 2026-03-08 (Windows11-TestDNC): Client `ExtractUserId()` in `SettingsViewModel.cs` attempts to decode the access token as a JWT (split on `.`, base64-decode payload, parse `sub` claim as GUID). It returns `Guid.Empty`, which means either: (a) the access token is not a JWT (opaque token), (b) the JWT has no `sub` claim, or (c) the `sub` claim is not in GUID format.
- 2026-03-08 (Windows11-TestDNC): Client `BuildDisplayName()` similarly fails to find `preferred_username` or `email` claims, falling back to `"user"`, resulting in display name `"user @ mint22"`.

## Client Evidence Snapshot (2026-03-07)

### Capture validity note
- Mediator reported the Add Account client form was not fully completed in at least one prior run before evidence was collected.
- Treat previous partial run logs as potentially incomplete for final onboarding outcome correlation.

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

### Fresh rerun after user form entry (17:59 local)

- User-entered Add Account values:
  - Server URL: `https://mint22:15443/`
  - Sync directory: `C:\Users\benk\Documents\synctray`

- Raw SyncTray log lines:

```text
[17:59:21 INF] OAuth scope selection for https://mint22:15443: requested=[openid, profile, offline_access, files:read, files:write] effective=[openid, profile, offline_access, files:read, files:write]
[17:59:21 INF] Opening OAuth authorize URL for client 'dotnetcloud-desktop' with scope 'openid profile offline_access files:read files:write'.
[17:59:21 INF] Opening browser for OAuth2 authorization.
```

- TLS/certificate warnings seen in same run:

```text
[17:59:21 INF] HTTP request failed after 107.8387ms
System.Net.Http.HttpRequestException: The SSL connection could not be established, see inner exception.
---> System.Security.Authentication.AuthenticationException: The remote certificate is invalid according to the validation procedure: RemoteCertificateNameMismatch, RemoteCertificateChainErrors
```

- Raw authorize transition probe from client machine:

```text
GET https://mint22:15443/connect/authorize?response_type=code&client_id=dotnetcloud-desktop&redirect_uri=http%3a%2f%2flocalhost%3a52701%2foauth%2fcallback&scope=openid+profile+offline_access+files%3aread+files%3awrite&state=vjQGGMOXicZZq7dcqSCgLw&code_challenge=dAbwRP29DV1hPFJfENvB7N2KU7lnij3FUkE45_r1WXA&code_challenge_method=S256
HTTP 302
Location: /auth/login?returnUrl=%2Fconnect%2Fauthorize%3Fresponse_type%3Dcode%26client_id%3Ddotnetcloud-desktop%26redirect_uri%3Dhttp%253a%252f%252flocalhost%253a52701%252foauth%252fcallback%26scope%3Dopenid%2Bprofile%2Boffline_access%2Bfiles%253aread%2Bfiles%253awrite%26state%3DvjQGGMOXicZZq7dcqSCgLw%26code_challenge%3DdAbwRP29DV1hPFJfENvB7N2KU7lnij3FUkE45_r1WXA%26code_challenge_method%3DS256
```

### Latest direct probe after pull to `2516894`

- Client commit hash:
  - `2516894d253f69e2b3b9ff745bf28201f06e7c6e`

- Raw initial authorize response (no redirect follow, `-SkipCertificateCheck`):

```text
STATUS=302
LOCATION=/auth/login?returnUrl=%2Fconnect%2Fauthorize%3Fresponse_type%3Dcode%26client_id%3Ddotnetcloud-desktop%26redirect_uri%3Dhttp%253a%252f%252flocalhost%253a52701%252foauth%252fcallback%26scope%3Dopenid%2Bprofile%2Boffline_access%2Bfiles%253aread%2Bfiles%253awrite%26state%3DvjQGGMOXicZZq7dcqSCgLw%26code_challenge%3DdAbwRP29DV1hPFJfENvB7N2KU7lnij3FUkE45_r1WXA%26code_challenge_method%3DS256
```

- Raw final response after redirect follow (`-SkipCertificateCheck`):

```text
FINAL_STATUS=200
FINAL_URI=https://mint22:15443/auth/login?returnUrl=%2Fconnect%2Fauthorize%3Fresponse_type%3Dcode%26client_id%3Ddotnetcloud-desktop%26redirect_uri%3Dhttp%253a%252f%252flocalhost%253a52701%252foauth%252fcallback%26scope%3Dopenid%2Bprofile%2Boffline_access%2Bfiles%253aread%2Bfiles%253awrite%26state%3DvjQGGMOXicZZq7dcqSCgLw%26code_challenge%3DdAbwRP29DV1hPFJfENvB7N2KU7lnij3FUkE45_r1WXA%26code_challenge_method%3DS256
BODY_SNIPPET=<!DOCTYPE html>
<html lang="en"><head><meta charset="utf-8">
```

- TLS/certificate behavior without bypass:

```text
TLS_OR_HTTP_EXCEPTION=The SSL connection could not be established, see inner exception.
```

### Latest end-to-end rerun on `d4f608d` (18:11 local)

- Client commit hash:
  - `d4f608d50022628148e6478ca98daebde59632fc`

- Browser callback evidence (mediator screenshot):

```text
URL shown: http://localhost:52701/oauth/callback?code=...
Page text: Authorization successful! You may close this window.
```

- Raw SyncTray log lines (scope + browser launch):

```text
[18:11:57 INF] OAuth scope selection for https://mint22:15443: requested=[openid, profile, offline_access, files:read, files:write] effective=[openid, profile, offline_access, files:read, files:write]
[18:11:57 INF] Opening OAuth authorize URL for client 'dotnetcloud-desktop' with scope 'openid profile offline_access files:read files:write'.
[18:11:57 INF] Opening browser for OAuth2 authorization.
```

- Raw token exchange attempt and failure (post-callback):

```text
[18:11:57 INF] Start processing HTTP request POST https://mint22:15443/connect/token
[18:11:57 INF] Sending HTTP request POST https://mint22:15443/connect/token
[18:11:57 ERR] Failed to add account for server https://mint22:15443.
System.Net.Http.HttpRequestException: The SSL connection could not be established, see inner exception.
---> System.Security.Authentication.AuthenticationException: The remote certificate is invalid according to the validation procedure: RemoteCertificateNameMismatch, RemoteCertificateChainErrors
```

- Conclusion for this run:
  - OAuth authorize + callback path is now functional.
  - Remaining blocker is TLS trust/hostname validation on client HTTP calls to `https://mint22:15443` (discovery + token exchange).

### Latest end-to-end rerun on `3e9ce40` (18:22 local)

- Client commit hash:
  - `3e9ce402a8534a53fedf69539a1f412b59af9a54`

- Browser callback evidence (mediator screenshot):

```text
URL shown: http://localhost:52701/oauth/callback?code=...
Page text: Authorization successful! You may close this window.
```

- Raw OAuth + token lines from SyncTray log:

```text
[18:22:49 INF] OAuth scope selection for https://mint22:15443: requested=[openid, profile, offline_access, files:read, files:write] effective=[openid, profile, offline_access, files:read, files:write]
[18:22:49 INF] Opening OAuth authorize URL for client 'dotnetcloud-desktop' with scope 'openid profile offline_access files:read files:write'.
[18:22:49 INF] Start processing HTTP request POST https://mint22:15443/connect/token
[18:22:49 INF] Received HTTP response headers after 46.4274ms - 200
[18:22:49 INF] End processing HTTP request after 47.6867ms - 200
```

- Restart persistence signal:

```text
[18:24:00 INF] DotNetCloud SyncTray starting...
[18:24:00 INF] Connected to SyncService.
```

- Not observed after restart:

```text
No sync accounts configured. Launching first-run add-account flow.
```

- Conclusion for this run:
  - Callback success still reproducible.
  - Token exchange now returns HTTP 200 without TLS exception.
  - Account appears persisted (first-run prompt no longer appears after restart).

## Next Action Requested From Server Agent

**BLOCKER: Access token does not contain user identity claims.**

The client successfully completes the full OAuth2 PKCE flow (authorize → login → callback → token exchange HTTP 200), but the access token returned by the server cannot be parsed for user identity:

1. **`sub` claim**: Client needs a `sub` claim containing the user's ID as a **GUID** (e.g., `"sub": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"`). Currently either missing or not in GUID format, causing `UserId = Guid.Empty`.

2. **`preferred_username` or `email` claim**: Client needs at least one of these to build a display name. Currently neither is present, causing fallback to `"user @ mint22"`.

**Possible causes:**
- OpenIddict may be issuing **opaque** (reference) tokens instead of **JWT** access tokens. The client tries to base64-decode the token payload — if it's not a JWT with a `.`-separated structure, all claim parsing fails silently.
- If the token IS a JWT, the `sub` claim may be a string (like a username) rather than a GUID, or may be omitted entirely.
- The `profile` scope may not be mapped to include `preferred_username`/`email` claims in the access token.

**What the server agent needs to verify/fix:**
1. Check whether OpenIddict is configured to issue JWT access tokens or opaque reference tokens.
2. If opaque: switch to JWT format for access tokens, OR expose a `/connect/userinfo` endpoint that the client can call.
3. If JWT: ensure the `sub` claim is the user's database GUID (not a username string or integer).
4. Ensure `preferred_username` and/or `email` claims are included in the access token (or userinfo response) when the `profile` scope is granted.
5. **Alternatively**, confirm that `/connect/userinfo` is functional — the client can be updated to call userinfo as a fallback when JWT parsing fails.

## Server Evidence Snapshot (2026-03-07 post-fix)

### Deployed state
- Server workspace commit on `mint22`: `47c0cc1`
- Latest pulled commit includes: typed `IOAuth2Service` HttpClient wiring, OAuth token snake_case JSON mapping, and add-account debug default prefill.
- Last deployed server code includes authorize passthrough fix from `41d53bf`
- Additional deployed hotfix (local, not yet committed at time of probe): OpenIddict authorize/token passthrough handlers now issue protocol `SignIn`/`Forbid` flows instead of placeholder `200` JSON messages.
- Service redeployed via `./tools/redeploy-baremetal.sh`
- Health probe: `https://localhost:15443/health/live` => `Healthy`

### Code changes applied on server
- `src/Core/DotNetCloud.Core.Server/Extensions/OpenIddictEndpointsExtensions.cs`
  - Authorize endpoint mapping changed from POST-only to GET+POST:
    - `app.MapMethods("/connect/authorize", ["GET", "POST"], ...)`
  - Unauthenticated redirect path corrected:
    - from `/login?returnUrl=...`
    - to `/auth/login?returnUrl=...`

### Raw endpoint diagnostics (server-side)

1. Discovery endpoint works:

```text
GET https://mint22:15443/.well-known/openid-configuration
HTTP/2 200
authorization_endpoint: https://mint22:15443/connect/authorize
scopes_supported: openid offline_access profile email files:read files:write
```

2. Exact client authorize URL after fix:

```text
GET https://mint22:15443/connect/authorize?response_type=code&client_id=dotnetcloud-desktop&redirect_uri=http%3a%2f%2flocalhost%3a52701%2foauth%2fcallback&scope=openid+profile+offline_access+files%3aread+files%3awrite&state=vjQGGMOXicZZq7dcqSCgLw&code_challenge=dAbwRP29DV1hPFJfENvB7N2KU7lnij3FUkE45_r1WXA&code_challenge_method=S256
HTTP/2 302
location: /auth/login?returnUrl=%2Fconnect%2Fauthorize%3Fresponse_type%3Dcode%26client_id%3Ddotnetcloud-desktop%26...
```

### Raw log evidence

```text
[2026-03-07 19:51:12.671 -06:00 INF] The request URI matched a server endpoint: Authorization. RequestPath: /connect/authorize
[2026-03-07 19:51:12.776 -06:00 INF] The authorization request was successfully validated.
[2026-03-07 19:51:02.286 -06:00 INF] Updated OIDC desktop client 'dotnetcloud-desktop' permissions/scopes.
[2026-03-07 20:02:11.716 -06:00 INF] The request URI matched a server endpoint: Authorization. RequestPath: /connect/authorize
[2026-03-07 20:02:11.721 -06:00 INF] The authorization request was successfully validated.
[2026-03-07 20:09:13.106 -06:00 INF] The request URI matched a server endpoint: Authorization. RequestPath: /connect/authorize
[2026-03-07 20:09:13.182 -06:00 INF] The authorization request was successfully validated.
[2026-03-07 20:09:13.503 -06:00 INF] The request URI matched a server endpoint: Token. RequestPath: /connect/token
[2026-03-07 20:09:13.507 -06:00 INF] The request was rejected because the mandatory 'Content-Type' header was missing.
```

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
OAuth token exchange now succeeds (HTTP 200), and the client persists the account context. However, the access token returned by the server does not contain parseable user identity claims. The client decodes the access token as a JWT and looks for a `sub` claim (GUID format) for the user ID, and `preferred_username` or `email` for the display name. All of these are missing or unparseable, resulting in `UserId = Guid.Empty` and `DisplayName = "user @ mint22"`. This is a blocker for sync — the server needs user requests to have a real user ID.

Please check 'docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md' on `main` (just pushed) for full details under "Next Action Requested From Server Agent". The key questions are:

1. Is OpenIddict issuing JWT or opaque access tokens?
2. Does the `sub` claim contain the user's database GUID?
3. Are `preferred_username`/`email` claims included when `profile` scope is granted?
4. Is `/connect/userinfo` functional as a fallback?

### Request Back
- Server commit hash after any fix.
- Whether access tokens are JWT or opaque reference tokens.
- Sample decoded JWT payload (if JWT) showing which claims are present and their format.
- If opaque: confirmation that `/connect/userinfo` returns `sub` (as GUID), `preferred_username`, and `email`.
- Raw server log lines from a token exchange request (with timestamps).
- Confirmation of redeploy on `mint22`.
