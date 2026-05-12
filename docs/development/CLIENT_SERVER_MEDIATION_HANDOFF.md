# Client/Server Mediation Handoff

Last updated: 20260512 (VFS Phase 2 complete — core abstraction layer implemented on Windows11-TestDNC)

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

- All prior Phase 2, chat, pre-Linux sync remediation, SyncTray icon enhancement work is complete and archived.
- VFS Phase 1 (server-side prerequisites) complete on `mint22`. Range header support and `metadataOnly` tree endpoint deployed.
- VFS Phase 2 (core abstraction layer) complete on `Windows11-TestDNC`:
  - `IVirtualFileProvider` interface defined with 8 methods + XML doc comments
  - `HydrationState` enum + `LocalFileRecord.HydrationState` property + schema evolution
  - `VirtualFileSettings` class + `VirtualFileStorageMode` enum
  - `VirtualFileSyncEngine` wrapping `ISyncEngine` with mode switch logic
  - `NoOpVirtualFileProvider` stub + DI registration in `ClientCoreServiceExtensions`
  - Build: 0 errors. Tests: 203/203 pass (Client.Core), full suite passes.

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

**Status:** VFS Phase 3 ready for `Windows11-TestDNC` (2026-05-12)  
**Blocked by:** nothing — Phase 2 (core abstractions) complete  
**Blocks:** Phase 4 (Linux FUSE), Phase 5 (SyncTray UI)

### Task: Implement Phase 3 — Windows Cloud Filter API Provider

All specs and code templates are in `docs/VIRTUAL_FILE_SYNCING_PLAN.md` — read the Phase 3 section.

**What to implement (3 steps):**

1. **Step 3.1** — Create P/Invoke wrappers for Cloud Filter API (`cfapi.dll`):
   - `src/Clients/DotNetCloud.Client.Core/Platform/Windows/CfApi/CfApiNative.cs` — `[DllImport]` wrappers for essential functions
   - `src/Clients/DotNetCloud.Client.Core/Platform/Windows/CfApi/CfApiTypes.cs` — structs, enums, and constants
   - Key functions: `CfRegisterSyncRoot`, `CfCreatePlaceholders`, `CfExecute`, `CfSetPinState`, `CfUpdatePlaceholder`, `CfUnregisterSyncRoot`

2. **Step 3.2** — Create `CloudFilterSyncProvider : IVirtualFileProvider`:
   - `src/Clients/DotNetCloud.Client.Core/Platform/Windows/CloudFilterSyncProvider.cs`
   - Implements all 8 interface methods using Cloud Filter API
   - Registers sync root at `InitializeAsync`, creates placeholders via `CfCreatePlaceholders`, hydrates via `CfExecute` + chunk download, etc.

3. **Step 3.3** — Wire up in DI:
   - Replace `NoOpVirtualFileProvider` with `CloudFilterSyncProvider` in `ClientCoreServiceExtensions.cs` (Windows branch)
   - Update `VirtualFileSyncEngine` integration as needed

**Prerequisites (already in place):**
- `IVirtualFileProvider` interface (Phase 2)
- `HydrationState` enum + `LocalFileRecord.HydrationState` (Phase 2)
- `VirtualFileSettings` + `VirtualFileSyncEngine` (Phase 2)
- Server: Range header + `metadataOnly` tree endpoint (Phase 1, deployed on `mint22`)
- Build: 0 errors. Tests: full suite passes.

**Pre-commit checklist:**
- Run `dotnet build` — must succeed with 0 errors
- Run `dotnet test` — all tests must pass
- Delete any unexpected untracked files before committing

**Post-completion:**
- Update `docs/VIRTUAL_FILE_SYNCING_PLAN.md` — mark Phase 3 deliverables ✓
- Update `docs/IMPLEMENTATION_CHECKLIST.md` — mark Phase 3 checkboxes ✓
- Update `docs/MASTER_PROJECT_PLAN.md` — update VFS Phase 3 status + deliverables
 