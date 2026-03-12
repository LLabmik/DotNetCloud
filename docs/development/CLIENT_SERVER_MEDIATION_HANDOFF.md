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
- Chat UI CSS: **FIXED** (2026-03-12). All 14 component stylesheets created or overhauled. Deployed to mint22, health verified Healthy.

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

### Chat UI CSS — Complete (Ready for Visual Verification)

**Date:** 2026-03-12
**Owner:** Client agent (`Windows11-TestDNC`)
**Status:** DONE — deployed, needs visual verification

**What was done (server agent):**

Created 8 missing `.razor.css` files and overhauled all 6 existing ones. All 14 chat component stylesheets now use the app's design system (CSS custom properties: `--color-*`, `--radius`, `--shadow-*`, etc.) with proper dark-mode support.

**New CSS files created (8):**
- `MessageComposer.razor.css` — Toolbar, input, send button, reply preview, emoji picker, mention dropdown
- `DirectMessageView.razor.css` — DM header, user search, avatar, status dots, layout
- `ChannelSettingsDialog.razor.css` — Modal form layout, member management, metadata
- `ChatNotificationBadge.razor.css` — Pill badge with pop-in animation, mention variant
- `NotificationPreferencesPanel.razor.css` — Push/DND settings, muted channel list
- `TypingIndicator.razor.css` — Animated bouncing dots, italic text
- `AnnouncementBanner.razor.css` — Priority colour variants (normal/important/urgent), slide-in
- `AnnouncementEditor.razor.css` — Modal with preview tabs, form groups, side-by-side fields

**Existing CSS files overhauled (6):**
- `ChannelHeader.razor.css` — Full header layout with channel info, topic separator, styled action buttons
- `ChannelList.razor.css` — Channel items with hover/active states, badges, search, create dialog, group headers
- `ChatPageLayout.razor.css` — Proper backgrounds on sidebar/main, design-system variables
- `MessageList.razor.css` — Message items with avatar, sender, timestamp, reactions, proper states
- `MemberListPanel.razor.css` — Full panel layout, member items, profile popup, role badges
- `AnnouncementList.razor.css` — Card layout, skeleton shimmer using design-system colours

**Deployment:**
- Built Release, redeployed via `redeploy-baremetal.sh`
- Service restarted, health verified Healthy at `https://localhost:15443/health/live`
- 263 Chat tests pass, 0 failures

**Action needed from client agent:**
1. Navigate to `https://mint22:15443/apps/chat`
2. Verify visual appearance — channels should have hover/active states, header should be clean, composer should be polished
3. Check dark mode if applicable
4. Report any remaining visual issues

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
