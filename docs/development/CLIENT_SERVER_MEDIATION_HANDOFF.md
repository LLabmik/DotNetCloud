# Client/Server Mediation Handoff

Last updated: 2026-06-28 00:30 UTC (Root cause found: IUserDirectory not registered in Chat module host DI — fix needed)

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

- 🔴 **SenderName still empty** — `IUserDirectory` not registered in Chat module host DI. MessageService resolves with `_userDirectory = null` → `SenderName = ""`. Blazor UI works because it runs in-process in Core.Server where `IUserDirectory` IS registered.
- ✅ **gRPC-based real-time broadcaster deployed** — Blazor UI should receive live message updates from Android-sent messages.
- ✅ **DbContext concurrency fixed** — Sequential processing replaces `Task.WhenAll` to prevent concurrent DbContext access.
- ✅ **Chat tab WORKING** — HTTP 200, channels list loads successfully on Android.
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

**Summary:** `SenderName` still empty from REST API — `IUserDirectory` is NOT registered in Chat module host DI. Register it using the same pattern the Blazor UI uses (in-process `UserDirectoryService` in Core.Server), or add it to the Chat host.

**Background (2026-06-28, updated 00:30 UTC):** Android client logcat reveals:

- `GetMessagesAsync msg: senderUserId=..., senderName=''` — **SenderName is STILL empty** from the REST API
- `GetChannelMembersAsync member: userId=..., displayName='587d777a'` — **Member DisplayName also contains GUID fragments** from the database
- The `IUserDirectory` dependency in `MessageService.ToMessageDtoAsync()` is optional (defaults to `null`), and it resolves to `null` in the Chat module host because **no `IUserDirectory` registration exists in the Chat host's DI**

### Root Cause

The Blazor UI shows perfect names because:
1. It runs **in-process in Core.Server**, where `IUserDirectory` IS registered (`UserDirectoryService`)
2. `MessageService` gets a non-null `IUserDirectory` → `SenderName` is properly populated
3. Additionally, `ChatPageLayout.razor.cs` calls `ResolveDisplayNamesAsync()` to batch-resolve all unknown sender IDs via `[Inject] IUserDirectory`

The REST API (Chat module host process) does NOT register `IUserDirectory`, so:
1. `MessageService._userDirectory` is `null` → `SenderName = ""`
2. The `ChannelMembers` table stores GUID fragments as display names — this is legacy data

### Required Fix

**Register `IUserDirectory` in the Chat module host DI** so `MessageService.ToMessageDtoAsync()` can resolve display names. Follow the same source the Blazor UI uses:

- **Interface:** `DotNetCloud.Core.Auth.IUserDirectory` (in `src/Core/DotNetCloud.Core.Auth/`)
- **Implementation:** `UserDirectoryService` (in `src/Core/DotNetCloud.Core.Auth/`) — queries `CoreDbContext.Users` via `UserManager`
- The Chat module may need a project reference to `DotNetCloud.Core.Auth` (already has one)
- Register in `ChatServiceRegistration.cs` or `Chat.Host/Program.cs`:
  ```csharp
  services.AddScoped<IUserDirectory, UserDirectoryService>();
  ```
  (may also need to ensure `CoreDbContext` and `UserManager` are available in the Chat host — they already reference `DotNetCloud.Core.Auth` in the csproj)

### Server Actions — `cloud.kimball.home`

1. **Register `IUserDirectory`** in the Chat module host DI
2. **Rebuild and deploy**
3. **Verify** — Android client should show "Ben Kimball" etc. instead of GUID fragments

### Android Client Actions — `monolith`

- ☐ After server fix deployed, rebuild APK and test
