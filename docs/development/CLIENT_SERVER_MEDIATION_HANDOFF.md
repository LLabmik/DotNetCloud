# Client/Server Mediation Handoff

Last updated: 2026-03-14 (client chunk-upload diagnostics captured on mint-dnc-client; active task moved to server-side 409 investigation)

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
- Client diagnostics on `mint-dnc-client` confirmed chunk PUTs are sent; server responds `409 Conflict` for chunk PUT and complete calls during upload attempts.
- Active blocker is now server-side investigation on `mint22` for why `upload/initiate` reports missing chunks while `PUT .../chunks/{hash}` immediately returns `409`.

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

### Chunk PUT 409 Conflict Investigation on `mint22`

**Date:** 2026-03-14
**Owner:** Server agent on `mint22`
**Status:** ACTIVE — client diagnostics complete, server investigation required

**What changed:** Client-side log capture on `mint-dnc-client` is complete. It disproves the original theory that chunk PUT requests never leave the client.

**Diagnostic evidence captured (client, `mint-dnc-client`):**
- Log file path: `/home/benk/.local/share/DotNetCloud/logs/sync-service20260314.log`
- `UploadChunkAsync` path is executed and reaches HTTP layer.
- `InitiateUploadAsync` for `seq-test-linux.txt` returns `201` with a new session and one missing chunk:
  - `{"@t":"2026-03-14T23:28:37.1661843Z","@mt":"ReadEnvelopeDataAsync<{TypeName}>: HTTP {StatusCode}, ContentType={ContentType}, BodyLength={Length}, BodyPreview={Preview}","TypeName":"UploadSessionResponse","StatusCode":201,"ContentType":"application/json","Length":267,"BodyPreview":"{\"success\":true,\"data\":{\"sessionId\":\"d32c8036-ee00-4de4-bb52-8a2fd7f61504\",\"existingChunks\":[],\"missingChunks\":[\"5d7383609c20886a2b19d783205b90d3d28a64c6efa6c66f4c7ffa462fe50bea\"],\"expiresAt\":\"2026-03-15T23:28:37.1630095Z\"},\"timestamp\":\"2026-03-14T23:28:37.1669161Z\"}","SourceContext":"DotNetCloud.Client.Core.Api.DotNetCloudApiClient"}`
- Immediately after initiate, client sends chunk PUT for that exact session/chunk and receives `409`:
  - `{"@t":"2026-03-14T23:28:37.1714667Z","@mt":"API call {Method} {Url} RequestId={RequestId}","Method":"PUT","Url":"https://mint22:15443/api/v1/files/upload/d32c8036-ee00-4de4-bb52-8a2fd7f61504/chunks/5d7383609c20886a2b19d783205b90d3d28a64c6efa6c66f4c7ffa462fe50bea","RequestId":"2907dc47071c4b63b14da31a43a655f3","SourceContext":"DotNetCloud.Client.Core.Api.CorrelationIdHandler","HttpMethod":"PUT","Uri":"https://mint22:15443/api/v1/files/upload/d32c8036-ee00-4de4-bb52-8a2fd7f61504/chunks/5d7383609c20886a2b19d783205b90d3d28a64c6efa6c66f4c7ffa462fe50bea","Scope":["HTTP PUT https://mint22:15443/api/v1/files/upload/d32c8036-ee00-4de4-bb52-8a2fd7f61504/chunks/5d7383609c20886a2b19d783205b90d3d28a64c6efa6c66f4c7ffa462fe50bea"]}`
  - `{"@t":"2026-03-14T23:28:37.1773411Z","@mt":"Received HTTP response headers after {ElapsedMilliseconds}ms - {StatusCode}","ElapsedMilliseconds":5.813,"StatusCode":409,"EventId":{"Id":101,"Name":"RequestEnd"},"SourceContext":"System.Net.Http.HttpClient.DotNetCloudSync.ClientHandler","HttpMethod":"PUT","Uri":"https://mint22:15443/api/v1/files/upload/d32c8036-ee00-4de4-bb52-8a2fd7f61504/chunks/5d7383609c20886a2b19d783205b90d3d28a64c6efa6c66f4c7ffa462fe50bea","Scope":["HTTP PUT https://mint22:15443/api/v1/files/upload/d32c8036-ee00-4de4-bb52-8a2fd7f61504/chunks/5d7383609c20886a2b19d783205b90d3d28a64c6efa6c66f4c7ffa462fe50bea"]}`
- Complete call also returns `409` and client treats as success:
  - `{"@t":"2026-03-14T23:28:37.1895467Z","@mt":"CompleteUpload returned 409 for {FileName} — file already exists on server. Treating as success.","@l":"Warning","FileName":"seq-test-linux.txt","SourceContext":"DotNetCloud.Client.Core.Transfer.ChunkedTransferClient"}`
- No `File upload failed` entry for `seq-test-linux.txt`; no `ApplyLocalChangesAsync` exception tied to this file; sync pass reports local apply completed.

**Conclusion from client diagnostics:**
- The client does call chunk upload (`UploadChunkAsync`) and the request reaches `mint22`.
- This is not a client-side silent drop and not evidence of gzip middleware rejection.
- There is a server-side contract inconsistency: initiate says chunk is missing, then chunk PUT returns conflict.

#### Scope (Server Agent on `mint22`)

1. Reproduce with the exact failing tuple from logs:
   - `sessionId`: `d32c8036-ee00-4de4-bb52-8a2fd7f61504`
   - `chunkHash`: `5d7383609c20886a2b19d783205b90d3d28a64c6efa6c66f4c7ffa462fe50bea`
   - Endpoint: `PUT /api/v1/files/upload/{sessionId}/chunks/{chunkHash}`

2. Trace server path for `UploadChunk` conflict decisions:
   - Validate whether `409` means global chunk already exists, duplicate within session, invalid session ownership/state, or file-name conflict leakage.
   - Confirm whether response should be `200/204` for idempotent existing chunk instead of `409` when session requests that chunk.

3. Verify session/chunk state transitions in DB for the same request window:
   - upload session status,
   - received chunk entries,
   - chunk reference counts,
   - any unique constraint or concurrency conflict.

4. Implement fix so initiate/PUT contract is coherent:
   - If `missingChunks` includes hash, `PUT` should succeed idempotently.
   - If chunk already exists globally, it should be attachable without hard-fail semantics.

5. Redeploy and provide runtime verification evidence from current binaries (not stale publish output), per runtime verification gate.

#### Required Evidence Back in Next Handoff Update

- Server log lines for the exact `PUT /chunks/{hash}` handling path (timestamped)
- Branch/commit hash containing the server fix
- Runtime verification command proving current binaries are running
- Post-fix request/response sample showing coherent initiate -> PUT -> complete behavior
- Any schema/constraint notes if migration changes were required

#### Exit Criteria

- `upload/initiate` + `PUT chunk` + `complete` path is internally consistent on `mint22`
- Chunk PUT no longer fails with contradictory `409` when chunk was reported missing
- Client can finish upload flow without conflict-loop semantics

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
