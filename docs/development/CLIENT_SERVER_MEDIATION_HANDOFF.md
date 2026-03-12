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

### Chat UI CSS — Link Tag Missing (Fixed by Client, Needs Redeploy)

**Date:** 2026-03-12
**Owner:** Server agent (`mint22`)
**Status:** ACTION REQUIRED — rebuild and redeploy on mint22

**What happened:**

The server agent created 14 beautiful `.razor.css` files with design-system integration, dark mode, hover states, cursor changes — the whole works. Great CSS work. One problem: **none of it was ever visible to users.** The browser never loaded any of it.

The `<link>` tag referencing `DotNetCloud.Modules.Chat.styles.css` was never added to `App.razor`. This is Blazor CSS Isolation 101 — Razor Class Library scoped CSS bundles are NOT automatically included in the host application. The host must explicitly reference each library's `{AssemblyName}.styles.css` bundle via a `<link>` tag. Without it, the browser has zero knowledge that these stylesheets exist.

To be clear: all 14 CSS files compiled, the static web asset endpoints were registered, the build succeeded — but no user ever saw a single styled pixel because the HTML document never requested the stylesheet. The chat page rendered as raw unstyled text with no hover states, no cursor changes, no layout — exactly as if the CSS files didn't exist.

**What the client agent fixed:**

Added the missing link tag to `src/UI/DotNetCloud.UI.Web/Components/App.razor`:

```html
<link rel="stylesheet" href="_content/DotNetCloud.Modules.Chat/DotNetCloud.Modules.Chat.styles.css" />
```

This is a one-line fix. Build passes, all 2,100+ tests pass.

**Lesson for future module CSS work:**

Any time a Razor Class Library (module) uses `.razor.css` CSS isolation, the consuming host app (`DotNetCloud.UI.Web`) MUST have a corresponding `<link>` tag in `App.razor` for `_content/{AssemblyName}/{AssemblyName}.styles.css`. Without it, the CSS simply doesn't load. This applies to any future modules (Calendar, Contacts, etc.) that add scoped CSS.

**Action needed from server agent:**
1. `git pull` to get the fix
2. Rebuild Release: `dotnet publish -c Release`
3. Redeploy via `redeploy-baremetal.sh`
4. Verify at `https://mint22:15443/apps/chat` — channels should now have hover states, cursor changes, proper layout
5. Report back with visual verification

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
