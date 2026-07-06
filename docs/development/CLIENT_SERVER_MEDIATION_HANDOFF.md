# Client/Server Mediation Handoff

Last updated: 2026-07-05 (Server deployed — cloud.kimball.home gRPC conversion published)

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
- **Current active branch:** `main`
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

**Summary:** Fix quota double-counting bug causing "409 Conflict" on second photo upload from Android

**Bug:** `InitiateUploadAsync` reserves quota via `TryReserveQuotaAsync` (adds file size to `UsedBytes`), then `CompleteUploadAsync` calls `AdjustUsedBytesAsync` which adds the same amount again — double-counting every upload. Also, `CancelUploadAsync` and `UploadSessionCleanupService` never released reserved quota on cancelled/expired sessions.

**Files changed:**
- `src/Modules/Files/DotNetCloud.Modules.Files.Data/Services/ChunkedUploadService.cs` — Fix `CompleteUploadAsync` to account for already-reserved quota (`finalAdjustment = quotaDelta - TotalSize`), and `CancelUploadAsync` to release reserved quota via `AdjustUsedBytesAsync(userId, -TotalSize)`
- `src/Modules/Files/DotNetCloud.Modules.Files.Data/Services/Background/UploadSessionCleanupService.cs` — Release reserved quota for expired upload sessions
- `src/Clients/DotNetCloud.Client.Android/Services/ApiExceptionHelper.cs` — Added `HttpStatusCode.Conflict` (409) handler so Android shows a meaningful error instead of "A connection error occurred"

---

### Server Actions — `cloud.kimball.home`

After pulling:

1. [ ] Deploy updated server: `dotnet publish src/Core/DotNetCloud.Core.Server -c Release -o /opt/dotnetcloud/publish` then `sudo systemctl restart dotnetcloud`
2. [ ] Fix already-inflated quota in the database:
   ```sql
   UPDATE core.FileQuotas SET UsedBytes = COALESCE((SELECT SUM(Size) FROM core.FileNodes WHERE OwnerId = FileQuotas.UserId AND NodeType = 'File' AND IsDeleted = 0), 0), LastCalculatedAt = NOW(), UpdatedAt = NOW();
   ```
3. [ ] Verify health: `curl -k https://cloud.kimball.home/health`

### Client Actions — `monolith` (Android client)

- [x] `ApiExceptionHelper.cs` updated with 409 handler
- [ ] Rebuild and deploy Android APK for testing

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
- Assistant pushes commits to `feature/fix-android-music-equalizer`.
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

**Summary:** Complete gRPC conversion — Chat proto expansion, 9 stub methods replaced, CoreCapabilities placeholders wired.

**Context:** All high-priority and medium-priority items from the gRPC conversion audit are now complete:

**Chat module — 9 new proto RPCs + server impl + client impl:**
- `chat_service.proto` expanded with `MarkAsRead`, `GetUnreadCounts`, `ListChannelMembers`, `SendCallOffer`, `SendCallAnswer`, `SendIceCandidate`, `SendMediaStateChange`, `InviteToCall`, `TransferCallHost`
- `ChatGrpcService.cs` — all 9 RPCs implemented, delegating to `IChannelMemberService`, `ICallSignalingService`, `IVideoCallService`
- `ChatGrpcApiClient.cs` — all 9 stub methods replaced with real gRPC calls
- 1272/1272 Chat module tests passing

**CoreCapabilities placeholders wired:**
- `SendNotification` — now resolves `INotificationService` from DI and dispatches to real notification pipeline
- `PublishEvent` — documented limitation (generic `IEventBus.PublishAsync<TEvent>` needs event type registry; modules should use `BroadcastRealtimeEvent` RPC for now)

**Cleanup:**
- Fixed misleading "Legacy in-process HTTP clients" comment in `Program.cs:537`

**SyncTray or Android app changes:** None. Only server-side files modified.

---

### Server Actions — `cloud.kimball.home`

- [x] `git pull` on `main` — pulled `cf183bee`
- [x] `dotnet publish src/Core/DotNetCloud.Core.Server -c Release -o /opt/dotnetcloud/publish` — succeeded, copied to `/opt/dotnetcloud/server/`
- [x] `dotnet publish src/Modules/Chat/DotNetCloud.Modules.Chat.Host -c Release -o /opt/dotnetcloud/modules/chat` — succeeded, copied to `/opt/dotnetcloud/server/modules/dotnetcloud.chat/`
- [x] `sudo systemctl restart dotnetcloud` (core server) — active (running)
- [x] `sudo systemctl restart dotnetcloud@chat` — unit not loaded (Chat runs as child process supervised by core)
- [x] Verify health: `curl -k https://cloud.kimball.home/health` — 13/13 modules healthy (chat, contacts, files, calendar, about, notes, tracks, video, email, bookmarks, music, ai, search)
- [ ] Verify new Chat RPCs: trigger a MarkAsRead or ListChannelMembers call from the UI and check module logs
- [x] Endpoint routing verified: all return 401 (auth required, not 404)

### Client Actions — `monolith` (Android client)

- [x] Replace hardcoded 10-band ProgressBars with device-accurate dynamic Sliders
- [x] Fix EQ gain reset on every playback state change (only recreate on session change)
- [x] Add save EQ preset dialog (name entry + overwrite existing) + REST client methods
- [x] Add EQ icon button in title bar, remove from segmented tab bar
- [x] All warnings fixed (0 warnings, 0 errors)
- [x] Built and deployed to phone — sliders work

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
- **Calendar event click crash fix:** `x:DataType` in Day view `CollectionView.ItemTemplate` corrected from `vm:CalendarViewModel` to `core:CalendarEventDto`
- **Calendar week view fix:** Inner `DataTemplate x:DataType` corrected from `x:Object` to `core:CalendarEventDto`
- **Calendar error handling:** `SelectEventAsync()` and `OnEventSelected()` now wrapped in try-catch to prevent unhandled crashes
