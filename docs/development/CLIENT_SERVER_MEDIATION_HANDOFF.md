# Client/Server Mediation Handoff

Last updated: 2026-03-12 (Chat UI missing CSS — channel list loads but entire page is unstyled)

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
- **NEW BUG (2026-03-12):** Chat UI completely unstyled — missing .razor.css files for most components. See Active Handoff.

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

### Chat UI Missing CSS — Entire Page Unstyled

**Date:** 2026-03-12
**Owner:** Server agent (`mint22`)
**Status:** OPEN — CSS files need to be created

**Bug report from client testing (Windows11-TestDNC):**

**Symptom:** Chat page at `https://mint22:15443/apps/chat` loads channels and messages (DbContext fix works), but the entire page is **completely unstyled** — plain HTML with no visual formatting. Specifically:
- No cursor change on clickable items (no `cursor: pointer`)
- Channel list items are plain text with `#` prefix, no hover/active states
- Message composer toolbar buttons (B, I, code, link) are unstyled squares
- Pin/Edit/Archive/Leave/Members buttons are unstyled native browser buttons
- No proper layout separation between channel list and message area
- Messages display as plain text blocks with `?` placeholders where avatars should be
- Emoji picker button is an unstyled circle
- No visual hierarchy, spacing, or theming

**Root cause (confirmed by code inspection on client machine):**

8 Chat components are **missing their `.razor.css` files entirely**, despite their `.razor` markup referencing 20+ CSS classes that don't exist:

| Component | Missing CSS Classes (examples) |
|-----------|-------------------------------|
| `MessageComposer.razor` ❌ | `.chat-message-composer`, `.composer-toolbar`, `.composer-fmt`, `.composer-input`, `.composer-send`, `.emoji-picker`, `.emoji-btn`, `.mention-suggestion*`, `.reply-preview` |
| `DirectMessageView.razor` ❌ | `.chat-dm-view`, `.dm-header`, `.dm-user-search*`, `.dm-user-result*` |
| `ChannelSettingsDialog.razor` ❌ | `.settings-section`, `.member-management-list`, `.member-row`, `.add-member-row` |
| `ChatNotificationBadge.razor` ❌ | (no CSS file) |
| `NotificationPreferencesPanel.razor` ❌ | (no CSS file) |
| `TypingIndicator.razor` ❌ | (no CSS file) |
| `AnnouncementBanner.razor` ❌ | (no CSS file) |
| `AnnouncementEditor.razor` ❌ | (no CSS file) |

**Components that DO have CSS but are too minimal:**
- `ChannelHeader.razor.css` — single line only (`.channel-actions` gap)

**Components with adequate CSS:**
- `ChatPageLayout.razor.css` — flex layout ✓
- `ChannelList.razor.css` — presence dots, drag-drop, loading skeletons ✓
- `MessageList.razor.css` — dividers, attachment previews ✓
- `MemberListPanel.razor.css` — member styling ✓
- `AnnouncementList.razor.css` — loading states ✓

**Action needed from server agent:**
1. Create `.razor.css` files for all 8 missing components listed above
2. The CSS should match the dark theme used by the rest of the app (dark background, light text, accent colors)
3. Priority order: MessageComposer (most visible) → ChannelHeader → DirectMessageView → ChannelSettingsDialog → remaining 4
4. Key styling needs:
   - Channel list items: `cursor: pointer`, hover/active states, proper padding
   - Message composer: styled toolbar, input area, send button, emoji picker
   - Buttons (Pin/Edit/Archive/Leave/Members): styled to match app button classes
   - Messages: avatar placeholders, proper spacing, timestamp styling
5. Rebuild, redeploy, restart service
6. Update this handoff with results

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
