# Client/Server Mediation Handoff

Last updated: 2026-06-23 19:30 UTC (Windows11-TestDNC: 429 fix verified, CompleteUpload 409 + 502 YARP issues found — handoff to server)

Purpose: shared handoff between client-side and server-side agents, mediated by user.

Archived context:

- Historical completed updates are in `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- Additional history remains available in git.
- VFS Phase 3 (Windows Cloud Filter API) completed on Windows11-TestDNC (2026-05-12).
- VFS Phase 2 (core abstraction layer) completed on Windows11-TestDNC (previously).

## Process Rules

**Agent autonomy (CRITICAL):**

- Both client and server agents work autonomously — they do NOT ask the moderator for context or permission.
- Agents pull the branch specified in the relay message, read the **Active Handoff** section, and execute the work described there independently.
- All actionable items, blockers, and technical details go directly in this document.
- **Current active branch:** `main`
- No moderator involvement in technical decisions, code reviews, or work coordination.

**Role separation (MANDATORY):**

- **Client code** (`src/Clients/`, `src/UI/`) is handled ONLY by client machines (`mint-OptiPlex-7010`, `Windows11-TestDNC`, `mint-dnc-client`, `monolith`).
- **Server code** (`src/Core/`, `src/Modules/`) is handled ONLY by server machines (`cloud.kimball.home`, `mint22`).
- Each agent ONLY executes actions in the block matching their machine name (from the Environment table).
- If no action block matches your machine, the handoff is not for you — relay it to the moderator.
- Never cross role boundaries: a client agent never deploys server code, a server agent never builds client apps.

**Active Handoff format (MANDATORY):**

Every Active Handoff MUST use per-machine action blocks. Actions are grouped by the machine that executes them, using the exact machine names from the Environment table.

```markdown
### Active Handoff

**Summary:** [one-line description of what's happening]

[Context/background — what changed, why, relevant commits]

---

### Server Actions — `cloud.kimball.home`

- [ ] Action 1 with exact commands
- [ ] Action 2

### Client Actions — `mint-OptiPlex-7010`

- [ ] Action 1 with exact commands
- [ ] Action 2
```

**Critical rules:**
- Each agent ONLY executes actions in the block matching their machine name (from the Environment table).
- If no action block matches your machine, the handoff is not for you — relay it to the moderator.
- Always include exact commands (ready to copy-paste).
- Mark blocks with `✓` when complete; update status inline.
- One handoff may have 1 or 2 action blocks depending on workflow stage.

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
- Moderator handoff prompt rule (MANDATORY): every ready-to-relay message must explicitly state the target machine name (for example: `cloud.kimball.home`, `mint-dnc-client`, `Windows11-TestDNC`).
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

- YARP auth header doubling fix: deployed, verified server-side (`cloud.kimball.home`), verified client-side (`Windows11-TestDNC`) — 401 resolved.
- X-Device-Id header duplication fix: deployed on `cloud.kimball.home` (commit `d1dd3746`). Service restarted, all 14 modules healthy.
- X-Device-Id fix verified on `Windows11-TestDNC`: SyncTray connects to `cloud.dotnetcloud.net` successfully, no 401/502 errors, no X-Device-Id warnings. (Archived.)
- Large file upload 429 rate limiting: **root cause found and fixed** on `cloud.kimball.home` (commit `73babfe5`). Core.Server's default auth scheme only handled cookies — Bearer token requests were anonymous, falling to IP-based 100 req/60s global limit. Added policy scheme (`DotNetCloud.Policy`) forwarding Bearer tokens to OpenIddict validation. Verified with diagnostic logging.
- **429 fix verified on `Windows11-TestDNC`:** SyncTray with client-side mitigations (sequential chunks, coordinated backoff, active session guard, empty NodeId cleanup, ListChildrenAsync URL fix) tested against `cloud.dotnetcloud.net`. **No 429 errors occurred** during large file (1.17 GB PDF) upload. ✅ Rate limiting fix is working end-to-end.
- **Secondary issues found during verification:**
  - **502 Bad Gateway (YARP):** Transient 502 errors from reverse proxy during chunk uploads. Client retries (3× exponential backoff) recover successfully. Not a client bug — YARP/reverse proxy infrastructure issue on `cloud.dotnetcloud.net`.
  - **CompleteUpload 409 → Guid.Empty:** After all chunks uploaded, `CompleteUpload` returns HTTP 409 (server's `ChunkedUploadService` throws `NameConflictException` for case-insensitive name conflict or `InvalidOperationException` for expired session). Client's 409 handler calls `ListChildrenAsync(parentFolderId=e0504d16-83fd-4be7-8b3c-40fab83f63cd)` but cannot find the file in the response. Stores `Guid.Empty` locally. Sync pass ends with `LocalQueued=4`. **Needs server-side investigation.**
- All prior Phase 2, chat, pre-Linux sync remediation, SyncTray icon enhancement work is complete and archived.
- VFS Phase 1 (server-side prerequisites) complete on `cloud.kimball.home`.
- VFS Phase 2 (core abstraction layer) complete on `Windows11-TestDNC`.
- VFS Phase 3 (Windows Cloud Filter API) complete on `Windows11-TestDNC`.
- VFS Phase 4 (Linux FUSE) complete on `mint-dnc-client`.
- VFS Phase 5 (SyncTray UI Integration) complete on `Windows11-TestDNC` (archived).
- VFS Phase 6 (Testing & Validation) complete on `Windows11-TestDNC`.

## Environment

| Role           | Machine              | Detail                                                                             |
| -------------- | -------------------- | ---------------------------------------------------------------------------------- |
| Server         | `cloud.kimball.home` | `https://cloud.dotnetcloud.net/` (production)                                      |
| Server         | `mint22`             | `https://mint22:5443/` (dev)                                                       |
| Client         | `Windows11-TestDNC`  | Sync dir: `C:\Users\benk\Documents\synctray`                                       |
| Client         | `mint-dnc-client`    | Linux Mint 22 validation host for desktop sync client implementation + E2E testing |
| Client         | `mint-OptiPlex-7010` | production client connected to `cloud.dotnetcloud.net`              |
| Android Client | `monolith`           | Android MAUI app development + emulator testing (Windows 11)                       |

## Key Carry-Forward Contracts

- Auth: Files module host uses a policy scheme (`DotNetCloud.Module`) that auto-selects between `OpenIddict.Validation.AspNetCore` (JWT Bearer) and `Identity.Application` (cookie) based on the `Authorization` header. Controllers use plain `[Authorize]`. All module hosts must follow this pattern.
- API envelope: middleware wraps responses; clients should unwrap via envelope helpers.
- Sync flow: changes -> tree -> reconcile -> chunk manifest -> chunk download -> file assembly.
- Desktop OAuth constant: `OAuthConstants.ClientId = "dotnetcloud-desktop"`.
- **SignalR channel group naming:** `chat-channel-{channelId}` (used by `ChatRealtimeService.ChannelGroup()` and Android `SignalRChatClient`).
- **Controller discovery:** Core.Server references Files.Host and Chat.Host via `ProjectReference`. ASP.NET Core auto-discovers controllers from referenced assemblies. Do NOT create duplicate controllers in Core.Server for routes already served by module Host assemblies.

## Active Handoff

**Summary:** 🔴 Large file (1.17 GB PDF) upload — chunks upload without 429s ✅, but `CompleteUpload` returns HTTP 409 and client stores `Guid.Empty`. File not visible on server. Also, transient 502 Bad Gateway from YARP during chunk uploads. Handing off to server for investigation.

**Client verification results (Windows11-TestDNC, branch `perf/synctray-scan-and-transfer-speedups`):**

| Check | Result | Details |
|-------|--------|---------|
| 429 errors | ✅ **None** | Rate limiting fix works. Zero 429s during entire upload. |
| Chunk upload | ✅ **All uploaded** | Sequential single-chunk uploads, all chunks either uploaded or content-addressed dedup (409). |
| 502 Bad Gateway | ⚠️ **Transient** | One chunk (`632798c386...`) got 2× 502 on retries, recovered on 3rd attempt. Client retry mechanism (3× exponential backoff with jitter) works correctly. |
| CompleteUpload | ❌ **409 Conflict** | Server returns 409. `ChunkedUploadService` only returns 409 for `NameConflictException` (case-insensitive name conflict) or `InvalidOperationException` (expired session). |
| NodeId lookup | ❌ **Guid.Empty** | Client's 409 handler calls `ListChildrenAsync(parentFolderId=e0504d16-83fd-4be7-8b3c-40fab83f63cd)` — response body is 3338 bytes (other files present) but the large PDF is NOT in the listing. Stores `Guid.Empty`. |
| File on server | ❌ **Not visible** | User confirmed file does not appear on `cloud.dotnetcloud.net` web UI. |
| Sync pass result | ⚠️ `LocalQueued=4` | 4 items remain queued. `Guid.Empty` recovery path in SyncEngine (lines 526–575) will retry lookup on next pass but will likely fail again. |

**Client-side investigation summary:**

1. **502 Bad Gateway (YARP):** `UploadChunkAsync` (`DotNetCloudApiClient.cs` line 222) calls `EnsureSuccessStatusCode()` directly (no retry in the API client itself). Retry is handled by the consumer loop in `ChunkedTransferClient.cs` (lines 251–256) which catches `HttpRequestException` with StatusCode ≥ 500 and retries up to 3× with exponential backoff (`3^(n-1)` + up to 1s jitter). The 502s are a **YARP/reverse proxy infrastructure issue** on `cloud.dotnetcloud.net` — the client handles them correctly with recovery.

2. **CompleteUpload 409 behavior:** The server's `ChunkedUploadService.CompleteUploadAsync` (`src/Modules/Files/DotNetCloud.Modules.Files.Data/Services/ChunkedUploadService.cs` lines 228–494) returns HTTP 409 ONLY in these scenarios:
   - `NameConflictException` (line 321) → 409 with error code `FILE_NAME_CONFLICT` — a case-insensitive name match exists (`n.Name.ToLower() == session.FileName.ToLower() && n.Name != session.FileName`). `EnforceCaseInsensitiveUniqueness` must be enabled.
   - `InvalidOperationException` (via `GetActiveSessionAsync`) → 409 with error code `INVALID_OPERATION` — upload session expired/invalid.
   - **Exact name match** (line 295–310) is treated as a **version update** (200 OK, no conflict). Content-addressable dedup is handled at Initiate time, not Complete time.

3. **Client 409 handler** (`ChunkedTransferClient.cs` lines 322–369): After catching `HttpRequestException` with StatusCode 409, it calls `ListChildrenAsync(parentFolderId)` and does a case-insensitive lookup (`StringComparison.OrdinalIgnoreCase`). The parent folder `e0504d16-83fd-4be7-8b3c-40fab83f63cd` is correctly resolved via `EnsureParentFolderAsync`. The file should be found if it exists in that folder — but it's not there.

4. **Likely cause:** The file's chunks were uploaded during a prior session (before the 429 auth fix) but `CompleteUpload` never succeeded due to 429s. The chunks exist as orphaned blobs on the server. When the current session calls `CompleteUpload`, the server detects the content-addressable chunks already exist but no `FileNode` was ever created. However, the server's 409 response suggests a name/expiry conflict rather than chunk dedup.

**Actions needed — server side:**

---

### Server Actions — `cloud.kimball.home` or `mint22`

**Problem 1: Investigate CompleteUpload 409 for large PDF**

Check what is causing the 409 on CompleteUpload for `bb-The.Art.Of.Dejah.Thoris.And.The.Worlds.Of.Mars.Vol.2.HC.pdf`:

- [ ] Check server journal for the 409: `sudo journalctl -u dotnetcloud --since "2026-06-23 19:15" | grep -i "conflict\|409\|CompleteUpload\|NameConflict\|InvalidOperation\|bb-The.Art"`
- [ ] Determine which exception path produces the 409:
  - `NameConflictException` → check if a file with same name (different case) exists in parent folder `e0504d16-83fd-4be7-8b3c-40fab83f63cd`
  - `InvalidOperationException` → check if the upload session expired or was already completed
  - Direct DB query: `psql -d dotnetcloud -c "SELECT id, name, parent_id FROM file_nodes WHERE parent_id = 'e0504d16-83fd-4be7-8b3c-40fab83f63cd' AND name ILIKE '%Dejah%Thoris%';"`
- [ ] Check if orphaned upload sessions exist: `psql -d dotnetcloud -c "SELECT id, file_name, created_at, status FROM upload_sessions WHERE file_name ILIKE '%Dejah%Thoris%' ORDER BY created_at DESC;"`
- [ ] Verify the upload session `019ef713-da2e-7daf-b9ae-83bb806f5459` status: `psql -d dotnetcloud -c "SELECT id, file_name, status, target_file_node_id, created_at FROM upload_sessions WHERE id = '019ef713-da2e-7daf-b9ae-83bb806f5459';"`

**Problem 2: Investigate 502 Bad Gateway during chunk uploads**

- [ ] Check YARP/reverse proxy logs for the 502 errors: `sudo journalctl -u dotnetcloud --since "2026-06-23 19:18" | grep "502\|BadGateway\|upstream\|connection refused\|timeout"`
- [ ] Verify YARP destination health: `curl -s -o /dev/null -w "%{http_code}" https://cloud.dotnetcloud.net/health`
- [ ] Check if YARP destinations are all responsive: `sudo journalctl -u dotnetcloud | grep "Destination\|health\|unhealthy|failed"`

**Fix options (once root cause is identified):**

- If `NameConflictException`: Either delete the conflicting file, adjust `EnforceCaseInsensitiveUniqueness`, or improve server error response to include the existing NodeId.
- If `InvalidOperationException`: Extend session timeout or auto-create node when orphans are detected.
- If orphaned chunks without node: Consider modifying `CompleteUploadAsync` to handle the case where chunks exist but no FileNode was created — either create the node on dedup match or return a more descriptive error.
- For 502: Investigate YARP connection pooling, timeout settings, or module host availability.

**Verification:** After server fix, relay back to `Windows11-TestDNC` to:
1. Rebuild and run SyncTray
2. Trigger re-sync (the file's `Guid.Empty` record should trigger a fresh upload or resolve via the recovery path)
3. Confirm file appears on server
4. Confirm subsequent sync passes show `0 queued`

---

### Client Actions — `Windows11-TestDNC`

- [✓] `git pull` on `perf/synctray-scan-and-transfer-speedups` — done
- [✓] Build SyncTray Release — done (build succeeded)
- [✓] Verify no 429 errors — **confirmed: zero 429s** ✅
- [✓] Verify chunk uploads complete — **all chunks uploaded** ✅
- [✗] File appears correctly on server — **❌ not visible** — blocked by server-side 409 issue
- [✗] Verify subsequent sync passes — **blocked** — `LocalQueued=4`, `Guid.Empty` stored
- [✗] Check server rate limiter logs — **blocked** — server access required
