# Client/Server Mediation Handoff

Last updated: 2026-03-18 (Chat auth enforcement — handoff to mint22 for server-side ChatController auth hardening)

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
- P0 server-side sync hardening deployed and verified on `mint22`.
- Upload hardening story: CLOSED (2026-03-15). All machines verified.
- Deletion propagation story: **CLOSED** (2026-03-16). All three machines verified.
  - Linux client (`mint-dnc-client`): verified 2026-03-16 ~03:00Z
  - Windows client (`Windows11-TestDNC`): verified 2026-03-16 ~08:16Z. Bug fixed: `RemoveFileRecordsUnderPathAsync` path separator on Windows.
  - Server (`mint22`): confirmed stable 2026-03-16. Zero ERR entries, both nodes soft-deleted, no 5xx.
- Duplicate controller fix: CLOSED (2026-03-18). Deployed and verified on `mint22`. Files endpoint returns 401, service healthy.
- **Active cycle:** Chat auth enforcement — `ChatController` has zero `[Authorize]` and trusts client-supplied `?userId=` query params. Security vulnerability. Handoff to `mint22` for server-side fix.

## Environment

| Role | Machine | Detail |
|---|---|---|
| Server | `mint22` | `https://mint22:15443/` |
| Client | `Windows11-TestDNC` | Sync dir: `C:\Users\benk\Documents\synctray` |
| Client | `mint-dnc-client` | Linux Mint 22 validation host for desktop sync client implementation + E2E testing |
| Android Client | `monolith` | Android MAUI app development + emulator testing (Windows 11) |

## Key Carry-Forward Contracts

- Auth: OpenIddict bearer on files/sync endpoints via `FilesControllerBase` `[Authorize(AuthenticationSchemes = "OpenIddict.Validation.AspNetCore")]`.
- API envelope: middleware wraps responses; clients should unwrap via envelope helpers.
- Sync flow: changes -> tree -> reconcile -> chunk manifest -> chunk download -> file assembly.
- Desktop OAuth constant: `OAuthConstants.ClientId = "dotnetcloud-desktop"`.
- **SignalR channel group naming:** `chat-channel-{channelId}` (used by `ChatRealtimeService.ChannelGroup()` and Android `SignalRChatClient`).
- **Controller discovery:** Core.Server references Files.Host and Chat.Host via `ProjectReference`. ASP.NET Core auto-discovers controllers from referenced assemblies. Do NOT create duplicate controllers in Core.Server for routes already served by module Host assemblies.

## Active Handoff

### Chat Auth Enforcement — Server-Side (for `mint22`)

**Target:** `mint22`
**Status:** READY FOR SERVER AGENT
**Priority:** P0 — security vulnerability

#### Problem

`ChatController` (`src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Controllers/ChatController.cs`) has **zero authentication enforcement**. Every endpoint accepts an unauthenticated `[FromQuery] Guid userId` parameter that the client supplies — any caller can impersonate any user.

By contrast, `FilesControllerBase` correctly uses:
```csharp
[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
```
and extracts the caller identity from bearer token claims via `ClaimTypes.NameIdentifier` / `"sub"`.

#### Root Cause Analysis (done on monolith)

| | ChatController (BROKEN) | FilesControllerBase (CORRECT) |
|---|---|---|
| `[Authorize]` | ❌ None | ✅ `OpenIddictValidation` scheme |
| User identity | `[FromQuery] Guid userId` — client-supplied, unverified | Extracted from bearer token claims (`sub` / `NameIdentifier`) |
| Token validation | Never checked by middleware | Validated by OpenIddict middleware |
| Base class | `ControllerBase` | Custom `FilesControllerBase` |

Chat "works" from the Android app only because the server never checks auth — it trusts whatever `userId` the client passes in the URL.

#### Required Server-Side Changes

**1. Add OpenIddict package to Chat.Host** (`DotNetCloud.Modules.Chat.Host.csproj`):
```xml
<PackageReference Include="OpenIddict.Validation.AspNetCore" Version="7.2.0" />
```

**2. Create `ChatControllerBase`** (`src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Controllers/ChatControllerBase.cs`):
- Mirror `FilesControllerBase` pattern exactly
- `[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]`
- `GetAuthenticatedCaller()` method that reads `ClaimTypes.NameIdentifier` / `"sub"` from `User` claims and returns `CallerContext`
- `Envelope()` and `ErrorEnvelope()` helpers (already exist as private methods in ChatController — promote to base)

**3. Refactor `ChatController`** to:
- Inherit `ChatControllerBase` instead of `ControllerBase`
- Remove `[FromQuery] Guid userId` from ALL action parameters (35+ occurrences)
- Replace `ToCaller(userId)` calls with `GetAuthenticatedCaller()`
- Remove the private `ToCaller(Guid userId)`, `Envelope()`, and `ErrorEnvelope()` helper methods (now in base)
- The `using` for `DotNetCloud.Core.Capabilities` can likely be removed (was only needed for `CallerType` used in the old `ToCaller`)

**4. Update `ChatHostWebApplicationFactory`** (`tests/DotNetCloud.Integration.Tests/Infrastructure/ChatHostWebApplicationFactory.cs`):
- Add test auth handler for OpenIddict scheme (same pattern as `FilesHostWebApplicationFactory`)
- Register `TestAuthHandler` for `"OpenIddict.Validation.AspNetCore"` scheme
- Add `IStartupFilter` middleware that reads `x-test-user-id` header and sets `ClaimsPrincipal`
- Update `CreateApiClient()` → `CreateAuthenticatedApiClient(Guid authenticatedUserId)` to inject the header
- See `FilesHostWebApplicationFactory.cs` for exact implementation reference

**5. Update `ChatRestApiIntegrationTests`** (`tests/DotNetCloud.Integration.Tests/Api/ChatRestApiIntegrationTests.cs`):
- Use `_factory.CreateAuthenticatedApiClient(UserA)` instead of `_factory.CreateApiClient()`
- Remove `?userId=` query params from ALL test URLs
- Add a test verifying unauthenticated requests return `401`

**6. Update `ChatControllerTests`** (`tests/DotNetCloud.Modules.Chat.Tests/ChatControllerTests.cs`):
- Unit tests instantiate `ChatController` directly — they will need to set up `HttpContext` with a `ClaimsPrincipal` on the controller since `GetAuthenticatedCaller()` reads from `User`
- See how Files controller unit tests handle this (set `ControllerContext` with mock `ClaimsPrincipal`)

#### Files to Reference (correct patterns already in codebase)

- `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/FilesControllerBase.cs` — the gold standard
- `tests/DotNetCloud.Integration.Tests/Infrastructure/FilesHostWebApplicationFactory.cs` — test auth handler + `x-test-user-id` pattern
- `tests/DotNetCloud.Core.Server.Tests/Controllers/FilesControllerTests.cs` — unit test ClaimsPrincipal setup

#### Post-Fix: Android Client Update (monolith will handle)

Once the server enforces auth and stops reading `[FromQuery] userId`, the Android `HttpChatRestClient` needs to:
- Remove `?userId=` query params from all chat API URLs
- Keep `SetAuth(accessToken)` (Bearer header) — this is already correct
- The `AccessTokenUserIdExtractor` usage in chat client can be removed (server reads from claims now)

**monolith will do this client-side update after mint22 confirms the server changes are deployed and verified.**

#### Verification Steps (for mint22 after deployment)

1. `curl -k -s -o /dev/null -w "%{http_code}" https://localhost:15443/api/v1/chat/channels` → should return `401` (currently returns `200` with empty data)
2. `dotnet test` — all Chat unit and integration tests must pass
3. Health check still `Healthy`
4. Report back to monolith with commit hash for client-side follow-up

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
