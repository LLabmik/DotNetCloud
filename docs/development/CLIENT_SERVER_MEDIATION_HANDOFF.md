# Client/Server Mediation Handoff

Last updated: 2026-03-12 (Chat UI CSS complete — all 14 component stylesheets created/overhauled, deployed to mint22)

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
- Chat UI fix deployed to mint22 (2026-03-12) — rebuilt, restarted, health verified Healthy.
- Chat UI Blazor binding fix verified on mint22 (2026-03-12) — redeploy complete, no raw variable names in `/apps/chat`, 302 auth redirect working.
- Full test suite: 2,106+ passed / 0 failed (1 pre-existing Files CDC test failure, unrelated).
- Chat DbContext concurrency bug: **FIXED** (2026-03-12). Service restarted, channels load.
- Chat UI CSS: Stylesheets created (2026-03-12) but **not loaded** — missing `<link>` tag in `App.razor`. Fixed by client agent.
- Chat UI CSS link tag fix: corrected `.styles.css` → `.bundle.scp.css` (2026-03-12). .NET 10 RCL CSS isolation uses `.bundle.scp.css` naming, not `.styles.css`. Deployed to mint22, all 14 component stylesheets verified loading (2,045 lines CSS, 200 OK).
- WYSIWYG Chat Composer: deployed to mint22 (2026-03-12). Contenteditable editor replaces raw textarea, JS module + CSS verified loading.
- Chat Permission Hardening + Members Display Names: deployed to mint22 (2026-03-12). Role-based UI gating, membership checks, announcement author-only edits, display names in members panel.
- **Channel Invite System**: implemented (2026-03-12). Single-user invites for private channels.
- Channel Invite EF migration + deploy: complete (2026-03-12). PostgreSQL migration applied, snapshot fixed, deployed to mint22.

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

### Chat UI Fixes — Invite Button, Members Panel, Online Status

**Date:** 2026-03-12
**Owner:** Client agent
**Status:** READY FOR DEPLOY

**What was done:**

1. **Channel invite UI** — Added "➕ Invite" button to channel header (visible only on private channels for admin/owner). Opens a dialog where you enter a username, sends a channel invite via `IChannelInviteService`. Previously there was zero discoverability for the invite system.

2. **Fixed member online status** — `ToMemberViewModel()` was hardcoding `Status = "Offline"` for every member. Now marks the current user as "Online" since they're actively viewing the page. (True presence tracking for other users will need SignalR connection tracking in a later phase.)

3. **Fixed MemberListPanel CSS class mismatch** — The Razor template used class names (`chat-member-panel`, `member-panel-header`, `status-dot`, `avatar-placeholder`, `role-badge`) that didn't exist in the CSS file. Aligned all class names between template and stylesheet.

4. **Overhauled MemberListPanel styling** — Rewrote CSS for dark theme consistency: proper header with close button, group labels, status dots with green glow for online, larger avatars, role badges with owner/admin colors, opacity dimming for offline members, dark-themed profile popup.

**Files changed:**
- `ChannelHeader.razor` — Added Invite button for private channels
- `ChannelHeader.razor.cs` — Added `OnInvite` callback parameter and handler
- `ChatPageLayout.razor` — Added `OnInvite` wiring + invite dialog markup
- `ChatPageLayout.razor.cs` — Added `IChannelInviteService` + `IUserDirectory` injection, invite state, invite handlers
- `ChatPageLayout.razor.css` — Added invite dialog styles + feedback messages
- `MemberListPanel.razor` — Fixed all CSS class names to match stylesheet
- `MemberListPanel.razor.css` — Complete restyle for dark theme with proper structure

**Build:** 0 errors, 283/283 chat tests pass.

**Needs:** Redeploy to mint22 to verify in browser.

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
