# Client/Server Mediation Handoff

Last updated: 2026-06-27 17:15 UTC (Server-side investigation complete — fix deployed, infrastructure healthy, needs Android client verification)

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
- **Current active branch:** `feature/chat-auth-bearer-token-support`
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

- ✅ **Chat module HTTP 500 fix deployed** — `ExecuteAsync()` wrapping on `ListChannelsAsync` and 18 other endpoints, `UseDeveloperExceptionPage()` gated behind `IsDevelopment`. Binary at `/opt/dotnetcloud/server/modules/dotnetcloud.chat/dotnetcloud.chat.dll` confirmed fresh (15:59 UTC deploy) with `ListChannels` and `INTERNAL_ERROR` strings.
- ✅ **Server-side investigation complete** — Full investigation done on `cloud.kimball.home` (see Active Handoff for detailed findings). All infrastructure healthy. Cannot verify authenticated flow without a Bearer token.
- ✅ **Chat bearer token auth** — Deployed to production. Auth confirmed working (401 without token, 401 with invalid token).
- ✅ **Sync architecture** — All testing complete. See archive.
- ✅ **Linux client validation** — Completed on mint-OptiPlex-7010. Archived.

## Environment

| Role           | Machine              | Detail                                                                             |
| -------------- | -------------------- | ---------------------------------------------------------------------------------- |
| Server         | `cloud.kimball.home` | `https://cloud.dotnetcloud.net/` (production)                                      |
| Server         | `mint22`             | `https://mint22:5443/` (dev)                                                       |
| Client         | `Windows11-TestDNC`  | Sync dir: `C:\Users\benk\synctray`                                       |
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

**Summary:** Server-side fix deployed and infrastructure verified. Need Android client (`monolith`) to rebuild APK and test Chat tab with Bearer token auth.

**Background (2026-06-27, updated 17:15 UTC):** Server-side investigation completed on `cloud.kimball.home`. All findings documented below:

### Investigation Findings — `cloud.kimball.home`

**✅ Chat module process:** Healthy. Responding to gRPC health checks on port 50284. All 14 modules healthy.

**✅ Binary verification:**
- `stat /opt/dotnetcloud/server/modules/dotnetcloud.chat/dotnetcloud.chat.dll` → Modify: `2026-06-27 15:59:37` (deployed at 16:00)
- `strings -e l` confirms `ListChannels` and `INTERNAL_ERROR` strings present
- Module published to `/opt/dotnetcloud/server/modules/dotnetcloud.chat/` during deploy

**✅ YARP proxy:** Working correctly.
- `curl -sk https://cloud.dotnetcloud.net/api/v1/chat/channels` → HTTP 401 (correctly forwarded to Chat module)
- `curl -sk --http2-prior-knowledge http://localhost:50284/api/v1/chat/channels` → HTTP 401 (direct to Chat module)
- Both return `content-length: 0` (expected — `[Authorize]` rejects without body)
- No YARP ForwarderError entries in journal logs
- Forwarder configured with `Version = Version20, VersionPolicy = RequestVersionOrHigher` — h2c works on .NET 10

**✅ Database:** All healthy.
- Chat tables in `core` schema: `Channels`, `ChannelMembers`, `Messages`, etc.
- Public channel exists (Id: `DC03F432-...`, Name: `Public`, Type: `Public`)
- Both users are members of Public channel
- `ChannelService.EnsureDefaultPublicChannelForUserAsync()` would not make changes for existing users

**✅ gRPC introspection:** Working.
- `DOTNETCLOUD_CORE_ENDPOINT=http://localhost:50100` set on Chat module process
- `DOTNETCLOUD_MODULE_ID=dotnetcloud.chat` set correctly
- Core.Server grpc endpoint on port 50100 responds to introspection calls
- `AuthenticationInterceptor` validates `module-id` header → sets `UserState["ModuleId"]`
- `TokenIntrospectionServiceImpl` validates caller identity correctly

**✅ All unprotected endpoints fixed:** 18 endpoints in `ChatController` now wrapped in `ExecuteAsync()` or try-catch.

**❌ Could not verify authenticated flow:** Cannot get a Bearer token from `cloud.kimball.home` server (no `password` or `client_credentials` grant with known secret). Need Android client to test with its OAuth token.

---

### Android Client Actions — `monolith`

1. **Rebuild APK** on `monolith` (Windows 11) with latest server changes from `feature/chat-auth-bearer-token-support`

2. **Install on emulator and test Chat tab** — the primary test case:
   ```bash
   # After APK install, open Chat tab
   # Expected: Chat tab loads channel list successfully (HTTP 200 with JSON body)
   #   {"success":true,"data":[...channel list...]}
   # If still failing: capture full response body via logcat
   ```

3. **If still getting 500 with empty body** — run this curl from `monolith` or the Android emulator:
   ```bash
   # Make request with the same Bearer token the Android app uses
   curl -sk -w "\nHTTP_CODE:%{http_code}\nCONTENT_LENGTH:%{size_download}\n" \
     -H "Authorization: Bearer <android-token>" \
     https://cloud.dotnetcloud.net/api/v1/chat/channels
   ```
   Report the HTTP status code, content-length, and any response body.

4. **Expected behavior:** Sending messages, creating channels all work via Bearer token auth.
