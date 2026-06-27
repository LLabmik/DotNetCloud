# Client/Server Mediation Handoff

Last updated: 2026-06-27 15:44 UTC (Android logcat confirms Chat HTTP 500 — server-side fix needed)

Purpose: shared handoff between client-side and server-side agents, mediated by user.

Archived context:

- Historical completed updates are in `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- Additional history remains available in git.
- VFS Phase 3 (Windows Cloud Filter API) completed on Windows11-TestDNC (2026-05-12).
- VFS Phase 2 (core abstraction layer) completed on Windows11-TestDNC (previously).

## Process Rules

**Agent autonomy (CRITICAL):**

- Both client and server agents work autonomously — they do NOT ask the moderator for context or permission.
- Agents pull the branch specified in the relay message, read the **Active Handoff** section, and execute the work described there independently.
- All actionable items, blockers, and technical details go directly in this document.
- **Current active branch:** `feature/chat-auth-bearer-token-support`
- No moderator involvement in technical decisions, code reviews, or work coordination.

**Role separation (MANDATORY):**

- **Client code** (`src/Clients/`, `src/UI/`) is handled ONLY by client machines (`mint-OptiPlex-7010`, `Windows11-TestDNC`, `mint-dnc-client`, `monolith`).
- **Server code** (`src/Core/`, `src/Modules/`) is handled ONLY by server machines (`cloud.kimball.home`, `mint22`).
- Each agent ONLY executes actions in the block matching their machine name (from the Environment table).
- If no action block matches your machine, the handoff is not for you — relay it to the moderator.
- Never cross role boundaries: a client agent never deploys server code, a server agent never builds client apps.

**Active Handoff format (MANDATORY):**

Every Active Handoff MUST use per-machine action blocks. Actions are grouped by the machine that executes them, using the exact machine names from the Environment table.

```markdown
### Active Handoff

**Summary:** [one-line description of what's happening]

[Context/background — what changed, why, relevant commits]

---

### Server Actions — `cloud.kimball.home`

- [ ] Action 1 with exact commands
- [ ] Action 2

### Client Actions — `mint-OptiPlex-7010`

- [ ] Action 1 with exact commands
- [ ] Action 2
```

**Critical rules:**
- Each agent ONLY executes actions in the block matching their machine name (from the Environment table).
- If no action block matches your machine, the handoff is not for you — relay it to the moderator.
- Always include exact commands (ready to copy-paste).
- Mark blocks with `✓` when complete; update status inline.
- One handoff may have 1 or 2 action blocks depending on workflow stage.

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
- Moderator handoff prompt rule (MANDATORY): every ready-to-relay message must explicitly state the target machine name (for example: `cloud.kimball.home`, `mint-dnc-client`, `Windows11-TestDNC`).
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

- 🔴 **Chat module HTTP 500** — `GET /api/v1/chat/channels` returns 500 on production. Root cause identified: `ListChannelsAsync()` lacks exception handling. Server-side fix needed (see Active Handoff).
- ✅ **Chat bearer token auth** — Deployed to production on `cloud.kimball.home`. Auth is working (token is accepted, reaches controller), but controller crashes before returning data.
- ✅ **Sync architecture** — All testing complete. See archive.
- ✅ **Linux client validation** — Completed on mint-OptiPlex-7010. Archived.

## Environment

| Role           | Machine              | Detail                                                                             |
| -------------- | -------------------- | ---------------------------------------------------------------------------------- |
| Server         | `cloud.kimball.home` | `https://cloud.dotnetcloud.net/` (production)                                      |
| Server         | `mint22`             | `https://mint22:5443/` (dev)                                                       |
| Client         | `Windows11-TestDNC`  | Sync dir: `C:\Users\benk\synctray`                                       |
| Client         | `mint-dnc-client`    | Linux Mint 22 validation host for desktop sync client implementation + E2E testing |
| Client         | `mint-OptiPlex-7010` | production client connected to `cloud.dotnetcloud.net`              |
| Android Client | `monolith`           | Android MAUI app development + emulator testing (Windows 11)                       |

## Key Carry-Forward Contracts

- Auth: Files module host uses a policy scheme (`DotNetCloud.Module`) that auto-selects between `OpenIddict.Validation.AspNetCore` (JWT Bearer) and `Identity.Application` (cookie) based on the `Authorization` header. Controllers use plain `[Authorize]`. All module hosts must follow this pattern.
- API envelope: middleware wraps responses; clients should unwrap via envelope helpers.
- Sync flow: changes -> tree -> reconcile -> chunk manifest -> chunk download -> file assembly.
- Desktop OAuth constant: `OAuthConstants.ClientId = "dotnetcloud-desktop"`.
- **SignalR channel group naming:** `chat-channel-{channelId}` (used by `ChatRealtimeService.ChannelGroup()` and Android `SignalRChatClient`).
- **Controller discovery:** Core.Server references Files.Host and Chat.Host via `ProjectReference`. ASP.NET Core auto-discovers controllers from referenced assemblies. Do NOT create duplicate controllers in Core.Server for routes already served by module Host assemblies.

## Active Handoff

**Summary:** Chat module returning HTTP 500 on `GET /api/v1/chat/channels` — `ListChannelsAsync` lacks exception handling; need server-side fix.

**Background (2026-06-27):** Chat bearer token auth was deployed to production (commit `11aa0d75`). Android client testing (monolith) revealed:

- Logcat confirmed token is present (2064 chars) — auth is working correctly
- `GET https://cloud.dotnetcloud.net/api/v1/chat/channels` returns **HTTP 500 (Internal Server Error)**
- Root cause: `ListChannelsAsync()` in `ChatController` has **no try-catch or `ExecuteAsync()` wrapping** (unlike other endpoints), so any exception from the service layer bubbles up unhandled
- Likely culprits: `ChannelService.ListChannelsAsync()` → `EnsureDefaultPublicChannelForUserAsync()` → `SaveChangesAsync()` failing on production DB (connection issue, unapplied migrations, or concurrent context operations)

**Additional issues found during audit:**
- `app.UseDeveloperExceptionPage()` is **unconditionally enabled** (not wrapped in `if (env.IsDevelopment())`) — leaks stack traces in production
- Several other endpoints in `ChatController` also lack try-catch (e.g., `ListAnnouncementsAsync`, `GetNotificationPreferencesAsync`)

---

### Server Actions — `cloud.kimball.home`

1. **Add exception handling to `ListChannelsAsync`** in `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Controllers/ChatController.cs`:
   - Wrap in `return await ExecuteAsync(async () => { ... })`
   - Or add explicit try-catch returning proper error envelope

2. **Fix production-only `UseDeveloperExceptionPage()`** in `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Program.cs`:
   ```csharp
   if (app.Environment.IsDevelopment())
       app.UseDeveloperExceptionPage();
   ```

3. **Audit and fix all other unprotected ChatController endpoints** — add `ExecuteAsync()` wrapping

4. **Investigate `ChannelService.ListChannelsAsync`** for potential DB issues:
   - Check if migrations are applied on production DB
   - Check if `EnsureDefaultPublicChannelForUserAsync` → `SaveChangesAsync` can fail
   - Verify DB connection string in production config

5. **Deploy** after fixing:
   ```bash
   git fetch origin
   git checkout feature/chat-auth-bearer-token-support
   git pull
   ./scripts/deploy.sh
   ```

6. **Verify** the fix:
   ```bash
   curl -H "Authorization: Bearer <test-token>" https://cloud.dotnetcloud.net/api/v1/chat/channels
   # Should return 200 with channel list, not 500
   ```

### Android Client Actions — `monolith`

- ☐ After server fix is deployed, rebuild APK, install on emulator, and test Chat tab
- Expected behavior: Chat tab loads channel list successfully
