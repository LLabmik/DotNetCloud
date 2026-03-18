# Client/Server Mediation Handoff

Last updated: 2026-03-18 (Chat auth enforcement — server-side COMPLETED on mint22; handoff to monolith for Android client cleanup)

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
- **Active cycle:** Chat auth enforcement — server-side COMPLETED on `mint22`. All chat endpoints now return 401 without bearer token. Handoff to `monolith` for Android client cleanup (remove `?userId=` query params from all chat API URLs).

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

### Chat Auth Client Cleanup — Android (for `monolith`)

**Target:** `monolith`
**Status:** READY FOR CLIENT AGENT
**Priority:** P1 — required for chat to work against auth-enforced server

#### Context

Server-side chat auth enforcement is deployed and verified on `mint22`. All chat endpoints now require a valid OpenIddict bearer token and return **401** without one. The server no longer reads `[FromQuery] Guid userId` — user identity comes exclusively from the bearer token's `sub`/`NameIdentifier` claim.

#### Required Android Client Changes

**1. Remove `?userId=` query params from all chat API URLs in `HttpChatRestClient`**

The server ignores these now — they're dead weight. Leaving them is harmless but creates confusion and sends unnecessary PII in URLs.

Search for all instances of `?userId=` or `&userId=` in the Android chat client code and remove them.

**2. Remove `AccessTokenUserIdExtractor` usage in chat client (if any)**

The chat client previously extracted `userId` from the access token to send as a query param. This is no longer needed — the server extracts identity from the bearer token directly. The `SetAuth(accessToken)` Bearer header is the only auth mechanism needed.

**3. Verify all chat operations still work against `mint22:15443`**

After removing `?userId=` params:
- List channels
- Send a message
- Load message history
- Create a channel
- Push notifications (register/unregister device)

All should work if `SetAuth(accessToken)` is correctly setting the Bearer header (which it already is).

#### Verification

1. Build Android app: `dotnet build` succeeds
2. Run chat-related tests: all pass
3. E2E smoke test against `mint22:15443`:
   - `GET /api/v1/chat/channels` with Bearer header → 200 (not 401)
   - `POST /api/v1/chat/channels` with Bearer header → 200/201
   - Without Bearer header → 401

#### Server-Side Reference (already deployed)

- `ChatControllerBase` uses `[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]`
- User identity read from `ClaimTypes.NameIdentifier` / `"sub"` claim
- No query params needed for user identity
- All 35+ endpoints enforce auth uniformly

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
