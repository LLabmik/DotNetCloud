# Client/Server Mediation Handoff

Last updated: 2026-06-24 20:47 UTC (Windows11-TestDNC: gRPC streaming still fails after server-side fix — `"Authentication required."` persists. Client verifies `tokenPresent=true`. Server-side `GetUserIdFromContext` fix needs review — header not reaching gRPC handler despite `context.GetHttpContext().Request.Headers` fix.)

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
- **Current active branch:** `perf/synctray-scan-and-transfer-speedups`
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

- ✅ **gRPC streaming upload deployed** — `FilesUploadStreamService` mapped, responds `"Authentication required."` (not UNIMPLEMENTED). All 14/14 modules healthy.
- ✅ **YARP 502 fix verified** — zero 502 errors during large file upload (previous session). Server deployed (19/19 Healthy).
- ✅ **429 fix** verified on `Windows11-TestDNC`.
- ✅ **Client resilience improved**: `ChunkUploadMaxRetries` 3→6, 502-specific backoff, 404-on-resume cleanup for stale sessions.
- ✅ **Server cleanup**: 62 orphaned upload sessions + 237 orphaned chunk blobs cleaned.
- ✅ **Windows11-TestDNC upload test complete** — All 5 test files (4 ODTs + 1.17GB PDF) synced and verified on `cloud.dotnetcloud.net`. gRPC attempted but falls back to HTTP; gRPC StatusCode=OK diagnostic captured.
- ✅ **Windows11-TestDNC gRPC diagnostics complete** — Rebuilt SyncTray with `HttpVersionPolicy.RequestVersionExact`, broad gRPC exception catch, and auth passed as HTTP default header. gRPC transport connects successfully (no more `RpcException`), but server's `GetUserIdFromContext` does not find the `Authorization` header. Files upload successfully via HTTP fallback. All 17 files in sync with zero errors.
- ✅ **Server-side gRPC investigation complete** — No YARP/nginx/proxy in front. gRPC routing through public `cloud.dotnetcloud.net:443` verified working. All 14 module host gRPC endpoints functional. Client `RpcException(StatusCode="OK")` was HTTP/2 negotiation issue — server side is clean.
- ✅ **Server-side auth fix deployed** — `GetUserIdFromContext` changed from `context.RequestHeaders` to `context.GetHttpContext().Request.Headers["Authorization"]`. `UseAuthentication()` + `UseAuthorization()` added to gRPC `MapWhen` pipeline. Deployed and hash-verified.
- ❌ **Windows11-TestDNC verification: gRPC auth still fails** — Despite server fix, `"Authentication required."` persists. Client log confirms `tokenPresent=true` at the time of gRPC call. HTTP fallback works. The `Authorization` header is still not reaching `GetUserIdFromContext` via `GetHttpContext().Request.Headers`. Server-side debug logging needed in `FilesUploadStreamService` to inspect headers at the handler.
- All prior Phase 2, chat, pre-Linux sync remediation, SyncTray icon enhancement, VFS work complete and archived.

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

**Summary:** Windows11-TestDNC verified server-side gRPC auth fix — still fails. Despite `GetUserIdFromContext` being changed to read from `context.GetHttpContext().Request.Headers["Authorization"]`, the server continues to return `"Authentication required."`. Client confirms `tokenPresent=true` at time of gRPC call. The `Authorization` header is not reaching the gRPC handler's `HttpContext` even after `UseAuthentication()` + `UseAuthorization()` were added to the pipeline. Server needs to add temporary debug logging in `FilesUploadStreamService` to inspect `GetHttpContext().Request.Headers` at the handler entry point.

**Windows11-TestDNC verification results:**
- ✅ Pulled latest from `perf/synctray-scan-and-transfer-speedups`
- ✅ `EnableGrpcStreaming` default changed to `true` in `ChunkedTransferClient` (redundant with `SyncContextManager` setting, but ensures all consumers use gRPC)
- ✅ SyncTray rebuilt and published to `C:\Users\benk\synctray-bin\`
- ✅ Test file created, sync detected, gRPC upload attempted
- ✅ `[20:46:54 INF] gRPC UploadFileStream: baseUrl=https://cloud.dotnetcloud.net/, tokenPresent=true` — auth token present on client
- ❌ `[20:46:54 WRN] gRPC upload failed for grpc-test-file.txt: Upload failed: Authentication required.` — server still rejects
- ✅ HTTP fallback works — file uploaded successfully via HTTP chunked upload
- ✅ All 18 files in sync (17 existing + 1 test file), test file cleaned up

**Key diagnostic evidence:**
```
[20:46:54 INF] gRPC UploadFileStream: baseUrl=https://cloud.dotnetcloud.net/, tokenPresent=true
[20:46:54 WRN] gRPC upload failed for grpc-test-file.txt: Upload failed: Authentication required.. Falling back to HTTP chunked upload.
System.InvalidOperationException: Upload failed: Authentication required.
   at DotNetCloud.Client.Core.Api.DotNetCloudApiClient.UploadFileStreamAsync(...)
   at DotNetCloud.Client.Core.Transfer.ChunkedTransferClient.UploadAsync(...)
```

The client's `InvalidOperationException` with message `"Authentication required."` originates from the server's `FilesUploadStreamService` — the gRPC handler itself returns this error because `GetUserIdFromContext` cannot find the `Authorization` header.

**Possible explanations for why the fix didn't work:**
1. The deployed binary may still be stale — the `MapWhen` pipeline change might not have been picked up
2. `GetHttpContext().Request.Headers["Authorization"]` might still be empty at the point `GetUserIdFromContext` runs (before middleware populates it)
3. The gRPC `MapWhen` branch might not be going through `UseAuthentication()` despite being added — check middleware ordering
4. The `FilesUploadStreamService` is in `Files.Host` module, not `Core.Server` — maybe the module's publish output was not deployed

---

### Server Actions — `cloud.kimball.home`

- ❌ **Server-side auth fix did NOT resolve the issue** — `"Authentication required."` persists
- [ ] **Add temporary debug logging in `FilesUploadStreamService`** at the handler entry point:
  ```csharp
  var httpContext = context.GetHttpContext();
  var logger = httpContext.RequestServices.GetRequiredService<ILogger<FilesUploadStreamService>>();
  logger.LogInformation("gRPC request headers: {Headers}", string.Join(", ", httpContext.Request.Headers.Select(h => $"{h.Key}={h.Value}")));
  logger.LogInformation("gRPC auth header: {AuthHeader}", httpContext.Request.Headers["Authorization"].FirstOrDefault() ?? "(null)");
  logger.LogInformation("gRPC user identity: {Identity}, IsAuthenticated: {IsAuth}", httpContext.User?.Identity?.Name ?? "(null)", httpContext.User?.Identity?.IsAuthenticated ?? false);
  ```
- [ ] **Verify middleware ordering** — ensure `UseAuthentication()` runs BEFORE the gRPC endpoint in the `MapWhen` pipeline
- [ ] **Verify deployment** — confirm `Files.Host` module DLL was actually replaced (not just `Core.Server`)
- [ ] **Test with grpcurl from server machine** using a known-good JWT:
  ```bash
  grpcurl -insecure -H "Authorization: Bearer <token>" localhost:50359 dotnetcloud.files.FilesService/UploadFileStream
  ```
- [ ] Once fixed, deploy and relay to `Windows11-TestDNC` for re-verification

### Client Actions — `Windows11-TestDNC`

- ✓ Server-side auth fix verification complete — gRPC still fails with `"Authentication required."`
- ✓ `EnableGrpcStreaming = true` default set in `ChunkedTransferClient`
- ✓ Findings documented — awaiting server-side debug logging deployment
