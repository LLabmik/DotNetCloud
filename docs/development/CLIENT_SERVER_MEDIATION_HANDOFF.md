# Client/Server Mediation Handoff

Last updated: 2026-03-12 (Chat UI fix — ChatPageLayout orchestrator replaces bare ChannelList)

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
- Other agent pulls latest, reads the handoff, and takes action without asking questions.

**Document maintenance:**
- Pre-commit archive rule (MANDATORY): before committing this file, move all completed/older handoff tasks to `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- Keep only the single current task in **Active Handoff** (one active block only).
- If a task is completed, archive it first, then replace **Active Handoff** with the next task.

## Moderator Communication (Minimal)

**Moderator relays ONLY ONE OF THESE messages — nothing more:**

- `New handoff update. Pull main and resume from 'Active Handoff' section.`
- `<Commit hash> — New handoff update. Pull and check docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md Active Handoff.`

**No moderator task:** Moderator provides zero context, zero explanation. The handoff document has everything the receiving agent needs.

## Current Status

- Issues #1-#45 and previous sprint/batch closeout work: complete.
- Phase 2.10 Android contract alignment: complete (archived).
- Phase 2.12 Chat Testing Infrastructure: complete (integration tests added).
- Phase 2.13 Documentation: complete.
- Urgent migration fix (AddSymlinkSupport/LinkTarget column): complete (2026-03-12).
- Integration test fixes (11 failures → 0): complete (2026-03-12).
- Phase 2.10 final items (badges, APK download docs, app store listing): complete (2026-03-12).
- **All Phase 2 work is now complete.**
- PosixMode migration blocker: fixed (2026-03-12) — all 6 Files migrations applied to production DB.
- Chat UI fix: ChatPageLayout orchestrator added (2026-03-12) — channels now clickable with full message view.
- Full test suite: 2,106+ passed / 0 failed (1 pre-existing Files CDC test failure, unrelated).

## Environment

| Role | Machine | Detail |
|---|---|---|
| Server | `mint22` | `https://mint22:15443/` |
| Client | `Windows11-TestDNC` | Sync dir: `C:\Users\benk\Documents\synctray` |

## Key Carry-Forward Contracts

- Auth: OpenIddict bearer on files/sync endpoints via `FilesControllerBase` `[Authorize]`.
- API envelope: middleware wraps responses; clients should unwrap via envelope helpers.
- Sync flow: changes -> tree -> reconcile -> chunk manifest -> chunk download -> file assembly.
- Desktop OAuth constant: `OAuthConstants.ClientId = "dotnetcloud-desktop"`.

## Active Handoff

### Chat UI Fix — Rebuild and Redeploy Required

**Date:** 2026-03-12
**Owner:** Server agent (`mint22`)
**Status:** ACTION REQUIRED 🔧
**Commit:** `2212a09`

**What changed (client-side commit):**

The Chat module's web UI was broken — clicking a channel in the sidebar did nothing. Root cause: `ModuleUiRegistrationHostedService` registered `ChannelList` (the sidebar component) as the root component for `/apps/chat`. Since nobody handled its `OnChannelSelected` callback, clicks were swallowed.

**Fix applied:**
1. Created `ChatPageLayout.razor/.cs/.css` — an orchestrator component that composes `ChannelList` + `ChannelHeader` + `MessageList` + `MessageComposer` into a split-pane layout.
2. Updated `ModuleUiRegistrationHostedService` to register `ChatPageLayout` instead of `ChannelList` as the root component.
3. Clicking a channel now loads messages via `IMessageService.GetMessagesAsync()` and renders the full chat conversation view.

**Files changed:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ChatPageLayout.razor` (new)
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ChatPageLayout.razor.cs` (new)
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ChatPageLayout.razor.css` (new)
- `src/Core/DotNetCloud.Core.Server/Initialization/ModuleUiRegistrationHostedService.cs` (modified)

**Build/test verification (client machine):**
- `dotnet build` — succeeded (0 errors)
- `dotnet test` — 263/263 Chat tests passed, all other suites green
- 1 pre-existing Files test failure (`ChunkAndHashCdcAsync_SmallData_ReturnsSingleChunk`) — unrelated

**Server agent action required:**
1. `git pull` on `mint22`
2. Rebuild and republish:
   ```bash
   dotnet publish src/Core/DotNetCloud.Core.Server -c Release -o /path/to/server-baremetal
   ```
3. Restart the service:
   ```bash
   sudo systemctl restart dotnetcloud.service
   ```
4. Verify:
   - `curl -k https://mint22:15443/health` → 200
   - Navigate to `https://mint22:15443/apps/chat` → channel list should render, clicking a channel should show header + messages + composer
5. Report back: commit hash, health output, and whether the chat UI is functional.

**No database changes required.** This is a code-only change.

## Relay Template

```markdown
### Send to [Server|Client] Agent
<message text>

### Request Back
- commit hash
- raw endpoint/URL used
- raw error/query params
- raw log lines around the event (with timestamp)
```
