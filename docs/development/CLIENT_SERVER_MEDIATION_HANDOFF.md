# Client/Server Mediation Handoff

Last updated: 2026-07-14 (Notes Required Module — deploy to production)

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
- **Current active branch:** `feature/notes-required-module`

## Active Handoff

**Summary:** ✅ Notes module Bearer token auth deployed and verified on `cloud.kimball.home`

**Context:** Notes module REST auth fix — accept Bearer tokens for Android client. Server-side deployment complete. Ready for Android client testing.

**⚠️ Additional fix discovered during deployment:** The initial handoff only modified `NotesControllerBase.cs` and the `.csproj`, but `Program.cs` was **not** updated to register the `Introspection` authentication scheme. On first deploy, the endpoint returned `HTTP 500` — `No authentication handler is registered for the scheme 'Introspection'`. Fixed by updating `Program.cs` to use a policy scheme (`DotNetCloud.Module`) that auto-routes between `Identity.Application` (cookie) and `Introspection` (Bearer token), matching the Calendar module pattern exactly.

**Files changed (server-side):**
- `src/Modules/Notes/DotNetCloud.Modules.Notes.Host/Program.cs` — Added `AddTokenIntrospection()`, policy scheme `DotNetCloud.Module` with `ForwardDefaultSelector`, `AddIntrospection()` handler registration
- `src/Modules/Notes/DotNetCloud.Modules.Notes.Host/Controllers/NotesControllerBase.cs` — Added `[Authorize(AuthenticationSchemes = "Identity.Application," + "Introspection")]` and `using DotNetCloud.Core.Auth.Introspection`
- `src/Modules/Notes/DotNetCloud.Modules.Notes.Host/DotNetCloud.Modules.Notes.Host.csproj` — Added `<ProjectReference>` to `DotNetCloud.Core.Auth`

**Branch:** `feature/android-notes-tab`

---

### Server Actions — `cloud.kimball.home` ✅

- [x] Already on branch `feature/android-notes-tab`, already up-to-date
- [x] Discovered missing `Program.cs` Introspection registration — fixed with policy scheme pattern
- [x] Built and published: `dotnet publish src/Modules/Notes/DotNetCloud.Modules.Notes.Host -c Release -o /opt/dotnetcloud/publish/notes`
- [x] Deployed: copied DLLs to `/opt/dotnetcloud/server/modules/dotnetcloud.notes/`
- [x] Hash verification: `dotnetcloud.notes.dll` hashes match ✅
- [x] Restarted: `sudo systemctl restart dotnetcloud.service`
- [x] Verified: `dotnetcloud.notes` status = **Healthy** (14 modules registered)
- [x] Endpoint test: Bearer token request now returns `HTTP 401` (was `HTTP 500`) — Introspection handler is active

### Client Actions — `monolith` (Android client)

1. Test the Android Notes tab against `cloud.dotnetcloud.net`:
   - Open app on phone
   - Navigate to Notes tab
   - Verify existing notes load from server (should now use Bearer token auth)
   - Test creating a new note
   - Test editing and deleting notes
   - Verify search and folder filtering work

2. Report any issues back.

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
- Assistant pushes commits to `feature/android-files-photo-thumbnails`.
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

<!-- carry-forward contracts and old Android changes archived to CLIENT_SERVER_MEDIATION_ARCHIVE.md -->
