# Client/Server Mediation Handoff

Last updated: 2026-03-17 (Client-side SignalR group join/leave completed on monolith — server broadcast handoff to mint22)

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
- **Active cycle:** Real-time chat message delivery — server-side SignalR broadcast from REST endpoints completed and deployed on `mint22`. Ready for Android client verification.

## Environment

| Role | Machine | Detail |
|---|---|---|
| Server | `mint22` | `https://mint22:15443/` |
| Client | `Windows11-TestDNC` | Sync dir: `C:\Users\benk\Documents\synctray` |
| Client | `mint-dnc-client` | Linux Mint 22 validation host for desktop sync client implementation + E2E testing |
| Android Client | `monolith` | Android MAUI app development + emulator testing (Windows 11) |

## Key Carry-Forward Contracts

- Auth: OpenIddict bearer on files/sync endpoints via `FilesControllerBase` `[Authorize]`.
- API envelope: middleware wraps responses; clients should unwrap via envelope helpers.
- Sync flow: changes -> tree -> reconcile -> chunk manifest -> chunk download -> file assembly.
- Desktop OAuth constant: `OAuthConstants.ClientId = "dotnetcloud-desktop"`.
- **SignalR channel group naming:** `chat-channel-{channelId}` (used by `ChatRealtimeService.ChannelGroup()` and Android `SignalRChatClient`).

## Active Handoff

### Server-Side Real-Time Chat — COMPLETED, Ready for Verification

**Target machine:** `monolith`
**Priority:** High
**Completed by:** `mint22` (server agent)

#### What Was Done on mint22 (Server-Side — COMPLETED)

**Task 1: REST endpoint SignalR broadcast (commit 7b8f757)**
1. Injected `IChatRealtimeService` into `ChatController`.
2. Added `BroadcastNewMessageAsync` / `BroadcastMessageEditedAsync` / `BroadcastMessageDeletedAsync` calls after REST send/edit/delete endpoints.
3. REST API clients (Android) now trigger real-time updates to SignalR group members.

**Task 2: Blazor in-process real-time (this commit)**
1. Created `IChatMessageNotifier` — lightweight in-process pub/sub singleton (`InProcessChatMessageNotifier`). Events: `MessageReceived`, `MessageEdited`, `MessageDeleted`.
2. Registered in `ChatServiceRegistration.cs` as singleton.
3. `ChatPageLayout.razor.cs`:
   - Injects `IChatRealtimeService` + `IChatMessageNotifier`.
   - After send/edit/delete: calls `BroadcastNewMessageAsync` (for SignalR/Android) AND `NotifyMessageReceived` (for other Blazor circuits).
   - Subscribes to `IChatMessageNotifier` events in `OnInitializedAsync`; unsubscribes in `Dispose`.
   - On receive: checks channelId match, deduplicates by message ID, calls `InvokeAsync(StateHasChanged)`.
4. `ChatController`: also publishes to `IChatMessageNotifier` after REST operations so Blazor users see messages from REST clients.
5. All 283 chat tests pass. Deployed and healthy on mint22.

**Complete real-time matrix now works:**
| Source | → Android (SignalR) | → Blazor (in-process) |
|--------|--------------------|-----------------------|
| REST API (Android) | ✓ via `IChatRealtimeService` | ✓ via `IChatMessageNotifier` |
| Blazor `ChatPageLayout` | ✓ via `IChatRealtimeService` | ✓ via `IChatMessageNotifier` |
| SignalR Hub (`CoreHub`) | ✓ (already worked) | ✗ (hub doesn't publish to notifier — follow-up) |

#### Verification Steps for monolith

1. Pull latest `main`.
2. Build Android app, deploy to emulator.
3. **Test 1 — Android sends, Blazor receives:** Open same channel in Android + Blazor web UI. Send from Android. Message should appear in Blazor without refresh.
4. **Test 2 — Blazor sends, Android receives:** Send from Blazor. Message should appear in Android app in real time.
5. **Test 3 — Blazor sends, Blazor receives:** Open same channel in two Blazor browser tabs. Send from one. Message should appear in the other without refresh.
6. **Test 4 — Edit/Delete:** Edit or delete a message from any client. Verify the change propagates in real time.

#### Request Back

- Commit hash after changes
- `dotnet test` output summary (pass/fail counts)
- `systemctl status dotnetcloud.service --no-pager` (first 10 lines)
- Any errors from `journalctl -u dotnetcloud.service --since "5 min ago" | grep -i err`

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
