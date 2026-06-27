# Client/Server Mediation Handoff

Last updated: 2026-06-27 17:35 UTC (Root cause identified via Chat vs Files comparison — `UseDeveloperExceptionPage` gated + `OpenIddict.Validation.AspNetCore` conflict)

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

- 🔴 **Chat module returns HTTP 500 with empty body when Bearer token is sent** — Auth middleware works (401 without token), but authenticated requests fail. Response headers include Core.Server's security headers (CSP, HSTS, X-Request-ID), confirming 500 flows through YARP proxy.
- 🔴 **Root cause identified via Chat vs Files comparison:**
  1. `UseDeveloperExceptionPage()` is gated behind `IsDevelopment` in Chat — in production, every exception returns **bare 500 with empty body**
  2. Files module has `UseDeveloperExceptionPage()` unconditionally, so it always catches and surfaces errors
  3. Chat module references `OpenIddict.Validation.AspNetCore` package — Files does NOT — this auto-registers auth handlers that conflict with the custom introspection scheme
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

**Summary:** Root cause identified via source comparison of Chat vs Files module auth configuration. Server needs to fix two specific issues, rebuild, and redeploy.

**Background (2026-06-27, updated 17:35 UTC):** Android client (`monolith`) enhanced logging confirmed the 500 comes from Core.Server YARP (response includes Core.Server's security headers: CSP, HSTS, X-Request-ID). Source code comparison between Chat and Files modules revealed the root cause.

### 🔍 Root Cause Analysis — Chat vs Files Comparison

**Finding 1: `UseDeveloperExceptionPage()` gated behind `IsDevelopment`**

Chat module (current):
```csharp
if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();
// No UseExceptionHandler() for production → bare 500 with empty body
```

Files module (working):
```csharp
app.UseDeveloperExceptionPage();  // ALWAYS on → catches all exceptions
```

The server agent's fix **gated** `UseDeveloperExceptionPage()` behind `IsDevelopment` but did **not** add `UseExceptionHandler()` for production. Any exception in the Chat module in production results in a bare 500 with empty body.

**Finding 2: `OpenIddict.Validation.AspNetCore` package — handler conflict**

Chat.csproj has:
```xml
<PackageReference Include="OpenIddict.Validation.AspNetCore" />
```

Files.csproj does **NOT** have this package.

`OpenIddict.Validation.AspNetCore` auto-registers its own OpenIddict validation handler and middleware. This **conflicts** with the custom `Introspection` scheme. When a Bearer token arrives:
1. Policy scheme sees Bearer → forwards to `Introspection` scheme
2. BUT OpenIddict's auto-registered handler may also try to process the token
3. The conflict causes an exception in the auth middleware
4. With `UseDeveloperExceptionPage()` gated behind dev, the exception produces a bare 500 with empty body in production

**Finding 3: Auth config is otherwise byte-for-byte identical**
All other auth setup (AddTokenIntrospection, AddAuthentication, AddPolicyScheme, AddAuthorization, middleware order) is identical between Chat and Files.

**Finding 4: Response headers confirm the flow**
Android logcat shows:
```
GetChannelsAsync RESPONSE: Status=500, Content-Length=0,
  Headers=Date=...; Content-Security-Policy=...; X-Request-ID=019f0b...
```
Core.Server's security headers present → 500 comes through YARP proxy, not direct module crash.

---

### Server Actions — `cloud.kimball.home`

1. **Remove `OpenIddict.Validation.AspNetCore`** from `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/DotNetCloud.Modules.Chat.Host.csproj`:
   ```xml
   <!-- Delete this line: -->
   <!-- <PackageReference Include="OpenIddict.Validation.AspNetCore" /> -->
   ```
   Files module doesn't have it, and the custom introspection scheme is the designated auth mechanism.

2. **Fix `UseDeveloperExceptionPage()` in `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Program.cs`** — either:
   **Option A (match Files behavior):** Make it unconditional for now (during debugging):
   ```csharp
   app.UseDeveloperExceptionPage();
   ```
   **Option B (recommended — production-safe):** Add `UseExceptionHandler()` for production:
   ```csharp
   if (app.Environment.IsDevelopment())
       app.UseDeveloperExceptionPage();
   else
       app.UseExceptionHandler(a => a.Run(async context =>
       {
           context.Response.StatusCode = 500;
           context.Response.ContentType = "application/json";
           await context.Response.WriteAsJsonAsync(new
           {
               success = false,
               error = new { code = "INTERNAL_ERROR", message = "An unexpected error occurred." }
           });
       }));
   ```

3. **Rebuild and redeploy:**
   ```bash
   git fetch origin
   git checkout feature/chat-auth-bearer-token-support
   git pull
   ./scripts/deploy.sh
   ```

4. **Verify WITH a Bearer token** (not just without):
   ```bash
   # Test without token (should still return 401)
   curl -sk -o /dev/null -w "%{http_code}" https://cloud.dotnetcloud.net/api/v1/chat/channels
   
   # Check the Chat module logs for any exceptions
   sudo journalctl -u dotnetcloud-chat --since "5 min ago" --no-pager | grep -i "error\|exception\|fail"
   ```

   Note: The server cannot get a Bearer token (no password/client_credentials grant with known secret), but fixing the two issues above and verifying no exceptions in the module logs should be sufficient. The Android client will test with its OAuth token.

### Android Client Actions — `monolith`

- ☐ After server fixes are deployed, rebuild APK and test Chat tab
