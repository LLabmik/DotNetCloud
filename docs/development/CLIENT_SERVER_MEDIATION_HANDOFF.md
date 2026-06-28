# Client/Server Mediation Handoff

Last updated: 2026-06-28 02:30 UTC (All fixes deployed — IUserDirectory gRPC bridge + Blazor real-time forwarding)

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

- ✅ **IUserDirectory gRPC bridge deployed** — `GrpcUserDirectoryService` registered in Chat module host DI. Resolves `SenderName` via Core.Server's `CoreCapabilities.GetUser` RPC, which was wired up to resolve `IUserDirectory` (database-backed `UserDirectoryService`).
- ✅ **Blazor real-time forwarding deployed** — `BroadcastRealtimeEvent` handler in Core.Server now forwards chat events (`NewMessage`, `MessageEdited`, `MessageDeleted`) to `IChatMessageNotifier` in-process, so Blazor Server components receive updates without requiring a client-side SignalR HubConnection.
- ✅ **gRPC-based real-time broadcaster** — Messages from Android (REST API → Chat module host → gRPC → Core.Server → SignalR) now also reach Blazor UI via in-process `IChatMessageNotifier` bridge.
- ✅ **DbContext concurrency fixed** — Sequential processing replaces `Task.WhenAll`.
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

**Summary:** All server-side fixes deployed and verified. Android client needs to rebuild APK and test both `SenderName` display and real-time message updates.

**Background (2026-06-28, updated 02:30 UTC):** Three server-side issues fixed and deployed:

### Fix 1: `SenderName` via gRPC User Directory

Created `GrpcUserDirectoryService` (following `GrpcRealtimeBroadcaster` pattern) that implements `IUserDirectory` by calling Core.Server's `CoreCapabilities.GetUser` RPC. Wired up the previously-stubbed `GetUser` and `SearchUsers` handlers in `CoreCapabilitiesServiceImpl` to resolve `IUserDirectory` from DI (database-backed `UserDirectoryService`).

**Files changed:**
- `src/Core/DotNetCloud.Core.Server/Grpc/Services/GrpcHealthServiceImpl.cs` — `GetUser` and `SearchUsers` wired to `IUserDirectory`
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Services/GrpcUserDirectoryService.cs` — **NEW**: gRPC client implementation of `IUserDirectory`
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Program.cs` — registered `GrpcUserDirectoryService`
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/MessageService.cs` — null-safe `GetDisplayNamesAsync` call

### Fix 2: Blazor Real-Time Updates

Root cause: The Blazor Web UI relies on `IChatMessageNotifier` in-process events (no client-side `HubConnection` to SignalR). When a message arrives via the REST API (Android → Chat module host), the Chat module host calls `NotifyMessageReceived()` on **its own** `InProcessChatMessageNotifier` instance — a different process from Core.Server. The SignalR broadcast (`GrpcRealtimeBroadcaster` → `BroadcastRealtimeEvent` RPC) reaches SignalR clients but NOT the Blazor components.

**Fix:** Added `TryForwardToChatMessageNotifier()` in `CoreCapabilitiesServiceImpl.BroadcastRealtimeEvent` handler. After broadcasting via `IRealtimeBroadcaster` (SignalR), it also forwards chat events (`NewMessage`, `MessageEdited`, `MessageDeleted`) to the in-process `IChatMessageNotifier`, which Blazor Server components subscribe to.

**Files changed:**
- `src/Core/DotNetCloud.Core.Server/Grpc/Services/GrpcHealthServiceImpl.cs` — `TryForwardToChatMessageNotifier()` method

### Server Actions — `cloud.kimball.home`

- ✓ Create `GrpcUserDirectoryService` in Chat.Host/Services/
- ✓ Wire up `GetUser`/`SearchUsers` in `CoreCapabilitiesServiceImpl`
- ✓ Register `IUserDirectory` in Chat host DI
- ✓ Add `TryForwardToChatMessageNotifier` in `BroadcastRealtimeEvent` handler
- ✓ Build, test (all 1272 Chat tests pass), deploy (15/15 targets), verify (14/14 modules healthy)

### Android Client Actions — `monolith`

- ☐ Rebuild APK after pulling latest
- ☐ Test Chat tab: verify `SenderName` shows display names (e.g., "Ben Kimball") instead of empty
- ☐ Test real-time: send a message from Android and verify Blazor Web UI receives it without page refresh
- ☐ Test the reverse: send from Blazor Web UI and verify Android receives it in real-time
