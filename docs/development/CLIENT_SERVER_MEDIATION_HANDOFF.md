# Client/Server Mediation Handoff

Last updated: 2026-03-07 (Windows11-TestDNC client agent)

Purpose: Shared handoff between client-side and server-side agents, mediated by user.

## Current Incident

- Symptom: Browser fails during SyncTray add-account with `error=invalid_scope`.
- Environment:
  - Client machine: `Windows11-TestDNC`
  - Server machine: `mint22`

## Confirmed Facts

- `client_id=dotnetcloud-desktop` is now recognized by server (no longer invalid client id).
- Failure shifted to scope validation (`invalid_scope`).
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

## Next Action Requested From Server Agent

- Verify active deployed server binary includes scope registration for `files:read` and `files:write`.
- Verify `dotnetcloud-desktop` OpenIddict application record has both scope permissions.
- If existing client record predates update logic, force-update it via startup seeder or admin script.
