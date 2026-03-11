# Client/Server Mediation Handoff

Last updated: 2026-03-11 (compacted active-only handoff)

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
- This handoff was compacted on 2026-03-11 to remove completed historical sections from active view.

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

### Phase 2.12 COMPLETE — Chat Integration Tests Added

**Date:** 2026-03-11
**Owner:** Server agent (completed)
**Status:** COMPLETE ✅

**What was completed:**
- Created `ChatHostWebApplicationFactory` (in-memory DB, NoOp broadcaster) for isolated Chat.Host integration testing.
- Created 47 REST API integration tests covering: channel CRUD (9), member management (6), message CRUD (8), reactions (3), pins (3), typing indicators (2), announcements (7), file attachments (2), push device registration (3), mark-read (1), health/info (2), full end-to-end flow (1).
- Fixed 3 bugs uncovered by integration tests:
  1. `CreatedAtAction` route mismatch — ASP.NET Core's `SuppressAsyncSuffixInActionNames=true` strips "Async" suffix, but `nameof(GetChannelAsync)` yields `"GetChannelAsync"` which doesn't match `"GetChannel"`.
  2. Duplicate `AnnouncementController` conflicting with `ChatController`'s `~/api/v1/announcements` routes — removed redundant controller.
  3. Test-discovered enum mismatches: `ChannelMemberRole` has no `Moderator` (use `Admin`), `NotificationPreference` has no `MentionsOnly` (use `Mentions`).
- Used `extern alias` (`FilesHost`/`ChatHost`) in integration test project to resolve `Program` type ambiguity between Chat.Host and Files.Host.
- Full suite: 2,086 passed, 0 failed, 2 skipped (env-gated).

**Next action:**
- Check `docs/MASTER_PROJECT_PLAN.md` for next in-progress or pending phase.
- Phase 2.13 (Documentation) is next pending item.
- Remaining in-progress phases: 2.3 (4 pending tasks), 2.8, 2.10 (ongoing Android app work).
- Assign owner (client or server) based on what the next task requires.

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
