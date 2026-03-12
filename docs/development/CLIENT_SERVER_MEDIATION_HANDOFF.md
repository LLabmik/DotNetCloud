# Client/Server Mediation Handoff

Last updated: 2026-03-12 (Phase 2.10 fully closed — all client items complete)

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
- Full test suite: 2,095 passed / 0 failed / 13 skipped (env-gated).

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

### Phase 2 COMPLETE — Ready for Phase 3 Planning

**Date:** 2026-03-12
**Owner:** Client agent (`Windows11-TestDNC`)
**Status:** COMPLETE ✅

**What was completed (client-side, this session):**
1. **Notification badges on app icon:** Created `AppBadgeManager` static utility (`src/Clients/DotNetCloud.Client.Android/Services/AppBadgeManager.cs`) with `WithBadgeCount()` extension method that calls `SetNumber()` on `Notification.Builder`. Wired into both `FcmMessagingService` (Google Play) and `UnifiedPushReceiver` (F-Droid) notification builders. Supported launchers (Samsung One UI, Pixel, etc.) display numeric badge count.
2. **Direct APK download option:** Expanded `docs/clients/android/DISTRIBUTION.md` with GitHub Releases download section, sideloading instructions (checksum verification, enable unknown sources), and enterprise MDM distribution guidance.
3. **App store listing description:** Added full Google Play listing (title, short description, categorized full description with feature bullets) and F-Droid metadata cross-reference to DISTRIBUTION.md.
4. Updated `IMPLEMENTATION_CHECKLIST.md` (3 items marked ✓) and `MASTER_PROJECT_PLAN.md` (Phase 2.10 → 8/8 complete).

**Files changed:**
- `src/Clients/DotNetCloud.Client.Android/Services/AppBadgeManager.cs` (new)
- `src/Clients/DotNetCloud.Client.Android/Platforms/Android/FcmMessagingService.cs` (added `WithBadgeCount()`)
- `src/Clients/DotNetCloud.Client.Android/Platforms/Android/UnifiedPushReceiver.cs` (added `WithBadgeCount()`)
- `docs/clients/android/DISTRIBUTION.md` (expanded Direct APK + App Store Listing sections)
- `docs/IMPLEMENTATION_CHECKLIST.md` (Phase 2.10 items marked complete)
- `docs/MASTER_PROJECT_PLAN.md` (Phase 2.10 fully complete)

**Test suite:** 2,095 passed / 0 failed / 13 skipped (env-gated). Build: 0 errors.

**Next action:**
- All Phase 2 work is complete (Phases 2.1–2.13, all deliverables shipped).
- Next step: Begin Phase 3 planning, or address any remaining Phase 0/1 gaps if prioritized.

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
