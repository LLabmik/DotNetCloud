# Client/Server Mediation Handoff

Last updated: 2026-06-25 05:00 UTC (Windows11-TestDNC: gRPC auth still fails after server fix. Claims principal lacks "sub"/NameIdentifier in gRPC pipeline. 32 client tests fixed.)

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
- ❌ **Windows11-TestDNC re-test: gRPC auth STILL fails** — Despite the exact same `FindFirst("sub") ?? FindFirst(ClaimTypes.NameIdentifier)` pattern used by all controllers, `GetUserIdFromContext` returns `null` in the gRPC pipeline. The claims principal is missing both "sub" and `ClaimTypes.NameIdentifier` claims. Requires server-side investigation of OpenIddict validation claims in the `MapWhen` gRPC branch.
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

**Summary:** Windows11-TestDNC re-test complete. gRPC upload STILL fails ("Authentication required.") despite server-side fix — `GetUserIdFromContext` claims extraction not finding "sub" or `ClaimTypes.NameIdentifier` in gRPC-context `HttpContext.User`. HTTP fallback works correctly. Client tests fixed (32→0 failures, 263 passed, 1 skipped). Server agent needs to investigate why claims principal lacks "sub"/NameIdentifier in the gRPC pipeline.

---

### Server Actions — `cloud.kimball.home`

- ✓ Fix `GetUserIdFromContext` in `FilesUploadStreamService.cs` — replaced `ReadJwtToken()` with `httpContext.User.FindFirst("sub")`
- ✓ Remove temporary GRPC-DEBUG logging — done (4 log lines removed)
- ✓ Remove unused `using System.IdentityModel.Tokens.Jwt` — done
- ✓ Build & test — `dotnet build` succeeded (0 errors, 0 warnings), 575/575 server tests passed
- ✓ Deploy to cloud.dotnetcloud.net — `sudo ./scripts/deploy.sh` completed. All 14/14 modules healthy
- ✓ Hash verification — deployed DLL matches build output (`cd4aa0608ffe586350ba1d13223d59b6`)
- [ ] **Investigate gRPC claims principal**: Add debug logging to `GetUserIdFromContext` to enumerate all claims available on `httpContext.User` (claim types and values). The auth middleware authenticates the user (confirmed `IsAuthenticated=True` in prior GRPC-DEBUG), but neither `"sub"` nor `ClaimTypes.NameIdentifier` is found. Investigate whether OpenIddict validation via `MapWhen` gRPC branch produces different claim types than the controller pipeline.
  - Check: Is `UseLocalServer()` + `UseAspNetCore()` inside `MapWhen` producing the same `ClaimsPrincipal` as the non-gRPC pipeline?
  - Check: Does `IClaimsTransformation` (DotNetCloudClaimsTransformation) run inside the gRPC `MapWhen` branch's `UseAuthentication()`?
  - **Do NOT remove debug logging** — commit and deploy so Windows11-TestDNC can capture the output in a subsequent re-test.
- [ ] If root cause identified, fix `GetUserIdFromContext` and re-deploy.

### Client Actions — `Windows11-TestDNC`

- ✓ Pull latest
- ✓ Rebuild SyncTray
- ✓ Re-test gRPC upload — **FAILED**: `GetUserIdFromContext` still returns `null`, producing `"Authentication required."` error. Code pattern matches all controllers (`FindFirst("sub") ?? FindFirst(ClaimTypes.NameIdentifier)`), but neither claim exists on the principal in the gRPC branch. See investigation notes above for server-side leads.
- ✓ Verify files appear correctly on `cloud.dotnetcloud.net` — HTTP fallback works; test file `grpc-test-postfix-20260625-045356.txt` (93 bytes) uploaded successfully.
- ✓ **Fixed 32 failing client tests** (from 32→0 failures, 263 passed, 1 skipped):
  - **Root cause (28 sync tests):** Missing `GetActiveUploadSessionsAsync` mock setup in `TestInitialize` — Moq returned `null`, causing NRE on `.Select(s => s.LocalPath)` in `ScanLocalDirectoryAsync` at line 459, silently swallowed by sync engine's catch block.
  - **Fix:** Added `_stateDbMock.Setup(db => db.GetActiveUploadSessionsAsync(...)).ReturnsAsync(Array.Empty<ActiveUploadSessionRecord>())` to `SyncEngineTests.Initialize()`.
  - **Retry count mismatch (2 tests):** `UploadAsync_NetworkErrorExhaustsRetries_Throws` and `DownloadAsync_ChunkHashAlwaysMismatch_ThrowsChunkIntegrityException` expected `Times.Exactly(3)` but `ChunkUploadMaxRetries` and `ChunkDownloadMaxAttempts` were bumped to 6. Updated verifications to `Times.Exactly(6)`.
  - **API route change (1 test):** `ListChildrenAsync_NullFolder_CallsRootEndpoint` expected `root/children` but route changed to `api/v1/files`. Updated assertion.
  - **Linux-only test (1 test):** `FuseSyncFilesystem_ClassExists_OnLinux` fails on Windows. Added `if (!OperatingSystem.IsLinux()) return;` guard.
  - Run: `dotnet test tests/DotNetCloud.Client.Core.Tests/ -c Release` — result: 263 passed, 1 skipped, 0 failed.
