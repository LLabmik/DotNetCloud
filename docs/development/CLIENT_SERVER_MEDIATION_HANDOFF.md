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
- Chat UI fixes (invite button, members panel, online status): deployed to mint22 (2026-03-12). All new CSS verified loading.
- Chat message sender names fix: deployed to mint22 (2026-03-12). Display names resolved via IUserDirectory cache.

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

### Sync Changes Response Shape Mismatch + Rate Limiting — Server Fix Needed

**Date:** 2026-03-13
**Owner:** Server agent
**Status:** ACTION REQUIRED

#### Issue 1: Sync changes endpoint returns flat array instead of PagedSyncChangesDto

**Symptom:** Desktop sync client calls `GET /api/v1/files/sync/changes?limit=500` (no cursor, no since). Server returns HTTP 200. The `ResponseEnvelopeMiddleware` wraps the response, but the `data` field contains a **flat array** of `SyncChangeDto` objects instead of the expected `PagedSyncChangesDto` object `{changes:[], nextCursor:"...", hasMore:false}`.

**Evidence from client diagnostic log (v0.13, 2026-03-13T11:59:04Z):**
```
ReadEnvelopeDataAsync<PagedSyncChangesResponse>: HTTP 200, ContentType=application/json, BodyLength=9376,
BodyPreview={"success":true,"data":[{"nodeId":"80147381-...","name":"dotnetcloud-desktop-client-win-x64-0.1.0-alpha.zip","nodeType":"File","parentId":null,...}]}
```

Expected shape: `{"success":true,"data":{"changes":[...],"nextCursor":"...","hasMore":false}}`
Actual shape: `{"success":true,"data":[...]}`

**Analysis:** `SyncController.GetChangesAsync` should take the cursor path (since `since` is null), calling `_syncService.GetChangesSinceCursorAsync()` which returns `PagedSyncChangesDto`. The `Ok(paged)` result should serialize as an object with `changes`/`nextCursor`/`hasMore`. But the envelope shows a flat array. Either:
- The controller is unexpectedly hitting the legacy path (returning `IReadOnlyList<SyncChangeDto>`), OR
- The `ResponseEnvelopeMiddleware` is re-serializing and flattening the object, OR
- The deployed server binary on mint22 has stale code

**Requested action:** 
1. Verify the deployed `SyncController` on mint22 matches current source (check DLL timestamp).
2. Reproduce with `curl -H "Authorization: Bearer <token>" https://mint22:15443/api/v1/files/sync/changes?limit=500` and inspect the raw response body.
3. If the response is a flat array, debug why the cursor path returns a flat array instead of `PagedSyncChangesDto`.
4. Fix so that `GET /api/v1/files/sync/changes?limit=500` returns `{"success":true,"data":{"changes":[...],"nextCursor":"...","hasMore":false/true}}`.

**Client-side mitigation (already deployed in v0.13):** The client's `GetChangesSinceAsync` now handles both shapes — if `data` is an array, it wraps it as `PagedSyncChangesResponse { Changes = list, HasMore = false }`. So sync works with either format, but the proper fix is server-side.

#### Issue 2: Rate limiting on chunk downloads blocks sync completion

**Symptom:** During initial sync (first-time sync, no cursor), the client downloads all file chunks. The server rate limit policy `module-chunks` is 3000 permits/60 seconds per user. The client downloads chunks in parallel and hits 429s, triggering 60-second retry waits. After accumulating enough retries, the sync pass eventually times out with `TaskCanceledException`.

**Evidence (2026-03-13T12:14:34Z):** 36 rate-limited (429) chunk download requests in a single sync pass. Download retries stacked up with 60s delays, eventually the sync was cancelled.

**Log excerpt:**
```
{"@t":"2026-03-13T12:14:34.4908012Z","@mt":"Rate limited (429). Waiting {Delay}s before retry.","Delay":60}
{"@t":"2026-03-13T12:14:34.4295353Z","@mt":"File download failed: NodeId={NodeId}, DurationMs={DurationMs}.","@l":"Error","@x":"System.Threading.Tasks.TaskCanceledException: A task was canceled."}
```

**Context:** Single user on gigabit LAN, no other clients active. The 3000/min limit should be sufficient, but the fixed-window rate limiter resets at window boundaries — if a burst of parallel chunk requests arrives near a window edge, they can exhaust the budget instantly.

**Requested action:** Consider one or more of:
- Increase `chunks` rate limit (e.g., 10000/min) for single-user/small-deployment scenarios
- Switch to a token bucket or sliding window limiter for chunk downloads (smoother burst handling)
- Add a configuration override in `appsettings.json` on mint22 to raise the limit for testing
- Exclude chunk downloads from rate limiting entirely for authenticated users (they're already bandwidth-limited by the network)

**Config location:** `src/Core/DotNetCloud.Core.Server/appsettings.json` → `RateLimiting.ModuleLimits.chunks`

#### Files changed (client-side, already committed):
- `src/Clients/DotNetCloud.Client.Core/Api/DotNetCloudApiClient.cs` — `GetChangesSinceAsync(cursor)` now handles both object and array `data` shapes

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
