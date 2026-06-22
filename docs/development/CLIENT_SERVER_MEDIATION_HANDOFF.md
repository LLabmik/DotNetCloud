# Client/Server Mediation Handoff

Last updated: 20260622 (Token introspection architecture — ready for deploy)

Purpose: shared handoff between client-side and server-side agents, mediated by user.

Archived context:

- Historical completed updates are in `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- Additional history remains available in git.
- VFS Phase 3 (Windows Cloud Filter API) completed on Windows11-TestDNC (2026-05-12).
- VFS Phase 2 (core abstraction layer) completed on Windows11-TestDNC (previously).

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

- All prior Phase 2, chat, pre-Linux sync remediation, SyncTray icon enhancement work is complete and archived.
- VFS Phase 1 (server-side prerequisites) complete on `cloud.kimball.home`.
- VFS Phase 2 (core abstraction layer) complete on `Windows11-TestDNC`.
- VFS Phase 3 (Windows Cloud Filter API) complete on `Windows11-TestDNC`.
- VFS Phase 4 (Linux FUSE) complete on `mint-dnc-client`:
  - `FuseSyncFilesystem : IVirtualFileProvider` with mount/unmount lifecycle
  - `DotNetCloudFuseOperations : IFuseOperations` with all FUSE callbacks
  - `LruCacheManager` wired into FUSE read path
  - DI registration: `FuseSyncFilesystem` on Linux
  - Build: 0 errors (CI solution filter). Tests: 253/254 Client.Core pass, 106/106 SyncTray pass.
- VFS Phase 5 (SyncTray UI Integration) complete on `Windows11-TestDNC` (archived).
- VFS Phase 6 (Testing & Validation) complete on `Windows11-TestDNC`:
  - 50+ unit tests across all VFS components
  - `LruCacheManager` class created + DI registered
  - Windows/Linux/E2E test scenarios documented
  - Build: 0 errors. Tests: Core 435, Client.Core 253/254, SyncTray 106.

## Environment

| Role           | Machine              | Detail                                                                             |
| -------------- | -------------------- | ---------------------------------------------------------------------------------- |
| Server         | `cloud.kimball.home` | `https://cloud.dotnetcloud.net/` (production)                                      |
| Server         | `mint22`             | `https://mint22:5443/` (dev)                                                       |
| Client         | `Windows11-TestDNC`  | Sync dir: `C:\Users\benk\Documents\synctray`                                       |
| Client         | `mint-dnc-client`    | Linux Mint 22 validation host for desktop sync client implementation + E2E testing |
| Client         | `mint-OptiPlex-7010` | This machine — production client connected to `cloud.dotnetcloud.net`              |
| Android Client | `monolith`           | Android MAUI app development + emulator testing (Windows 11)                       |

## Key Carry-Forward Contracts

- Auth: Files module host uses a policy scheme (`DotNetCloud.Module`) that auto-selects between `OpenIddict.Validation.AspNetCore` (JWT Bearer) and `Identity.Application` (cookie) based on the `Authorization` header. Controllers use plain `[Authorize]`. All module hosts must follow this pattern.
- API envelope: middleware wraps responses; clients should unwrap via envelope helpers.
- Sync flow: changes -> tree -> reconcile -> chunk manifest -> chunk download -> file assembly.
- Desktop OAuth constant: `OAuthConstants.ClientId = "dotnetcloud-desktop"`.
- **SignalR channel group naming:** `chat-channel-{channelId}` (used by `ChatRealtimeService.ChannelGroup()` and Android `SignalRChatClient`).
- **Controller discovery:** Core.Server references Files.Host and Chat.Host via `ProjectReference`. ASP.NET Core auto-discovers controllers from referenced assemblies. Do NOT create duplicate controllers in Core.Server for routes already served by module Host assemblies.

## Active Handoff

**Status:** � DEPLOY REQUIRED — Token introspection architecture (commit pending on `fix/files-module-bearer-auth`)

### What changed (architectural)

Replaced the broken JwtBearer local-key-validation on Files.Host with OAuth2-standard **token introspection**:

```
Client ──JWT──▶ Core.Server (YARP) ──JWT──▶ Files.Host
                      ▲                        │
                      │   gRPC introspection    │
                      └────────────────────────┘
```

**Before:** Files.Host loaded PEM signing keys and tried to validate JWT signatures locally. This never worked — 4 different JwtBearer configurations (kid matching, deterministic KeyId, IssuerSigningKeyResolver, ValidateIssuerSigningKey=false) all returned `invalid_token`.

**After:** Files.Host extracts the Bearer token, calls Core.Server's new `TokenIntrospection` gRPC service over the existing inter-module channel, gets back validated claims. No key sharing. No kid matching. No RSA concerns.

### New files (8)

- `src/Core/DotNetCloud.Core.Grpc/Protos/token_introspection.proto` — gRPC contract
- `src/Core/DotNetCloud.Core.Server/Grpc/Services/TokenIntrospectionServiceImpl.cs` — validates tokens via OpenIddict signing keys
- `src/Core/DotNetCloud.Core.Auth/Introspection/ITokenIntrospectionClient.cs` — interface
- `src/Core/DotNetCloud.Core.Auth/Introspection/TokenIntrospectionClient.cs` — gRPC client
- `src/Core/DotNetCloud.Core.Auth/Introspection/IntrospectionAuthenticationHandler.cs` — ASP.NET Core auth handler
- `src/Core/DotNetCloud.Core.Auth/Introspection/IntrospectionAuthenticationOptions.cs` — options (1-min cache, module ID, audience)
- `src/Core/DotNetCloud.Core.Auth/Introspection/IntrospectionAuthenticationExtensions.cs` — `.AddIntrospection()` extension
- `src/Core/DotNetCloud.Core.Auth/Introspection/IntrospectionServiceCollectionExtensions.cs` — DI registration
- `src/Core/DotNetCloud.Core.Auth/Introspection/IntrospectionResult.cs` — DTO

### Modified files (6)

- `src/Core/DotNetCloud.Core.Auth/DotNetCloud.Core.Auth.csproj` — added `Grpc.Net.Client` + `Grpc.Tools` packages
- `src/Core/DotNetCloud.Core.Grpc/DotNetCloud.Core.Grpc.csproj` — added proto
- `src/Core/DotNetCloud.Core.Server/Extensions/SupervisorServiceExtensions.cs` — register introspection gRPC service + client DI
- `src/Modules/Files/DotNetCloud.Modules.Files.Host/Program.cs` — **removed JwtBearer + key-loading**, replaced with introspection handler
- `src/Modules/Files/DotNetCloud.Modules.Files.Data/Services/AdminSharedFolderService.cs` — fallback to `IGroupDirectory` when `_coreDb` is null (fixes 2 tests)
- `src/Modules/Search/DotNetCloud.Modules.Search/Services/SqlServerSearchProvider.cs` — handle in-memory DB transactions (fixes 103 tests)
- `src/Clients/DotNetCloud.Client.SyncTray/Startup/DesktopStartupManager.cs` — injectable system desktop dir (fixes 1 test)
- `src/Modules/Music/DotNetCloud.Modules.Music.Host/Controllers/MusicController.cs` — unique temp filenames (fixes 1 flaky test)

### New tests (20)

- `tests/DotNetCloud.Core.Auth.Tests/Introspection/IntrospectionAuthenticationHandlerTests.cs` — **12 tests**: valid/invalid token, cache hit/miss, pass-through, challenge 401, forbidden 403, module ID forwarding, transport errors not cached
- `tests/DotNetCloud.Client.Core.Tests/Sync/SyncStreamListenerTests.cs` — **8 tests**: Bearer header, no-token, 401 triggers refresh, refresh fails disables SSE, SSE event parsing, non-sync events ignored, connection lifecycle

### Security hardening

- `TokenIntrospectionServiceImpl` rejects requests when gRPC auth interceptor didn't set ModuleId (defense in depth)
- Introspection handler caches results by SHA256(token), TTL = 1 minute
- Transport errors NOT cached (retried on next request)
- `WWW-Authenticate: Bearer error="invalid_token"` on challenge
- Audience validation: module host passes `required_audience`, service verifies JWT contains it

### Test results (all suites, zero failures)

| Suite                     | Count      |
| ------------------------- | ---------- |
| Files                     | 734/734 ✅ |
| Auth (incl. 12 new)       | 85/85 ✅   |
| Core.Server               | 575/575 ✅ |
| Search                    | 664/664 ✅ |
| Client.Core (incl. 8 new) | 264/264 ✅ |
| SyncTray                  | 106/106 ✅ |
| Music                     | 379/379 ✅ |

### Deploy (on `cloud.kimball.home`)

```bash
git checkout fix/files-module-bearer-auth
git pull
sudo ./scripts/deploy.sh --force
```

### Verify (on `cloud.kimball.home`)

- Check Core.Server log: `TokenIntrospectionService: loaded N signing key(s)`
- Check Files module log: `TokenIntrospectionClient: connected to Core.Server`
- Files API with valid Bearer: should return 200 (not 401)

### Then (on `mint-OptiPlex-7010`)

```bash
git pull
dotnet run --project src/Clients/DotNetCloud.Client.SyncTray/DotNetCloud.Client.SyncTray.csproj
```

Expected: `"SSE stream connected."`

### Known remaining work (future)

- SignalR push for cache invalidation (revoked tokens accepted for up to 1 min)
- Other module hosts (Music, Chat, etc.) should adopt introspection pattern
- Scope filtering per module's declared capabilities (currently returns all token scopes)

**Client version:** 0.3.9-alpha
**Server build:** 0.3.12
