# Client/Server Mediation Handoff

Last updated: 2026-03-21 (IIS HTTPS + HTTP reverse proxy complete on Windows11-TestDNC; child count fix deployed on mint22)

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
- **Active cycle:** Phase 1 & Phase 2 completion verified on `mint22`. Docs cleaned up, all 2,242 tests pass.

## Environment

| Role | Machine | Detail |
|---|---|---|
| Server | `mint22` | `https://mint22:15443/` |
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

**Target: none (verification complete)**

### Task: Phase 1 & Phase 2 Completion Verification — DONE

**Results (2026-03-21, mint22):**

1. **Build:** `dotnet build DotNetCloud.CI.slnf` — 0 errors, 0 warnings
2. **Tests:** `dotnet test DotNetCloud.CI.slnf` — **2,242 passed, 0 failed, 2 skipped** across 12 test projects:
   - Core.Tests: 138 passed
   - CLI.Tests: 119 passed
   - Core.Server.Tests: 331 passed (2 skipped)
   - Modules.Example.Tests: 51 passed
   - Core.Data.Tests: 176 passed
   - Core.Auth.Tests: 85 passed
   - Client.SyncService.Tests: 27 passed
   - Modules.Chat.Tests: 283 passed
   - Modules.Files.Tests: 638 passed
   - Client.Core.Tests: 182 passed
   - Client.SyncTray.Tests: 77 passed
   - Integration.Tests: 133 passed
3. **Integration test fix:** 20 integration tests were failing due to duplicate OpenIddict auth scheme registration in `DotNetCloudWebApplicationFactory`. Fixed by forwarding existing scheme to `TestAuthHandler` instead of re-registering.
4. **Doc cleanup:**
   - MASTER_PROJECT_PLAN.md: renamed mislabeled "Phase 2: Files" → "Phase 2: Chat & Notifications & Android", added Phase 1 summary section, fixed phase-2.1/2.2 step titles from "Files" to "Chat", corrected "Files API Integration Tests" → "Chat API Integration Tests"
   - Updated stale test counts: 803 → 2,242 across all tracking docs
   - Added `storage/chunks` and `storage/files` to `.gitignore`
5. **Phase 1 status:** 274/277 steps complete. 3 deferred items (right-click context menu, paste image upload, UI polish) are non-blocking for launch.
6. **Phase 2 status:** 13/13 sub-phases complete (100%).

### Completed This Session (2026-03-21)

1. **Windows installer improvement plan** — implemented all 12 tasks in `tools/install-windows.ps1`:
   - .NET runtime check, PostgreSQL auto-install, DB creation, admin credential prompts
   - IIS `allowedServerVariables` registration, ARR `reverseRewriteHostInResponseHeaders` fix
   - Self-signed HTTPS cert + binding, dynamic `X-Forwarded-Proto`
   - Beginner mode skips CLI wizard, updated summary with HTTPS URLs
   - Admin password minimum raised to 12 characters
   - Updated `WINDOWS_IIS_INSTALL_GUIDE.md` and `INSTALLATION.md`
2. **IIS Reverse Proxy** — configured on `Windows11-TestDNC`
   - URL Rewrite 2.1 + Application Request Routing 3.0 installed
   - `web.config` rewrite rule proxies all requests from IIS → Kestrel (port 5080)
   - HTTP (port 80): verified `http://localhost/health/live` → 200 OK
   - HTTPS (port 443): self-signed localhost cert (thumbprint `C0DB23E...`), verified `https://localhost/health/live` → 200 OK, `Server: Microsoft-IIS/10.0`
3. **File browser child count fix** — deployed on `mint22` by server agent

## Relay Template

```markdown
### Send to [Server|Client] Agent on <target-machine>
<message text including target machine>

### Request Back
- commit hash
- raw endpoint/URL used
- raw error/query params
- raw log lines around the event (with timestamp)
```
