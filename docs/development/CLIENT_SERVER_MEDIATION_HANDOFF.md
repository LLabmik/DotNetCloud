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
- ❌ **Windows11-TestDNC verification: gRPC auth still fails** — Despite server fix, `"Authentication required."` persists. Client log confirms `tokenPresent=true` at the time of gRPC call. HTTP fallback works. The `Authorization` header is still not reaching `GetUserIdFromContext` via `GetHttpContext().Request.Headers`. Server-side debug logging added and deployed.
- ✅ **Round 2 server-side investigation complete** — Debug logging added to `FilesUploadStreamService.UploadFileStream` (logs all headers, auth header, ContentType, User identity). Middleware ordering verified correct. No middleware strips `Authorization` before gRPC branch. Deployed and hash-verified. All 14/14 modules healthy.
- ✅ **Windows11-TestDNC verification with GRPC-DEBUG logs complete** — Debug logs reveal:
  - `ContentType=application/grpc` ✅ — gRPC content type matches correctly
  - `Authorization=Bearer <JWE>` ✅ — Auth header IS present in HTTP/2 headers
  - `User.Identity.Name=Ben Kimball, IsAuthenticated=True` ✅ — Auth middleware IS authenticating the user successfully
  - **Root cause identified:** `GetUserIdFromContext` parses JWT with `JwtSecurityTokenHandler.ReadJwtToken()` and returns `jwt.Subject` — but the tokens are **JWE (encrypted)** tokens (`alg=RSA-OAEP, enc=A256CBC-HS512`). `ReadJwtToken()` cannot read inner claims from JWE, so `jwt.Subject` is always `null`.
  - **Fix:** Use `httpContext.User.FindFirst("sub")` instead — the `UseAuthentication()` middleware has already decrypted the JWE and populated claims. This matches the pattern used by every other controller in the codebase.
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

**Summary:** Root cause identified — `GetUserIdFromContext` fails on JWE-encrypted tokens. `JwtSecurityTokenHandler.ReadJwtToken()` cannot read inner claims from JWE (encrypted) tokens, so `jwt.Subject` is always `null`. The auth middleware (`UseAuthentication()`) has already decrypted the JWE and populated `httpContext.User` with claims — `GetUserIdFromContext` should use `httpContext.User.FindFirst("sub")` instead.

**Windows11-TestDNC verification complete:**
- ✓ Pulled latest (`169012b0`), rebuilt SyncTray — build succeeded
- ✓ Created test file `grpc-test-grpc-debug.txt`, ran sync — gRPC upload attempted
- ✓ Client log: `tokenPresent=true`, fallback to HTTP succeeded
- ✓ Server `GRPC-DEBUG` logs fetched via SSH (cloud.kimball.home):
  - `ContentType=application/grpc` — gRPC routing works
  - `Authorization=Bearer <JWE>` — auth header IS present in HTTP/2 headers
  - `User.Identity.Name=Ben Kimball, IsAuthenticated=True` — auth middleware works
- ✓ **Root cause identified:** See below

**Root cause: JWE token + ReadJwtToken incompatibility**

The `GetUserIdFromContext` method at `FilesUploadStreamService.cs:291` calls:
```csharp
var handler = new JwtSecurityTokenHandler();
if (handler.CanReadToken(token)) {
    var jwt = handler.ReadJwtToken(token);
    return jwt.Subject;  // ← Always null for JWE tokens!
}
```

The tokens issued by the server are **JWE (JSON Web Encryption)** tokens, not JWS (signed) tokens. The token header shows:
```
{"alg":"RSA-OAEP","enc":"A256CBC-HS512","kid":"7BFXo9zhz7DSmH3KeWfWyW-zLTT5C_J7GnsTS7K7wNY","typ":"at+jwt","cty":"JWT"}
```

`JwtSecurityTokenHandler.ReadJwtToken()` is designed for JWS (signed) tokens. When given a JWE token, it reads only the **outer encryption envelope** — the inner claims payload is encrypted and inaccessible. Therefore `jwt.Subject` is always `null`.

Meanwhile, `UseAuthentication()` middleware (OpenIddict validation) has **already decrypted the JWE** and populated `httpContext.User` with the full claims. This is confirmed by:
```
GRPC-DEBUG: User.Identity.Name=Ben Kimball, IsAuthenticated=True
```

The fix: replace the manual JWT parsing with the standard codebase pattern used by every controller:
```csharp
httpContext.User?.FindFirst("sub")?.Value
    ?? httpContext.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
```

---

### Server Actions — `cloud.kimball.home`

- [ ] Fix `GetUserIdFromContext` in `src/Core/DotNetCloud.Core.Server/Grpc/Services/FilesUploadStreamService.cs`:
  - Replace `var handler = new JwtSecurityTokenHandler(); ... return jwt.Subject;` with the standard claims-based pattern:
  ```csharp
  var httpContext = context.GetHttpContext();
  var subClaim = httpContext.User?.FindFirst("sub")?.Value
      ?? httpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
  if (subClaim is not null) return subClaim;
  ```
- [ ] Optionally remove the temporary GRPC-DEBUG logging after fix is verified
- [ ] Deploy to cloud.dotnetcloud.net (dotnet build, deploy.sh)
- [ ] Verify with grpcurl using a real JWT token, or wait for Windows11-TestDNC re-test

### Client Actions — `Windows11-TestDNC`

- ✓ Pulled latest (`169012b0`) — done
- ✓ Rebuilt SyncTray — done
- ✓ Tested gRPC upload — done (`tokenPresent=true`, server debug logs captured)
- ✓ Provided server-side GRPC-DEBUG log output — done
- ✓ Root cause identified and documented — done
- [ ] After server fix is deployed, re-test gRPC upload to confirm fix
