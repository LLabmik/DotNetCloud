# Client/Server Mediation Handoff

Last updated: 2026-06-24 20:17 UTC (Windows11-TestDNC: gRPC streaming diagnosed — transport connects but auth header not received by server. Files sync via HTTP fallback. Handing off to cloud.kimball.home for server-side auth fix.)

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

**Summary:** Windows11-TestDNC gRPC diagnostics complete. Client-side HTTP/2 transport fixed (`HttpVersionPolicy.RequestVersionExact`). gRPC connects to server successfully but server rejects auth — `GetUserIdFromContext` iterates `context.RequestHeaders` but does not find the `Authorization` header, even though the client sends it (verified: `tokenPresent=true, headerCount=1` via CallOptions metadata, and also set as HTTP `DefaultRequestHeaders` on the gRPC HttpClient). Files sync successfully via HTTP fallback. Server needs to investigate why the `Authorization` header is not visible to the gRPC handler.

**Windows11-TestDNC investigation results:**
- ✅ `HttpVersionPolicy.RequestVersionExact` added to `GrpcChannelOptions` — forces HTTP/2 for gRPC
- ✅ Auth header sent two ways: (1) as gRPC `CallOptions` metadata, (2) as `HttpClient.DefaultRequestHeaders` via custom `SocketsHttpHandler`
- ✅ `GetGrpcHeaders()` removed from `CallOptions` (replaced by HTTP default headers)
- ✅ Broad `Exception` catch in `ChunkedTransferClient.UploadAsync` gRPC path — all gRPC failures now fall back to HTTP cleanly
- ✅ Debug logging added: `"gRPC UploadFileStream: baseUrl={BaseUrl}, tokenPresent={TokenPresent}"` at Info level
- ✅ SyncTray rebuilt and published to `C:\Users\benk\synctray-bin\`
- ✅ Full sync pass verified — all 17 files in sync, 0 changes detected on subsequent run
- ⚠️ `RpcException(StatusCode="OK", Detail="")` resolved? Original error no longer reproduces after `HttpVersionPolicy` fix
- ❌ **Server returns `"Authentication required."`** — gRPC transport succeeds (reaches server) but `GetUserIdFromContext()` in `FilesUploadStreamService.cs:275` cannot find the `Authorization` header in `context.RequestHeaders`

**Key diagnostic evidence from Windows11-TestDNC log:**
```
[20:14:36 INF] gRPC UploadFileStream: baseUrl=https://cloud.dotnetcloud.net/, tokenPresent=true
[20:14:36 WRN] gRPC upload failed for Test3.odt: Upload failed: Authentication required.
```
Token is present on the client side (including after being set as `HttpClient.DefaultRequestHeaders.Authorization`). The server's `GetUserIdFromContext` does not find it.

---

### Server Actions — `cloud.kimball.home`

- [ ] Investigate why `Authorization` header (sent as HTTP/2 default header by gRPC client) is not visible in `FilesUploadStreamService.GetUserIdFromContext(ServerCallContext context)`
- [ ] Verify `context.RequestHeaders` contents at runtime (add debug logging or use `grpcurl` with `-authority` flag)
- [ ] Test with `grpcurl -insecure -authority cloud.dotnetcloud.net -rpc-header "Authorization: Bearer <token>" ...` to confirm auth header reaches the handler
- [ ] Consider adding `app.UseAuthentication()` to the gRPC `MapWhen` pipeline (currently only has `UseRouting` + `UseEndpoints`) so the JWT is validated by the framework before reaching the service

### Client Actions — `Windows11-TestDNC`

- ✓ All client-side actions complete. SyncTray rebuilt with gRPC improvements. Files sync via HTTP fallback while server-side auth is investigated.
