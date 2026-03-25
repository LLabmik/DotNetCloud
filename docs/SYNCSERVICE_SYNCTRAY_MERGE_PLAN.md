# SyncService → SyncTray Merge Plan

**Created:** 2026-03-25
**Status:** Planning
**Goal:** Eliminate the separate `DotNetCloud.Client.SyncService` background process by moving all sync functionality into `DotNetCloud.Client.SyncTray`, making the tray app the single owner of the sync lifecycle.

---

## Problem Statement

The current two-process architecture (SyncTray + SyncService) creates lifecycle management headaches:
- SyncTray exit does **not** stop SyncService → orphaned background process keeps hitting the server
- IPC adds complexity, latency, and failure modes (connection drops, reconnect logic, serialization round-trips)
- On Linux, tray app tries to start the service via `Process.Start` — fragile
- On Windows, the service runs as a Windows Service — lifecycle is decoupled from user session

**Decision:** Merge SyncService into SyncTray. One process. No IPC.

---

## Current Architecture

```
┌──────────────────────┐          IPC (Named Pipe / Unix Socket)          ┌──────────────────────────┐
│   SyncTray (Avalonia) │  ──────────────────────────────────────────────> │   SyncService (Worker)    │
│                      │                                                   │                          │
│  ViewModels          │   Commands: list-contexts, sync-now, pause...    │  SyncWorker (Hosted)      │
│   └─ IIpcClient ─────│──────────────────────────────────────────────────>│   └─ IpcServer            │
│                      │   Events: SyncProgress, SyncComplete, Error...   │   └─ IpcClientHandler     │
│                      │ <────────────────────────────────────────────────│   └─ SyncContextManager   │
│  DesktopStartupMgr   │                                                   │       └─ ISyncEngine(s)   │
│   └─ Process.Start() │                                                   │                          │
└──────────────────────┘                                                   └──────────────────────────┘
```

**Files involved:**
- SyncService: 13 files in 3 subdirectories (Program.cs, SyncWorker.cs, SyncServiceExtensions.cs, Ipc/*, ContextManager/*)
- SyncTray IPC: IpcClient.cs, IIpcClient.cs (2 files)
- SyncTray startup: DesktopStartupManager.cs (TryEnsureSyncServiceStarted)

---

## Target Architecture

```
┌───────────────────────────────────────────────────────────┐
│   SyncTray (Avalonia) — SINGLE PROCESS                     │
│                                                             │
│  ViewModels                                                 │
│   └─ ISyncContextManager ──────> SyncContextManager         │
│                                    └─ ISyncEngine(s)        │
│                                                             │
│  Lifecycle:                                                 │
│   OnStartup  → LoadContextsAsync()                         │
│   OnExit     → StopAllAsync()                              │
│                                                             │
│  NO IPC. NO separate process. NO orphaned services.        │
└───────────────────────────────────────────────────────────┘
```

---

## Inventory Summary

### What Moves Where

| Component | Current Location | Action |
|---|---|---|
| `ISyncContextManager` | SyncService/ContextManager/ | Move to Client.Core |
| `SyncContextManager` | SyncService/ContextManager/ | Move to Client.Core |
| `SyncContextRegistration` | SyncService/ContextManager/ | Move to Client.Core |
| `SyncEventArgs` | SyncService/ContextManager/ | Move to Client.Core |
| `AddAccountRequest` | SyncService/ContextManager/ | Move to Client.Core |
| `SyncServiceExtensions.cs` | SyncService/ | Refactor → Client.Core (without IPC/Worker registrations) |
| `SyncWorker.cs` | SyncService/ | **Delete** — SyncTray manages lifecycle directly |
| `IpcServer.cs` | SyncService/Ipc/ | **Delete** |
| `IpcClientHandler.cs` | SyncService/Ipc/ | **Delete** |
| `IIpcServer.cs` | SyncService/Ipc/ | **Delete** |
| `IpcCallerIdentity.cs` | SyncService/Ipc/ | **Delete** |
| `IpcProtocol.cs` | SyncService/Ipc/ | **Delete** |
| `Program.cs` | SyncService/ | **Delete** |
| `IpcClient.cs` | SyncTray/Ipc/ | **Delete** |
| `IIpcClient.cs` | SyncTray/Ipc/ | **Delete** |
| `DesktopStartupManager` | SyncTray/Startup/ | Simplify — remove TryEnsureSyncServiceStarted |

### What Stays
| Component | Location | Notes |
|---|---|---|
| Client.Core | src/Clients/DotNetCloud.Client.Core/ | Unchanged — already has sync engines, state DB, HTTP clients |
| All ViewModels | SyncTray/ViewModels/ | Rewired from IIpcClient → ISyncContextManager |
| App.axaml.cs | SyncTray/ | Updated DI + lifecycle |

### Package Reference Changes

SyncTray.csproj needs:
- **Remove:** ProjectReference to DotNetCloud.Client.SyncService (project will be deleted/emptied)
- **Keep:** ProjectReference to DotNetCloud.Client.Core
- **Add (optional):** `Microsoft.Extensions.Hosting.WindowsServices` + `Microsoft.Extensions.Hosting.Systemd` — only if we want SyncTray itself to register as a service. **Likely NOT needed** since SyncTray is a desktop GUI app, not a background service.

---

## IPC → Direct Call Mapping

ViewModels currently call `IIpcClient` methods that go over IPC to `SyncContextManager`. After the merge, they call `ISyncContextManager` directly.

### Method Mapping

| IIpcClient Method | ISyncContextManager Equivalent | Notes |
|---|---|---|
| `ListContextsAsync()` | `GetContextsAsync()` | Return type change: `ContextInfo` → `SyncContextRegistration` |
| `AddAccountAsync(AddAccountData)` | `AddContextAsync(AddAccountRequest)` | DTO type change |
| `RemoveAccountAsync(Guid)` | `RemoveContextAsync(Guid)` | Same signature |
| `SyncNowAsync(Guid)` | `SyncNowAsync(Guid)` | Same |
| `PauseAsync(Guid)` | `PauseAsync(Guid)` | Same |
| `ResumeAsync(Guid)` | `ResumeAsync(Guid)` | Same |
| `ListConflictsAsync(Guid, bool)` | `ListConflictsAsync(Guid, bool)` | Return type change: `ConflictRecordData` → `ConflictRecord` |
| `ResolveConflictAsync(Guid, int, string)` | `ResolveConflictAsync(Guid, int, string)` | Same |
| `UpdateBandwidthAsync(decimal, decimal)` | `UpdateBandwidthAsync(decimal, decimal)` | Same |
| `UpdateConflictSettingsAsync(...)` | `PersistConflictResolutionSettingsAsync(...)` | Param shape change |
| `GetFolderTreeAsync(Guid)` | `GetFolderTreeAsync(Guid)` | Same |
| `UpdateSelectiveSyncAsync(Guid, rules)` | `UpdateSelectiveSyncAsync(Guid, rules)` | Same |

### Event Mapping

| IIpcClient Event | ISyncContextManager Event | Notes |
|---|---|---|
| `SyncProgressReceived` | `SyncProgress` | EventArgs type change |
| `SyncCompleteReceived` | `SyncComplete` | EventArgs type change |
| `SyncErrorReceived` | `SyncError` | EventArgs type change |
| `ConflictDetected` | `ConflictDetected` | EventArgs type change |
| `ConflictAutoResolved` | `ConflictAutoResolved` | EventArgs type change |
| `TransferProgressReceived` | `TransferProgress` | EventArgs type change |
| `TransferCompleteReceived` | `TransferComplete` | EventArgs type change |

### IPC-Only Concerns (Removed)

| IIpcClient Feature | Disposition |
|---|---|
| `ConnectAsync()` | **Delete** — no connection needed |
| `IsConnected` property | **Delete** — always "connected" (in-process) |
| `ConnectionStateChanged` event | **Delete** — no disconnections possible |
| Subscribe/Unsubscribe commands | **Delete** — direct event subscription |
| IPC reconnect logic | **Delete** |
| JSON serialization/deserialization | **Delete** |
| IPC throttling (2 events/sec) | Review — may want to throttle UI updates instead |

---

## Implementation Steps

### Phase 1: Move Core Types to Client.Core

**Scope:** Move ContextManager types from SyncService to Client.Core so they're accessible without referencing SyncService.

1. Move `SyncService/ContextManager/ISyncContextManager.cs` → `Client.Core/Sync/ISyncContextManager.cs`
2. Move `SyncService/ContextManager/SyncContextManager.cs` → `Client.Core/Sync/SyncContextManager.cs`
3. Move `SyncService/ContextManager/SyncContextRegistration.cs` → `Client.Core/Sync/SyncContextRegistration.cs`
4. Move `SyncService/ContextManager/SyncEventArgs.cs` → `Client.Core/Sync/SyncEventArgs.cs`
5. Move `SyncService/ContextManager/AddAccountRequest.cs` → `Client.Core/Sync/AddAccountRequest.cs`
6. Update namespaces from `DotNetCloud.Client.SyncService.ContextManager` → `DotNetCloud.Client.Core.Sync`
7. Create new `Client.Core/Extensions/SyncServiceCollectionExtensions.cs` with only the DI registrations needed (HttpClient, SyncContextManager — **no** IpcServer, **no** SyncWorker)
8. Verify Client.Core builds

### Phase 2: Rewire SyncTray ViewModels

**Scope:** Replace all `IIpcClient` usage with direct `ISyncContextManager` calls.

1. **Create adapter interface** (optional): If the type differences between IPC DTOs and ContextManager types are significant, create a thin `ISyncService` interface in Client.Core that ViewModels use. Otherwise, use `ISyncContextManager` directly.
2. **TrayViewModel**: Replace `IIpcClient` with `ISyncContextManager`. Update event subscriptions from IPC events to SyncContextManager events. Map EventArgs types.
3. **SettingsViewModel**: Replace all IPC calls with ISyncContextManager calls. Map `AddAccountData` → `AddAccountRequest`, `ConflictRecordData` → `ConflictRecord`, etc.
4. **ConflictViewModel**: Replace `IIpcClient.ResolveConflictAsync` with `ISyncContextManager.ResolveConflictAsync`.
5. **FolderBrowserViewModel**: Replace `IIpcClient.GetFolderTreeAsync`/`UpdateSelectiveSyncAsync` with ISyncContextManager equivalents.
6. Remove all references to `IIpcClient`, `IpcClient`, connection state handling.

### Phase 3: Update SyncTray Lifecycle

**Scope:** SyncTray now owns sync startup and shutdown.

1. **App.axaml.cs DI**: 
   - Call new `AddSyncContextManager()` extension (from Phase 1) instead of registering `IIpcClient`
   - Remove `IIpcClient` / `IpcClient` singleton registration
2. **App.axaml.cs Startup** (`OnFrameworkInitializationCompleted`):
   - Remove `DesktopStartupManager.TryEnsureSyncServiceStarted()`
   - Remove `_ipcClient.ConnectAsync()`
   - Add: `_syncContextManager.LoadContextsAsync(token)` — starts all persisted sync contexts
3. **App.axaml.cs OnExit**:
   - Remove `_ipcClient` disposal
   - Add: `_syncContextManager.StopAllAsync(token)` — gracefully stops all sync engines
4. **DesktopStartupManager**: Remove `TryEnsureSyncServiceStarted()` method entirely (or gut it to no-op)

### Phase 4: Delete IPC Layer

**Scope:** Remove all IPC code from both projects.

1. Delete `src/Clients/DotNetCloud.Client.SyncService/Ipc/` directory (5 files):
   - IpcServer.cs
   - IpcClientHandler.cs
   - IIpcServer.cs
   - IpcCallerIdentity.cs
   - IpcProtocol.cs
2. Delete `src/Clients/DotNetCloud.Client.SyncTray/Ipc/` directory (2 files):
   - IpcClient.cs
   - IIpcClient.cs
3. Delete `src/Clients/DotNetCloud.Client.SyncService/SyncWorker.cs`
4. Delete `src/Clients/DotNetCloud.Client.SyncService/SyncServiceExtensions.cs` (replaced by Client.Core version)
5. Delete `src/Clients/DotNetCloud.Client.SyncService/Program.cs`

### Phase 5: Clean Up SyncService Project

**Scope:** Decide fate of the SyncService project.

**Option A — Delete entirely:**
- Remove from `DotNetCloud.sln`
- Remove from `DotNetCloud.CI.slnf`
- Remove SyncTray's ProjectReference to SyncService
- Delete `src/Clients/DotNetCloud.Client.SyncService/` directory

**Option B — Keep as empty shell:**
- Keep project for potential future use (e.g., headless sync daemon)
- Remove all files except csproj
- Not recommended — adds maintenance burden for no value

**Recommended:** Option A. A headless sync daemon can be re-created later by referencing Client.Core directly.

### Phase 6: Update Tests

**Scope:** Fix test projects that reference changed types/namespaces.

1. `tests/DotNetCloud.Client.SyncService.Tests/` — either delete or update to test types now in Client.Core
2. `tests/DotNetCloud.Client.SyncTray.Tests/` — update ViewModel test mocks from `IIpcClient` → `ISyncContextManager`
3. `tests/DotNetCloud.Client.Core.Tests/` — add tests for moved types if needed
4. Verify all test projects build and pass

### Phase 7: Build, Verify, Commit

1. `dotnet build` — ensure zero errors
2. `dotnet test` — ensure all tests pass
3. Update `docs/MASTER_PROJECT_PLAN.md` and `docs/IMPLEMENTATION_CHECKLIST.md` if applicable
4. Clean `git status` — no untracked garbage
5. Commit with descriptive message
6. Push

---

## Risk Assessment

| Risk | Mitigation |
|---|---|
| ViewModel type mismatches (IPC DTOs vs ContextManager types) | Map carefully in Phase 2. Consider creating shared DTOs or adapters if needed. |
| SyncContextManager thread safety in Avalonia UI context | SyncContextManager is already thread-safe (used from IPC handlers on thread pool). Avalonia dispatch for UI updates stays in ViewModels. |
| Event throttling lost (IPC throttled to 2/sec) | Add UI-level throttling in ViewModels if needed (timer-based coalescing). |
| Windows Service mode lost | Acceptable. Desktop sync tray is a user-session app. If headless daemon needed later, create a new minimal project referencing Client.Core. |
| Linux systemd integration lost | Same as above. Tray app is a user-session app. |
| Test coverage gaps | Phase 6 addresses this. SyncService.Tests either get moved or deleted. |

---

## Files Changed Summary

### New Files
- `src/Clients/DotNetCloud.Client.Core/Sync/ISyncContextManager.cs` (moved)
- `src/Clients/DotNetCloud.Client.Core/Sync/SyncContextManager.cs` (moved)
- `src/Clients/DotNetCloud.Client.Core/Sync/SyncContextRegistration.cs` (moved)
- `src/Clients/DotNetCloud.Client.Core/Sync/SyncEventArgs.cs` (moved)
- `src/Clients/DotNetCloud.Client.Core/Sync/AddAccountRequest.cs` (moved)
- `src/Clients/DotNetCloud.Client.Core/Extensions/SyncServiceCollectionExtensions.cs` (new, replaces SyncServiceExtensions.cs)

### Modified Files
- `src/Clients/DotNetCloud.Client.SyncTray/DotNetCloud.Client.SyncTray.csproj` — remove SyncService ProjectReference
- `src/Clients/DotNetCloud.Client.SyncTray/App.axaml.cs` — DI + lifecycle changes
- `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/TrayViewModel.cs` — ISyncContextManager
- `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/SettingsViewModel.cs` — ISyncContextManager
- `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/ConflictViewModel.cs` — ISyncContextManager
- `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/FolderBrowserViewModel.cs` — ISyncContextManager
- `src/Clients/DotNetCloud.Client.SyncTray/Startup/DesktopStartupManager.cs` — remove process launch
- `DotNetCloud.sln` — remove SyncService project
- `DotNetCloud.CI.slnf` — remove SyncService project
- Test projects as needed

### Deleted Files
- `src/Clients/DotNetCloud.Client.SyncService/` — entire directory (13 files)
- `src/Clients/DotNetCloud.Client.SyncTray/Ipc/IpcClient.cs`
- `src/Clients/DotNetCloud.Client.SyncTray/Ipc/IIpcClient.cs`

---

## Verification Checklist

After completing the merge:

- ☐ `dotnet build` succeeds with zero errors
- ☐ `dotnet test` — all passing tests still pass
- ☐ SyncTray launches and shows tray icon
- ☐ Adding an account works (OAuth2 flow → context registered)
- ☐ Sync starts automatically after account add
- ☐ Pause/Resume works from tray UI
- ☐ Closing SyncTray stops all sync engines (no orphaned processes)
- ☐ Selective sync folder browser loads from server
- ☐ Conflict resolution UI works
- ☐ No SyncService process found after SyncTray exit
- ☐ git status clean — no untracked files

---

## Also Fix (While Here)

### FileBrowser JSInterop Prerender Crash

**File:** `src/UI/DotNetCloud.UI.Web/Components/Files/FileBrowser.razor.cs`
**Bug:** `DisposeAsync()` calls `Js.InvokeVoidAsync` during static SSR prerendering → `InvalidOperationException`
**Fix:** Wrap the JS interop call in a try-catch for `InvalidOperationException`, or guard with a prerender check (`_jsAvailable` flag set during `OnAfterRenderAsync`).

This is independent of the merge and should be fixed in a separate commit.
