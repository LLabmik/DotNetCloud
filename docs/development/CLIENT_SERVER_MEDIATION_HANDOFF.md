# Client/Server Mediation Handoff

Last updated: 2026-06-29 (Android client alphabet index deployed to phone ✓)

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
- **Current active branch:** `feature/android-calendar-tab`
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
- Assistant pushes commits to `feature/android-calendar-tab`.
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

## Active Handoff

**Summary:** Calendar module host needs Bearer token introspection auth deployed to production (cloud.kimball.home)

**Context:** The Android Calendar tab (`feature/android-calendar-tab` branch) crashes with "session expired" when tapped. Root cause: the Calendar module host only supports cookie auth (`Identity.Application`), but Android clients send Bearer JWT tokens. The YARP proxy forwards the Bearer token to the Calendar host, which doesn't support it → 401 Unauthorized → Android interprets as session expired.

**Fix committed to `feature/android-calendar-tab` at `efc8f8f7`:**
- `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Host/Program.cs` — replaced cookie-only auth with dual-auth (cookie + Bearer token introspection), matching the pattern used by Music/Files/Chat module hosts
- `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Host/Controllers/CalendarControllerBase.cs` — updated `[Authorize]` attribute to accept both `Identity.Application` and `Introspection` schemes

**What was NOT changed (works as-is):**
- The Android client (`src/Clients/`) already sends Bearer tokens correctly — it was the Calendar host rejecting them
- The YARP proxy in Core.Server already forwards the `Authorization: Bearer` header correctly
- The `DotNetCloud.Core.Auth.Introspection` package (gRPC token introspection) is already a dependency of the Calendar host via its `.csproj` file

---

### Server Actions — `cloud.kimball.home`

**Goal:** Deploy the Calendar module auth fix to production so Android clients can call the Calendar API.

1. **Get the fix onto your machine:**
   ```bash
   # Option A: Merge the feature branch into main (preferred)
   git checkout main
   git pull origin main
   git merge origin/feature/android-calendar-tab
   
   # Option B: Just build from the feature branch directly
   git fetch origin
   git checkout feature/android-calendar-tab
   ```

2. **Build the Calendar module host:**
   ```bash
   dotnet build src/Modules/Calendar/DotNetCloud.Modules.Calendar.Host/DotNetCloud.Modules.Calendar.Host.csproj -c Release
   ```

3. **Publish the Calendar module host** (overwrites old DLLs):
   ```bash
   dotnet publish src/Modules/Calendar/DotNetCloud.Modules.Calendar.Host/DotNetCloud.Modules.Calendar.Host.csproj -c Release -o /opt/dotnetcloud/modules/calendar
   ```

4. **Restart the entire DotNetCloud service** (all modules restart):
   ```bash
   sudo systemctl restart dotnetcloud
   ```

5. **Verify all modules are healthy:**
   ```bash
   curl -s https://cloud.dotnetcloud.net/health | jq .
   ```
   Look for `"status": "Healthy"` for all 14 modules including Calendar.

6. **Verify Calendar API responds to Bearer token requests:**
   The Android app sends Bearer tokens obtained from `/connect/token`. After the fix, the Calendar host's introspection handler will validate them via gRPC call to Core.Server. No manual curl test needed — just have the Android client try the Calendar tab.

**What the fix does:** The Calendar module host's `Program.cs` now registers:
- `AddTokenIntrospection()` — gRPC client that calls Core.Server to validate Bearer tokens
- `AddIntrospection()` — auth handler that uses the introspection client
- A policy scheme that routes Bearer tokens to introspection, cookies to Identity.Application
- `CalendarControllerBase.cs` `[Authorize]` now accepts both schemes

**Files changed (2 files, committed to `feature/android-calendar-tab`):**
- `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Host/Program.cs`
- `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Host/Controllers/CalendarControllerBase.cs`

---

### Client Actions — `monolith` (Android client)

- [x] Android client already rebuilt and deployed to physical phone with all Calendar-related fixes
- [x] XAML converter key crash fix deployed (`IsNotNullConverter` → `IsNotNull`)
- [ ] Re-test Calendar tab on phone after server-side fix is deployed

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
- ✅ **SignalR channel group naming:** `chat-channel-{channelId}` (used by `ChatHub.ChannelGroup()`, `CoreHub.JoinGroupAsync()`, and Android `SignalRChatClient`).
- **Controller discovery:** Core.Server references Files.Host and Chat.Host via `ProjectReference`. ASP.NET Core auto-discovers controllers from referenced assemblies. Do NOT create duplicate controllers in Core.Server for routes already served by module Host assemblies.

**Fix applied in source (committed to branch):**
- `CoreHub.JoinGroupAsync()` now accepts both `"chat-channel-{guid}"` and bare GUID formats
- Extracts the GUID, then joins the connection to `"chat-channel-{guid}"` — matching `ChatHub.ChannelGroup()`
- `CoreHub.LeaveGroupAsync()` updated similarly for consistency
- `ChannelGroup()` helper method added to `CoreHub` matching the one in `ChatHub`

**Android client changes (already deployed in APK):**
- `ChatConnectionService` now starts correctly (was never started before)
- SignalR connection verified working via logcat ("SignalR connected successfully!")
- `SenderName` display confirmed working
- `JoinChannelGroupAsync` already sends the correct format ("chat-channel-{guid}")
