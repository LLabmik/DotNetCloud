# Client/Server Mediation Handoff

Last updated: 2026-06-29 (Music module auth handoff — Android client verified ✓)

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
- **Current active branch:** `feature/android-music-tab`
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

## Active Handoff

**Summary:** Add bearer token auth support to Music module (matching Chat/Files pattern)

**Background:** The Android Music tab client is fully implemented, but all API calls return 401. Root cause: `MusicControllerBase` uses `[Authorize(AuthenticationSchemes = "Identity.Application")]` which only accepts cookies. The Android app sends `Authorization: Bearer <token>` which the Music module doesn't recognize. Chat and Files modules use a policy scheme that auto-routes Bearer tokens to the Introspection handler — Music needs the same treatment.

Diagnostic logcat output confirming the gap:
```
CheckModuleEndpoint: status=200 body={"success":true,"data":{"installed":false}}
ProbeMusicApi: status=401 body=
CheckAvailableModulesAsync: isAvailable=True
MUSIC: GET https://cloud.kimball.home/api/v1/music/artists?skip=0&take=50
MUSIC: 401 for https://cloud.kimball.home/api/v1/music/artists?skip=0&take=50:
```

---

### Server Actions — `cloud.kimball.home` / `mint22`

Two files need changes, following the exact pattern already deployed in `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/`:

**File 1: `src/Modules/Music/DotNetCloud.Modules.Music.Host/Controllers/MusicControllerBase.cs`**

Change line:
```csharp
[Authorize(AuthenticationSchemes = "Identity.Application")]
```
to:
```csharp
[Authorize]
```

**File 2: `src/Modules/Music/DotNetCloud.Modules.Music.Host/Program.cs`**

Replace the current auth setup (cookie-only) with the policy scheme + introspection pattern matching Chat/Files. The existing Chat `Program.cs` at `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Program.cs` is the reference.

1. Add `using` for `DotNetCloud.Core.Auth` and `DotNetCloud.Core.Auth.Introspection`

2. Replace:
```csharp
builder.Services.AddAuthentication("Identity.Application")
    .AddCookie("Identity.Application", options =>
    {
        options.Cookie.Name = ".AspNetCore.Identity.Application";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.IsEssential = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(24);
        options.SlidingExpiration = true;
    });
```
With:
```csharp
builder.Services.AddTokenIntrospection();

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "DotNetCloud.Module";
    options.DefaultAuthenticateScheme = "DotNetCloud.Module";
    options.DefaultChallengeScheme = "DotNetCloud.Module";
})
.AddCookie("Identity.Application", options =>
{
    options.Cookie.Name = ".AspNetCore.Identity.Application";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.IsEssential = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(24);
    options.SlidingExpiration = true;
})
.AddIntrospection(IntrospectionAuthenticationExtensions.SchemeName)
.AddPolicyScheme("DotNetCloud.Module", "DotNetCloud.Module", options =>
{
    options.ForwardDefaultSelector = context =>
    {
        if (context.Request.Headers.TryGetValue("Authorization", out var auth)
            && auth.Count > 0
            && auth[0]?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
        {
            return IntrospectionAuthenticationExtensions.SchemeName;
        }
        return "Identity.Application";
    };
});
```

3. Replace `builder.Services.AddAuthorization();` with:
```csharp
builder.Services.AddAuthorization(options => AuthorizationPolicies.Configure(options));
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
```

4. Add project dependency: ensure `.csproj` has a reference to `DotNetCloud.Core.Auth` (same as Chat.Host.csproj).

5. **Build:** `dotnet build src/Modules/Music/DotNetCloud.Modules.Music.Host/`

6. **Deploy:** restart the music module process (e.g., `sudo systemctl restart dotnetcloud-module-music`)

- ✓ Apply auth changes to `MusicControllerBase.cs` and `Program.cs`
- ✓ Verify introspection is configured via the new programmatic introspection client
- ✓ Build succeeds with `dotnet build` (Release publish verified)
- ✓ Deploy to production and restart main dotnetcloud service
- ✓ All 14 modules healthy (including music: Healthy)
- ✓ Verify Android client can call music API with bearer token and get 200

**Note:** First deploy failed — `DotNetCloud.Core.Auth.dll` was copied but `dotnetcloud.music.deps.json` wasn't updated, causing `FileNotFoundException`. Fixed by also copying the updated `.deps.json` and `.runtimeconfig.json`. Ownership set to `dotnetcloud:dotnetcloud`.

**Server work complete — Android client verified successfully.**

---

### Android Client Actions — `monolith` (Completed ✓)

- ✓ **Built APK:** `dotnet build src/Clients/DotNetCloud.Client.Android/DotNetCloud.Client.Android.csproj -f net10.0-android` — succeeded
- ✓ **Deployed** signed APK to physical device (`R5CWC356B2K`, arm64-v8a)
- ✓ **Verified music API calls return 200** (no 401 errors) — logcat confirms:
  ```
  I/DotNetCloud: MUSIC: GET https://cloud.dotnetcloud.net/api/v1/music/artists?skip=0&take=50
  I/DotNetCloud: MUSIC: GET https://cloud.dotnetcloud.net/api/v1/music/artists/f044ce0d-.../albums
  I/DotNetCloud: MUSIC: GET https://cloud.dotnetcloud.net/api/v1/music/albums/f251e567-.../tracks
  ```
  No `MUSIC: 401` lines — all requests succeed with bearer token auth.
- ✓ **Music tab loads** — artists, albums, tracks display correctly
- ✓ **Playback** — track playback works end-to-end

**Result:** ✅ Music module bearer token auth is fully working. Server fix (policy scheme + introspection) resolves the 401 issue. Android client gets 200 on all music API endpoints.

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
- ✅ **SignalR channel group naming:** `chat-channel-{channelId}` (used by `ChatHub.ChannelGroup()`, `CoreHub.JoinGroupAsync()`, and Android `SignalRChatClient`).
- **Controller discovery:** Core.Server references Files.Host and Chat.Host via `ProjectReference`. ASP.NET Core auto-discovers controllers from referenced assemblies. Do NOT create duplicate controllers in Core.Server for routes already served by module Host assemblies.

**Fix applied in source (committed to branch):**
- `CoreHub.JoinGroupAsync()` now accepts both `"chat-channel-{guid}"` and bare GUID formats
- Extracts the GUID, then joins the connection to `"chat-channel-{guid}"` — matching `ChatHub.ChannelGroup()`
- `CoreHub.LeaveGroupAsync()` updated similarly for consistency
- `ChannelGroup()` helper method added to `CoreHub` matching the one in `ChatHub`

**Android client changes (already deployed in APK):**
- `ChatConnectionService` now starts correctly (was never started before)
- SignalR connection verified working via logcat ("SignalR connected successfully!")
- `SenderName` display confirmed working
- `JoinChannelGroupAsync` already sends the correct format ("chat-channel-{guid}")

### Server Actions — `cloud.kimball.home`

- ✓ Pull latest `feature/chat-auth-bearer-token-support`
- ✓ Fixed `channelId`→`groupKey` rename in log line (build error)
- ✓ Re-deployed `CoreHub.cs` group-name fix (previous deploy killed during stop phase — files weren't copied)
- ✓ Verified via deployed DLL strings: old "Channel ID cannot be empty." replaced by "Group name cannot empty."
- ✓ 14/14 modules healthy
