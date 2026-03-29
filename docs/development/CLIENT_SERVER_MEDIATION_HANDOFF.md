# Client/Server Mediation Handoff

Last updated: 20260329 (SyncTray icon enhancement — add status symbols to tray icons)

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
- Duplicate controller fix: CLOSED (2026-03-18). Deployed and verified on `mint22`. Files endpoint returns 401, service healthy.
- Windows IIS + Service Validation: **COMPLETE** (2026-03-21). Three startup blockers resolved. IIS reverse proxy configured and verified (URL Rewrite + ARR). HTTP (port 80) and HTTPS (port 443) both proxy to Kestrel :5080. Self-signed localhost cert bound.
- File browser child count fix: **DEPLOYED** (2026-03-21). `mint22` redeployed; service stable.
- `mint22` connectivity diagnosis: **COMPLETE** (2026-03-22). Current deployment listens directly on HTTPS `:5443`; no listener exists on `:15443`.
- Security audit desktop client validation on `Windows11-TestDNC`: **COMPLETE** (2026-03-23).
- Security audit closeout + merge validation on `mint22`: **COMPLETE** (2026-03-23).
- Post-closeout Windows runtime smoke: **COMPLETE** (2026-03-23). 4/4 targeted tests passed; login launch path verified reachable.
- **Active cycle (20260328–20260329):** WS-4 live verification 58/66 passed. Windows Phase C complete (8 pass, 2 deferred, 1 skip). Linux Phase C complete (10 pass, 1 skip). Both deferred Windows tests (TC-1.52 conflict, TC-1.53 offline) passed on Linux.
- **Current (20260329):** SyncTray icon enhancement — adding white status symbols to colored circle tray icons. Starting on Windows.

## Environment

| Role | Machine | Detail |
|---|---|---|
| Server | `mint22` | `https://mint22:5443/` |
| Client | `Windows11-TestDNC` | Sync dir: `C:\Users\benk\Documents\synctray` |
| Client | `mint-dnc-client` | Linux Mint 22 validation host for desktop sync client implementation + E2E testing |
| Android Client | `monolith` | Android MAUI app development + emulator testing (Windows 11) |

## Key Carry-Forward Contracts

- Auth: OpenIddict bearer on files/sync endpoints via `FilesControllerBase` `[Authorize(AuthenticationSchemes = "OpenIddict.Validation.AspNetCore")]`.
- API envelope: middleware wraps responses; clients should unwrap via envelope helpers.
- Sync flow: changes -> tree -> reconcile -> chunk manifest -> chunk download -> file assembly.
- Desktop OAuth constant: `OAuthConstants.ClientId = "dotnetcloud-desktop"`.
- **SignalR channel group naming:** `chat-channel-{channelId}` (used by `ChatRealtimeService.ChannelGroup()` and Android `SignalRChatClient`).
- **Controller discovery:** Core.Server references Files.Host and Chat.Host via `ProjectReference`. ASP.NET Core auto-discovers controllers from referenced assemblies. Do NOT create duplicate controllers in Core.Server for routes already served by module Host assemblies.

## Active Handoff

**Target machine:** Windows11-TestDNC
**Status:** READY FOR PICKUP
**Context:** SyncTray tray icon enhancement — add white status symbols to colored circles

### Overview

The current SyncTray tray icons are plain colored circles. We're adding a **white symbol overlay** to each circle so the status is immediately clear without relying solely on color. This also improves accessibility for colorblind users.

**Full design plan:** `docs/SYNCTRAY_ICON_ENHANCEMENT_PLAN.md`

### Color + Symbol Mapping (Updated)

| TrayState | Color | Hex | Symbol | Description |
|-----------|-------|-----|--------|-------------|
| **Idle** | Green | `#00B040` | ✓ Checkmark | Two joined line segments |
| **Syncing** | Blue | `#0078D4` | ⟳ Sync arrows | Two opposing curved/straight arrows |
| **Paused** | RebeccaPurple | `#663399` | ⏸ Pause bars | Two vertical rectangles |
| **Error** | Crimson | `#C41E3A` | ✕ X mark | Two crossing diagonal lines |
| **Conflict** | Dark Orange | `#FF8C00` | ! Exclamation | Vertical line + dot |
| **Offline** | Grey | `#707070` | — Dash | Horizontal line |

**Color changes from current code:**
- `Paused` changed from Amber `#FFA500` → RebeccaPurple `#663399` (was too similar to Conflict)

### Implementation Instructions

**File to modify:** `src/Clients/DotNetCloud.Client.SyncTray/TrayIconManager.cs`

**Approach:** Pixel-level drawing (extend existing `CreateCircleBitmap()` approach). No new dependencies.

1. **Update the color mapping** in `CreateStatusIcon()`:
   - Change `TrayState.Paused` from `(0xFF, 0xA5, 0x00)` to `(0x66, 0x33, 0x99)` (RebeccaPurple)

2. **Add a reusable anti-aliased line helper:**
   ```csharp
   private static void DrawAntiAliasedLine(byte[] pixels, int size, float x0, float y0, float x1, float y1, float thickness, byte r, byte g, byte b)
   ```
   - Xiaolin Wu's algorithm or distance-from-line approach
   - Used for: checkmark, X mark, dash, sync arrows

3. **Add a filled-circle-at helper** (for exclamation dot):
   ```csharp
   private static void DrawFilledCircleAt(byte[] pixels, int size, float cx, float cy, float radius, byte r, byte g, byte b)
   ```

4. **Add per-state symbol drawing methods** (all draw white `255,255,255` into the pixel buffer):
   - `DrawCheckmark()` — two line segments joined at (~12, 20), short leg up-left, long leg up-right, stroke ~2.5px
   - `DrawSyncArrows()` — two opposing arrows suggesting circular motion, stroke ~2px
   - `DrawPauseBars()` — two filled rectangles (x=11-13 and x=18-20, y=9-22)
   - `DrawXMark()` — two diagonal lines crossing at center, stroke ~2.5px
   - `DrawExclamation()` — vertical line (y=8-18) + dot at (15.5, 22)
   - `DrawDash()` — single horizontal line at vertical center, stroke ~2.5px

5. **Wire it into `CreateCircleBitmap()`** — call `DrawStatusSymbol()` after the circle loop, before the badge loop. Pass the `TrayState` into the method (add parameter).

6. **Rendering order must be:** Circle → Symbol → Chat badge (badge on top of everything)

### Symbol Rendering Specs

- **Canvas:** 32×32 pixels, circle radius ~14px
- **Symbol area:** ~50-60% of circle diameter (symbols span roughly 14-16px)
- **Symbol color:** White `(255, 255, 255)` — premultiplied alpha, composited over the circle color
- **Anti-aliasing:** Required on all symbol edges for professional appearance
- **Only draw white pixels inside the circle bounds** (don't bleed outside the circle edge)

### Testing Checklist

After implementation, visually verify all 6 states. To trigger each state:
- **Idle:** Normal state after sync completes
- **Syncing:** Drop a file in the sync folder
- **Paused:** Pause sync from tray menu
- **Error:** Temporarily break server connectivity (wrong URL in config)
- **Conflict:** Modify same file on both client and server between syncs
- **Offline:** Stop the SyncService

Verify:
- ☐ All 6 symbols render clearly at 32×32
- ☐ Symbols are centered within the circle
- ☐ Anti-aliasing looks clean (no jagged edges)
- ☐ Chat badge (if present) renders on top of symbols
- ☐ Colors are correct (especially Paused = purple, not amber)
- ☐ No visual artifacts or bleeding outside circle

### After Completion

Commit, push, and provide relay message for `mint-dnc-client` to verify the same icons on Linux.
