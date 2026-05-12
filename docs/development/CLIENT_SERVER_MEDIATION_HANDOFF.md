# Client/Server Mediation Handoff

Last updated: 20260512 (VFS Phase 3 complete — Windows Cloud Filter API implemented on Windows11-TestDNC)

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

- All prior Phase 2, chat, pre-Linux sync remediation, SyncTray icon enhancement work is complete and archived.
- VFS Phase 1 (server-side prerequisites) complete on `mint22`. Range header support and `metadataOnly` tree endpoint deployed.
- VFS Phase 2 (core abstraction layer) complete on `Windows11-TestDNC`.
- VFS Phase 3 (Windows Cloud Filter API) complete on `Windows11-TestDNC`:
  - `CfApiTypes.cs` + `CfApiNative.cs` — P/Invoke wrappers for cfapi.dll (11 functions, 15+ structs/enums)
  - `CloudFilterSyncProvider` — full `IVirtualFileProvider` implementation with sync root registration, placeholder creation, on-demand hydration, pin/unpin, dehydrate
  - `CloudFilterCallbacks` — managed callback delegates pinned via `GCHandle` (FETCH_DATA, VALIDATE_DATA, FETCH_PLACEHOLDERS, CANCEL_FETCH_DATA, NOTIFY_*)
  - DI: `CloudFilterSyncProvider` registered on Windows; `NoOpVirtualFileProvider` fallback for other platforms
  - Build: 0 errors. Tests: 203/203 pass (Client.Core).

## Environment

| Role | Machine | Detail |
|---|---|---|
| Server | `mint22` | `https://mint22:5443/` |
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

**Status:** VFS Phase 5 ready for `Windows11-TestDNC` (2026-05-12)  
**Blocked by:** nothing — Phase 3 (Windows Cloud Filter API) complete  
**Blocks:** nothing (final client-side VFS step for SyncTray)

### Task: Implement Phase 5 — SyncTray UI Integration

All specs and code templates are in `docs/VIRTUAL_FILE_SYNCING_PLAN.md` — read the Phase 5 section.

**What to implement (3 steps):**

1. **Step 5.1** — Add "Storage Mode" setting to SettingsViewModel:
   - `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/SettingsViewModel.cs`
   - `src/Clients/DotNetCloud.Client.SyncTray/Views/SettingsWindow.axaml`
   - Add `StorageMode` and `MaxCacheSizeMb` properties with persistence
   - Radio buttons: "Download all files" / "Files on-demand"
   - Confirmation dialogs for mode switches

2. **Step 5.2** — Wire VFS lifecycle in App.axaml.cs:
   - `src/Clients/DotNetCloud.Client.SyncTray/App.axaml.cs`
   - Call `IVirtualFileProvider.InitializeAsync()` on startup when `FilesOnDemand`
   - Call `IVirtualFileProvider.ShutdownAsync()` on graceful shutdown

3. **Step 5.3** — VFS status in TrayViewModel:
   - `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/TrayViewModel.cs`
   - Add `CloudOnlyFileCount`, `HydratedFileCount`, `CacheSizeBytes`, `IsHydrating` properties
   - Show hydration progress indicator in tray UI

**Prerequisites (already in place):**
- `IVirtualFileProvider` interface (Phase 2)
- `CloudFilterSyncProvider` on Windows (Phase 3)
- `VirtualFileSettings` + `VirtualFileSyncEngine` (Phase 2)
- Build: 0 errors. Tests: 203/203 pass.

**Pre-commit checklist:**
- Run `dotnet build` — must succeed with 0 errors
- Run `dotnet test` — all tests must pass
- Delete any unexpected untracked files before committing

**Post-completion:**
- Update `docs/VIRTUAL_FILE_SYNCING_PLAN.md` — mark Phase 5 deliverables ✓
- Update `docs/IMPLEMENTATION_CHECKLIST.md` — mark Phase 5 checkboxes ✓
- Update `docs/MASTER_PROJECT_PLAN.md` — update VFS Phase 5 status + deliverables
 