# Client/Server Mediation Handoff

Last updated: 2026-03-14 (server gzip request decompression fix deployed; client upload retry verification needed on mint-dnc-client)

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
- Linux bring-up engineering fixes are complete.
- P0 sync hardening (atomic SyncSequence, unique name constraint, atomic chunk refcount) deployed.
- **Server gzip fix deployed (af66b41):** `UseRequestDecompression()` middleware added. Server now auto-decompresses `Content-Encoding: gzip` request bodies before controllers read them. The chunk PUT hash mismatch that caused false 409 is resolved.
- Active task: client-side upload retry verification on `mint-dnc-client` to confirm end-to-end upload flow works.

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

### Upload Retry Verification on `mint-dnc-client`

**Date:** 2026-03-14
**Owner:** Client agent on `mint-dnc-client`
**Status:** ACTIVE — server fix deployed, needs client-side verification

**What changed on server (`mint22`):**
- Commit `af66b41`: Added `AddRequestDecompression()` / `UseRequestDecompression()` to `Program.cs`.
- Server now auto-decompresses `Content-Encoding: gzip` request bodies before controllers read `Request.Body`.
- Previously: client gzip-compressed chunk data → server read raw gzip bytes → SHA-256 hash of compressed data ≠ declared chunk hash → `ValidationException` → 409 Conflict → client treated as "already exists" → upload never completed.
- Now: middleware decompresses before hash validation → hash matches → chunk stored → upload completes.
- Server redeployed and healthy on `mint22:15443` (PID 85460, started 2026-03-14T19:05:02-05:00).

#### Scope (Client Agent on `mint-dnc-client`)

1. Pull `main` (commit `af66b41` or later).

2. Create a fresh test file in the sync folder:
   ```bash
   echo "upload-verify-$(date +%s)" > ~/synctray/upload-verify-test.txt
   ```

3. Wait for sync service to detect the new file and attempt upload (or restart the sync service to trigger immediate sync).

4. Check client logs for the upload flow:
   - `upload/initiate` → should return 201 with `missingChunks` containing the hash
   - `PUT .../chunks/{hash}` → should return **200** (not 409)
   - `POST .../complete` → should return **200** with the created `FileNode`

5. Verify the file appears in the server's file tree:
   ```bash
   # From mint-dnc-client, using the sync service's auth token or via curl:
   # Check server DB for the uploaded file (or use the web UI on mint22:15443)
   ```

6. If upload succeeds, also verify downloads work by checking if existing server-side files download correctly to the sync folder.

#### Required Evidence Back in Next Handoff Update

- Client log lines showing successful `initiate` → `chunk PUT 200` → `complete 200` sequence (timestamped)
- Confirmation that `upload-verify-test.txt` exists on server (DB query or web UI screenshot description)
- Any remaining errors or unexpected behavior
- Client commit hash if any client-side changes were needed

#### Exit Criteria

- At least one new file uploaded successfully from `mint-dnc-client` through the full `initiate → chunk PUT → complete` path
- No false 409 on chunk PUT
- Upload completion returns a valid `FileNode` response

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
