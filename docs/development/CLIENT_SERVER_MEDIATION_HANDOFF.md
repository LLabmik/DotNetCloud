# Client/Server Mediation Handoff

Last updated: 2026-06-23 17:11 UTC (Windows11-TestDNC: large file upload blocked by 429 rate limiting — handoff to server)

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
- Large file upload blocked by HTTP 429 rate limiting: investigated on `Windows11-TestDNC`. Client-side fixes applied (sequential chunk uploads, coordinated backoff, active session guard, empty NodeId cleanup, ListChildrenAsync URL fix). **Root cause is server-side — rate limiter hits even sequential single-chunk uploads.**
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

**Summary:** 🔴 Large file upload (1.17 GB PDF) blocked by HTTP 429 rate limiting on `cloud.dotnetcloud.net`. Chunk uploads fail with 429 even at sequential single-chunk rate. Client-side mitigations applied — root cause is server-side.

**Client-side fixes already applied on `Windows11-TestDNC` (committed to `perf/synctray-scan-and-transfer-speedups`):**

1. **`SyncEngine.cs` — Active upload session guard**: `ScanLocalDirectoryAsync` checks for existing `ActiveUploadSessionRecord` before queueing an upload, preventing duplicate uploads when a new sync pass starts while a large file is still uploading from the previous pass.

2. **`SyncEngine.cs` — Server-side existence check**: Before queueing a "genuinely new" file for upload, calls `ListChildrenAsync(parentFolderId)` for a fresh server check. If the file exists on the server, compares size+hash — if match, records locally without upload; if differs, queues as update with existing NodeId.

3. **`SyncEngine.cs` — Empty NodeId stale record cleanup**: When a tracked file has `NodeId == Guid.Empty` (from a previous CompleteUpload 409), queries the server via `ListChildrenAsync`. If file exists, updates the record. If not, removes stale record and re-queues for upload.

4. **`ChunkedTransferClient.cs` — Sequential chunk uploads**: `MaxConcurrency` reduced from 8 → 1. Concurrent chunk uploads competed for bandwidth and overwhelmed the server's rate limiter.

5. **`ChunkedTransferClient.cs` — Coordinated 10s rate-limit cooldown**: After any consumer gets a 429, all consumers wait 10 seconds before the next chunk attempt, preventing cascading rate-limit failures.

6. **`ChunkedTransferClient.cs` — Stronger retry backoff**: Changed from `2^(n-1)` (1s, 2s, 4s) to `3^(n-1)` (1s, 3s, 9s) with up to 1s jitter.

7. **`ChunkedTransferClient.cs` — 409 CompleteUpload lookup**: When CompleteUpload returns 409 (unique constraint violation), calls `ListChildrenAsync` to find the existing node's NodeId and ContentHash, returning them instead of `Guid.Empty`.

8. **`DotNetCloudApiClient.cs` — Fixed `ListChildrenAsync` URL**: Was calling `api/v1/files/{folderId}/children` (path param, 404) — corrected to `api/v1/files?parentId={folderId}` (query param) matching the actual server route.

**Client-side fixes NOT requiring server changes have all been tested: builds pass, 484 tests pass.**

---

### Server Actions — `cloud.kimball.home`

**Problem:** Chunk uploads to `cloud.dotnetcloud.net` return HTTP 429 even at sequential single-chunk rates. The global rate limiter (10,000 req/60s for authenticated users) should be more than enough for ~1 req/s, but 429s still occur.

**Investigation needed:**

- [ ] Check rate limiter partition keys — verify the SyncTray client's Bearer token contains a valid `sub` claim. If `sub` is missing, the rate limiter falls back to IP-based limiting (global: 100 req/60s = 1.67 req/s), which would explain 429s at ~1 chunk/s.
- [ ] Verify `module-upload-chunks` per-device policy (2,400/60s) is actually being applied. Previous archive entry shows this was fixed, but confirm the `[EnableRateLimiting("module-upload-chunks")]` attribute is on the Files controller's `UploadChunkAsync` endpoint and that the module host has `AddRateLimiter()` configured.
- [ ] Check production server logs: `sudo journalctl -u dotnetcloud | grep "429\|RateLimit\|rate.limit"` to identify which rate limiter policy is being hit.
- [ ] Verify the `retry-after` header in 429 responses to determine the rate limit window and remaining budget.
- [ ] If rate limiting is working as configured, consider exempting upload chunk endpoints from the global rate limiter or increasing the authenticated permit limit further.
- [ ] Check infrastructure (YARP proxy, load balancer) for additional connection limits or rate limiting.

**Verification:** After fix, deploy and restart Core.Server. Confirm a 1+ GB file upload completes without 429 errors from the SyncTray client.

---

### Client Actions — `Windows11-TestDNC`

- [ ] After server-side rate limit fix is deployed, rebuild SyncTray (`dotnet build src\Clients\DotNetCloud.Client.SyncTray\DotNetCloud.Client.SyncTray.csproj -c Release`)
- [ ] Run SyncTray and confirm large file upload completes without 429 errors
- [ ] Verify file appears correctly on the server (check via web UI or API)
- [ ] Verify local state DB has correct NodeId (no `Guid.Empty` entries)
- [ ] Check that subsequent sync passes show `0 queued` (no re-upload loops)
