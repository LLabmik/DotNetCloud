# Client/Server Mediation Handoff

Last updated: 2026-06-25 05:45 UTC (Windows11-TestDNC: GRPC-AUTH-DEBUG complete. `sub` claim IS present (FindFirst works). Root cause: downstream Files module gRPC rejects unauthenticated internal calls.)

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
- ❌ **Windows11-TestDNC verification: gRPC auth still fails** — Despite server fix, `"Authentication required."` persists. Client log confirms `tokenPresent=true` at the time of gRPC call. HTTP fallback works. The `Authorization` header is still not reaching `GetUserIdFromContext` via `GetHttpContext().Request.Headers`. Server-side debug logging added and deployed.
- ✅ **Round 2 server-side investigation complete** — Debug logging added to `FilesUploadStreamService.UploadFileStream` (logs all headers, auth header, ContentType, User identity). Middleware ordering verified correct. No middleware strips `Authorization` before gRPC branch. Deployed and hash-verified. All 14/14 modules healthy.
- ✅ **Windows11-TestDNC verification with GRPC-DEBUG logs complete** — Debug logs reveal:
  - `ContentType=application/grpc` ✅ — gRPC content type matches correctly
  - `Authorization=Bearer <JWE>` ✅ — Auth header IS present in HTTP/2 headers
  - `User.Identity.Name=Ben Kimball, IsAuthenticated=True` ✅ — Auth middleware IS authenticating the user successfully
  - **Root cause identified:** `GetUserIdFromContext` parses JWT with `JwtSecurityTokenHandler.ReadJwtToken()` and returns `jwt.Subject` — but the tokens are **JWE (encrypted)** tokens (`alg=RSA-OAEP, enc=A256CBC-HS512`). `ReadJwtToken()` cannot read inner claims from JWE, so `jwt.Subject` is always `null`.
  - **Fix:** Use `httpContext.User.FindFirst("sub")` instead — the `UseAuthentication()` middleware has already decrypted the JWE and populated claims. This matches the pattern used by every other controller in the codebase.
- ✅ **Server-side GetUserIdFromContext fix deployed** — Replaced `JwtSecurityTokenHandler.ReadJwtToken()` with `httpContext.User.FindFirst("sub")`. Auth middleware already decrypts JWE and populates claims. GRPC-DEBUG logging removed. Deployed commit `1c1cf088`. All 14/14 modules healthy. Hash-verified.
- ✅ **Windows11-TestDNC GRPC-AUTH-DEBUG complete** — `sub` claim IS present (`sub=587d777a-4793-4248-2184-08deb47250fa`). `GetUserIdFromContext` works correctly. `NameIdentifier` absent but irrelevant.
- ❌ **Root cause: downstream module gRPC auth** — Core.Server creates gRPC channel to Files module host with `UnsafeUseInsecureChannelCallCredentials = true` (no credentials forwarded). The Files module's gRPC `InitiateUpload`/`UploadChunk`/`CompleteUpload` handlers reject unauthenticated calls. Error `"Authentication is required."` comes from Files module, not from `GetUserIdFromContext`. HTTP fallback works.
- ✅ **32 client tests fixed** — 263 passed, 1 skipped (Linux-only), 0 failed.
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

**Summary:** Windows11-TestDNC verification complete. `sub` claim IS present in gRPC pipeline (`sub=587d777a-4793-4248-2184-08deb47250fa`). `GetUserIdFromContext` works correctly. Root cause identified: downstream gRPC call from Core.Server → Files module host (`InitiateUpload`/`UploadChunk`) fails because `UnsafeUseInsecureChannelCallCredentials = true` — no credentials forwarded. Error message changed from `"Authentication required."` (old null check) to `"Authentication is required."` (downstream module rejection — `ResponseEnvelopeMiddleware.cs:340`). Server agent needs to fix downstream gRPC auth forwarding or make module gRPC handlers accept unauthenticated calls (userId already in request body).

---

### Server Actions — `cloud.kimball.home`

- ✓ Fix `GetUserIdFromContext` in `FilesUploadStreamService.cs` — replaced `ReadJwtToken()` with `httpContext.User.FindFirst("sub")`
- ✓ Remove temporary GRPC-DEBUG logging — done (4 log lines removed)
- ✓ Remove unused `using System.IdentityModel.Tokens.Jwt` — done
- ✓ Build & test — `dotnet build` succeeded (0 errors, 0 warnings), 575/575 server tests passed
- ✓ Deploy to cloud.dotnetcloud.net — `sudo ./scripts/deploy.sh` completed. All 14/14 modules healthy
- ✓ Hash verification — deployed DLL matches build output
- ✅ GRPC-AUTH-DEBUG confirms `sub` is present — `FindFirst("sub")` returns `587d777a-4793-4248-2184-08deb47250fa`. Claim is `sub` (not a different URI). `NameIdentifier` is absent but irrelevant. All prior hypotheses about missing claims are disproven.
- [ ] **Fix downstream gRPC auth for Files module host**: Core.Server creates gRPC channel to Files module with `UnsafeUseInsecureChannelCallCredentials = true` (no credentials). The Files module's gRPC `InitiateUpload`/`UploadChunk`/`CompleteUpload` handlers reject unauthenticated calls. Two options:
  - **Option A**: Forward the client's Bearer token on the downstream gRPC channel (via `CallCredentials` or set auth header on the `SocketsHttpHandler` used by the internal gRPC channel to the Files module). See `src/Core/DotNetCloud.Core.Server/Grpc/Services/FilesUploadStreamService.cs` line ~43-49.
  - **Option B**: Make the Files module host's gRPC `InitiateUpload`/`UploadChunk`/`CompleteUpload` handlers `[AllowAnonymous]` since `userId` is already in the request body. Check Files module's gRPC service class for `[Authorize]` attribute.
- [ ] Remove GRPC-AUTH-DEBUG logging from `GetUserIdFromContext` once downstream auth is fixed (4 `_logger.LogInformation` lines)
- [ ] Build & test: `dotnet build`
- [ ] Deploy: `sudo ./scripts/deploy.sh`

### Client Actions — `Windows11-TestDNC`

- ✓ Pull latest (`f93eddbd` — GRPC-AUTH-DEBUG logging)
- ✓ Rebuild SyncTray — `dotnet build src\Clients\DotNetCloud.Client.SyncTray.csproj -c Release` succeeded
- ✓ Re-test gRPC upload — **test file `Test.txt` triggered gRPC via SyncTray**. Local log confirms `tokenPresent=true`. Server GRPC-AUTH-DEBUG logs captured.
- ✓ **GRPC-AUTH-DEBUG findings**: `sub=587d777a-4793-4248-2184-08deb47250fa` ✅ — `FindFirst("sub")` works. `NameIdentifier=(not found)`. All 23 claims enumerated (name, email, client_id, scope, oi_scp entries, dnc:locale, dnc:tz).
- ✓ **Root cause identified**: Not a claims principal issue. The error changed from `"Authentication required."` (old) to `"Authentication is required."` (new — from `ResponseEnvelopeMiddleware.cs:340` 401 mapping). The `GetUserIdFromContext` returns the user ID successfully. The failure is in the downstream gRPC call from Core.Server → Files module host (`client.InitiateUploadAsync()` / `client.UploadChunkAsync()`) which has no auth credentials forwarded.
- ✓ HTTP fallback works — file uploaded successfully via REST API. Files verified on `cloud.dotnetcloud.net`.
- [ ] After server deploys fix, rebuild SyncTray and re-test gRPC upload. Trigger by creating a file in `C:\Users\benk\synctray`.
