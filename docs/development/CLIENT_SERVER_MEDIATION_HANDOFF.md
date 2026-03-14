# Client/Server Mediation Handoff

Last updated: 2026-03-14 (archived older execution trail; single active mint-dnc-client runtime verification task remains)

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
- Linux bring-up engineering fixes are complete (non-root path fallback, per-user singleton guards, reconciliation hardening, and sync re-entry coalescing).
- Remaining open item is runtime validation on `mint-dnc-client` for one clean Linux E2E sync cycle with no immediate churn.

## Environment

| Role | Machine | Detail |
|---|---|---|
| Server | `mint22` | `https://mint22:15443/` |
| Client | `Windows11-TestDNC` | Sync dir: `C:\Users\benk\Documents\synctray` |
| Client | `mint-dnc-client` | Linux Mint 22 validation host for desktop sync client implementation + E2E testing |

## Key Carry-Forward Contracts

- Auth: OpenIddict bearer on files/sync endpoints via `FilesControllerBase` `[Authorize]`.
- API envelope: middleware wraps responses; clients should unwrap via envelope helpers.
- Sync flow: changes -> tree -> reconcile -> chunk manifest -> chunk download -> file assembly.
- Desktop OAuth constant: `OAuthConstants.ClientId = "dotnetcloud-desktop"`.

## Active Handoff

### Upload Chunk Failure Diagnostic — Client Log Capture on `mint-dnc-client`

**Date:** 2026-03-14
**Owner:** Client agent on `mint-dnc-client`
**Status:** ACTIVE — awaiting client-side log capture

**Context:** P0 sync hardening fixes (atomic SyncSequence, unique file name constraint, atomic chunk refcount) have been deployed to `mint22`. During P0 validation testing, we discovered that **client uploads initiate successfully but chunk data never reaches the server**. This affects both Linux and Windows clients. All previous file uploads were done via web UI — this is the first time the client-side chunked upload path has been exercised in production.

**Server-side evidence (mint22):**
- Server logs show `upload/initiate` returning 201 for test files (`seq-test-linux.txt`, `seq-test-windows.txt`)
- Server logs say "0/1 chunks already exist" — meaning 1 chunk needs uploading
- **Zero** `PUT /api/v1/files/upload/{sessionId}/chunks/{chunkHash}` requests arrive at the server
- **Zero** `CompleteUpload` calls arrive
- Upload sessions stuck at `Status=InProgress`, `ReceivedChunks=0`
- No 429s, no errors, no rejections in server logs — the chunk requests simply never arrive

**Hypothesis:** Client-side `ChunkedTransferClient.UploadAsync` initiates the session, but the chunk upload loop (producer-consumer channel → `_api.UploadChunkAsync`) either:
1. Silently fails with a non-retryable HTTP error (4xx other than 409)
2. GZip content-encoding on chunk PUT is rejected by server middleware
3. CDC chunking produces a hash mismatch between metadata pass and upload pass
4. An exception is swallowed by `SyncEngine.ApplyLocalChangesAsync` generic catch

#### Scope (Client Agent on `mint-dnc-client`)

1. **Pull latest `main`** on `mint-dnc-client`.

2. **Find and capture recent sync service logs** for the upload attempts:
   - Look in `~/.local/share/DotNetCloud/` or `~/.config/DotNetCloud/` or wherever `sync-service*.log` files are stored.
   - Search logs for `seq-test-linux.txt` — capture ALL log lines from the upload attempt, including:
     - `File upload starting` messages
     - `ComputeChunkMetadata` / chunk count messages
     - `InitiateUploadAsync` response (session ID, present chunks)
     - Any `UploadChunkAsync` attempt or failure
     - Any `HttpRequestException` or error after initiate
     - Any `File upload failed` messages
     - Any `ApplyLocalChangesAsync` retry/error messages

3. **If logs don't show enough detail**, temporarily increase client logging:
   - In the sync service's `appsettings.json` or logging config, set `DotNetCloud.Client.Core.Transfer` to `Debug` level.
   - Drop a new test file (e.g., `diag-test.txt` with content "diagnostic test") into `~/synctray/`.
   - Wait for sync to attempt upload.
   - Capture the full debug-level log output for the upload attempt.

4. **Check the HTTP response** the client gets from `PUT .../chunks/{hash}`:
   - If the chunk PUT returns a non-200 status, capture the exact status code and response body.
   - Check if the server's `Content-Encoding: gzip` handling is rejecting the request.

#### Required Evidence Back in Next Handoff Update

- Exact log file path and relevant timestamped log lines (full, not excerpted)
- HTTP status code returned by chunk upload PUT (if any request was made)
- Any exceptions/stack traces from the upload flow
- The session ID from `InitiateUploadAsync` response
- Whether `UploadChunkAsync` was ever called (or if it failed before reaching the HTTP call)
- Whether the file was moved to the failed/retry queue by SyncEngine

#### Exit Criteria

- Root cause of chunk upload failure identified from client logs
- If client-side bug found: describe the exact failure point so server agent can fix code and redeploy
- If server-side issue: describe the HTTP response so server agent can investigate

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
