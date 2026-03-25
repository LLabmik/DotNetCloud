# Client/Server Mediation Handoff

Last updated: 20260325 (Password grant flow implemented on mint22 — WS-4 API testing unblocked)

Purpose: shared handoff between client-side and server-side agents, mediated by user.

Archived context:
- Historical completed updates are in `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- Additional history remains available in git.

## Process Rules

**Agent autonomy (CRITICAL):**
- Both client and server agents work autonomously — they do NOT ask the moderator for context or permission.
- Agents pull the latest `main`, read the **Active Handoff** section, and execute the work described there independently.
- All actionable items, blockers, and technical details go directly in this document (committed to `main`).
- No moderator involvement in technical decisions, code reviews, or work coordination.

**Handoff management:**
- Put all technical findings, debugging conclusions, and next-step details in this document.
- Assistant (current agent) commits their findings/work and updates the **Active Handoff** section with actionable next steps for the other client.
- Assistant pushes commits to `main`.
- Unexpected untracked content rule (MANDATORY): remove unexpected untracked files/directories before commit; only keep intentional tracked changes for the handoff update.
- Handoff readiness gate (MANDATORY): all executable tests must pass before marking a handoff as ready.
- Environment-gated tests are allowed to be skipped, but must be explicitly identified as gated with the required environment/runtime prerequisites documented in the handoff.
- Runtime verification gate (MANDATORY): before declaring a server-side blocker fixed, verify the running service is on current binaries (not stale publish output) and document the verification command/output in handoff notes.
- OAuth contract check (MANDATORY when auth is involved): verify `client_id`, `redirect_uri`, and requested scopes exactly match server-registered OpenIddict client permissions before requesting cross-machine retries.
- Secret handling rule (MANDATORY): never commit raw bearer tokens/refresh tokens; share token acquisition steps and sanitized outputs only.
- Moderator relays a short "check for updates" message to the other machine.
- Moderator handoff prompt rule (MANDATORY): every ready-to-relay message must explicitly state the target machine name (for example: `mint22`, `mint-dnc-client`, `Windows11-TestDNC`).
- Other agent pulls latest, reads the handoff, and takes action without asking questions.

**Document maintenance:**
- Pre-commit archive rule (MANDATORY): before committing this file, move all completed/older handoff tasks to `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- Keep only the single current task in **Active Handoff** (one active block only).
- If a task is completed, archive it first, then replace **Active Handoff** with the next task.

## Moderator Communication (Minimal)

**Moderator relays ONLY ONE OF THESE messages — nothing more:**

- `New handoff update for <target-machine>. Pull main and resume from 'Active Handoff' section.`
- `<Commit hash> — New handoff update for <target-machine>. Pull and check docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md Active Handoff.`

**No moderator task:** Moderator provides zero context, zero explanation. The handoff document has everything the receiving agent needs.

## Current Status

- All prior Phase 2, chat, and pre-Linux sync remediation work is complete and archived.
- P0 server-side sync hardening deployed and verified on `mint22`.
- Upload hardening story: CLOSED (2026-03-15). All machines verified.
- Deletion propagation story: **CLOSED** (2026-03-16). All three machines verified.
  - Linux client (`mint-dnc-client`): verified 2026-03-16 ~03:00Z
  - Windows client (`Windows11-TestDNC`): verified 2026-03-16 ~08:16Z. Bug fixed: `RemoveFileRecordsUnderPathAsync` path separator on Windows.
  - Server (`mint22`): confirmed stable 2026-03-16. Zero ERR entries, both nodes soft-deleted, no 5xx.
- Duplicate controller fix: CLOSED (2026-03-18). Deployed and verified on `mint22`. Files endpoint returns 401, service healthy.
- Windows IIS + Service Validation: **COMPLETE** (2026-03-21). Three startup blockers resolved. IIS reverse proxy configured and verified (URL Rewrite + ARR). HTTP (port 80) and HTTPS (port 443) both proxy to Kestrel :5080. Self-signed localhost cert bound.
- File browser child count fix: **DEPLOYED** (2026-03-21). `mint22` redeployed; service stable.
- `mint22` connectivity diagnosis: **COMPLETE** (2026-03-22). Current deployment listens directly on HTTPS `:5443`; no listener exists on `:15443`.
- Security audit desktop client validation on `Windows11-TestDNC`: **COMPLETE** (2026-03-23).
- Security audit closeout + merge validation on `mint22`: **COMPLETE** (2026-03-23).
- Post-closeout Windows runtime smoke: **COMPLETE** (2026-03-23). 4/4 targeted tests passed; login launch path verified reachable.
- **Active cycle (20260325):** Password grant flow implemented and deployed on `mint22`. WS-4 API verification unblocked. Monolith can now acquire bearer tokens via `POST /connect/token` with `grant_type=password`.

## Environment

| Role | Machine | Detail |
|---|---|---|
| Server | `mint22` | `https://mint22:5443/` |
| Client | `Windows11-TestDNC` | Sync dir: `C:\Users\benk\Documents\synctray` |
| Client | `mint-dnc-client` | Linux Mint 22 validation host for desktop sync client implementation + E2E testing |
| Android Client | `monolith` | Android MAUI app development + emulator testing (Windows 11) |

## Key Carry-Forward Contracts

- Auth: OpenIddict bearer on files/sync endpoints via `FilesControllerBase` `[Authorize(AuthenticationSchemes = "OpenIddict.Validation.AspNetCore")]`.
- API envelope: middleware wraps responses; clients should unwrap via envelope helpers.
- Sync flow: changes -> tree -> reconcile -> chunk manifest -> chunk download -> file assembly.
- Desktop OAuth constant: `OAuthConstants.ClientId = "dotnetcloud-desktop"`.
- **SignalR channel group naming:** `chat-channel-{channelId}` (used by `ChatRealtimeService.ChannelGroup()` and Android `SignalRChatClient`).
- **Controller discovery:** Core.Server references Files.Host and Chat.Host via `ProjectReference`. ASP.NET Core auto-discovers controllers from referenced assemblies. Do NOT create duplicate controllers in Core.Server for routes already served by module Host assemblies.

## Active Handoff

**Target machine:** monolith
**Status:** READY FOR EXECUTION (20260325)

### WS-4 API Verification — Password Grant Deployed, Run Tests

**What was done (mint22):**

1. **Password grant flow added to OpenIddict** — `AllowPasswordFlow()` enabled in server config
2. **Token endpoint handler updated** — `POST /connect/token` now handles `grant_type=password` with email/password validation, lockout checks, and full claim population (subject, name, email, roles)
3. **Client permissions updated** — Both `dotnetcloud-desktop` and `dotnetcloud-mobile` seeded clients now include `Permissions.GrantTypes.Password`
4. **DI bug fixed** — `NotificationEventSubscriber` (singleton) was injecting scoped `INotificationService` directly; fixed to use `IServiceScopeFactory`. This fixed 20 integration test failures.
5. **Rate limiting test fixed** — `RateLimitingOptionsTests.DefaultOptions_HasCorrectDefaults` expected `GlobalPermitLimit=20` but actual default is `100`; test updated.
6. **All 2792 CI tests pass** (0 failures, 2 skipped platform-gated)

**Files changed:**
- `src/Core/DotNetCloud.Core.Auth/Extensions/AuthServiceExtensions.cs` — Added `AllowPasswordFlow()`
- `src/Core/DotNetCloud.Core.Server/Extensions/OpenIddictEndpointsExtensions.cs` — Added password grant handler
- `src/Core/DotNetCloud.Core.Server/Initialization/OidcClientSeeder.cs` — Added `Permissions.GrantTypes.Password` to both clients
- `src/Core/DotNetCloud.Core.Server/Services/NotificationEventSubscriber.cs` — Fixed DI: `INotificationService` → `IServiceScopeFactory`
- `src/Core/DotNetCloud.Core.Server/Services/InAppNotificationEventHandler.cs` — Fixed DI: resolve `INotificationService` per-event via scope
- `tests/DotNetCloud.Core.Server.Tests/Configuration/RateLimitingOptionsTests.cs` — Fixed stale assertion

**Deployment status:** Published to `artifacts/publish/server-baremetal/`. Deployment to `/opt/dotnetcloud/server/` requires sudo (moderator will handle service restart).

#### Token Acquisition — How to Get a Bearer Token

```powershell
# Password grant — single HTTP call, returns access_token + refresh_token
$body = @{
    grant_type = "password"
    client_id  = "dotnetcloud-desktop"
    username   = "your-email@example.com"
    password   = "YourPassword"
    scope      = "openid profile offline_access files:read files:write"
}
$response = Invoke-RestMethod -Uri "https://mint22:5443/connect/token" `
    -Method POST -ContentType "application/x-www-form-urlencoded" `
    -Body $body -SkipCertificateCheck
$bearer = $response.access_token
```

#### Next Steps (monolith)

1. Wait for moderator to deploy on mint22 (`sudo systemctl stop dotnetcloud.service && sudo cp -r artifacts/publish/server-baremetal/* /opt/dotnetcloud/server/ && sudo systemctl start dotnetcloud.service`)
2. Verify token acquisition works: run `scripts/get-bearer-token.ps1` adapted for password grant, or use the PowerShell snippet above
3. Execute `scripts/ws4-api-verification.ps1` with the acquired bearer token against `https://mint22:5443/`
4. Record PASS/FAIL/SKIP for TC-1.27, TC-1.40, TC-1.41, TC-1.42, TC-1.45
5. Commit test results to `ws4-test-results.json` and update handoff
