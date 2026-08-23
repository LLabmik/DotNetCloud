# SyncTray: Multiple Local Sync Folders + Folder Size Limit

> Implementation plan for the desktop SyncTray client (`DotNetCloud.Client.SyncTray` +
> shared `DotNetCloud.Client.Core`) and the Files module (`DotNetCloud.Modules.Files*`).

## Purpose

1. Let SyncTray users register **multiple local folders per account**, each mapped **1:1**
   to a chosen remote (server) folder. The server must **track** these registrations.
2. Reject overlapping local folders (a folder that is already tracked under another synced
   folder must not be addable, in **either direction**).
3. Remove the `.selective-sync.json` "mystery file" from the user's synced folder by moving
   selective-sync rules into the local SQLite `state.db`.
4. Add a **folder size limit**: per-folder recursive size threshold (default **250 MB**,
   adjustable in SyncTray settings). When enabled, skip folders that exceed it while syncing
   as many folders/subfolders as possible; ask the user **once per over-limit folder** and
   remember the choice.

## Decisions (final, do not revisit)

| Topic                                                | Decision                                                                                                                                |
| ---------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------- |
| Scope                                                | Multiple local folders per one account (lift the current single-account guard).                                                         |
| Remote mapping                                       | 1:1 — each local folder maps to one chosen remote folder (its `FileNode.Id`).                                                           |
| Server awareness                                     | Server tracks registrations (new table + REST endpoints).                                                                               |
| Overlap guard                                        | Reject nested/duplicate local folders in **both** directions.                                                                           |
| Selective sync storage                               | **SQLite `state.db`** (no `.selective-sync.json` anywhere).                                                                             |
| Size limit unit                                      | **Folder-based** — recursive total size; a folder is "over limit" when its total size exceeds the threshold.                            |
| Size limit granularity                               | Skip the **smallest** over-limit folder(s); include as many folders as possible.                                                        |
| Size limit UX                                        | **Ask once per over-limit folder and remember** the choice; skipped folders are **silent + logged**.                                    |
| Leaf folder over limit only because of one huge file | **Exclude the whole folder** (folder-based rule).                                                                                       |
| Size detection source                                | Scoped server tree for the download direction; lazily-cached local scan (refreshed on the full-scan interval) for the upload direction. |
| Size limit default                                   | 250 MB when enabled, user-adjustable in Settings.                                                                                       |

## Architecture

Reuse the existing "one `SyncContextRegistration` + one `SyncEngine` per local folder"
model. Multiple folders under one account become multiple contexts that share the same
`AccountKey` (`{serverUrl}:{userId}`) and token store, each with its own `DataDirectory`,
`state.db`, engine, and rules.

`ServerFolderId == null` means **"whole account"** — existing single-folder setups keep
working unchanged.

The server already supports `folderId` scoping on `tree`, legacy `changes`, and `reconcile`;
the client currently hardcodes `null` (root). This feature threads a real `folderId` through.

---

## Repository conventions (MUST follow)

- **File-scoped namespaces**, nullable reference types enabled, `TreatWarningsAsErrors`
  (enforced by `Directory.Build.props`).
- **XML doc comments required on all public members.**
- **Test naming:** `MethodName_Condition_ExpectedResult` (Arrange-Act-Assert).
- Interfaces prefixed `I`; server DTOs are `sealed record`s with `required`/`init`.
- **gRPC-only inter-module rule:** do NOT add `<ProjectReference>` from `Core.Server` to any
  module `.Host`, do NOT call `builder.Services.AddXxxServices()` in Core.Server, no
  cross-module DI. This feature is **client ↔ server REST** via the existing Core.Server YARP
  proxy (`api/v1/files` → `dotnetcloud.files`). **No new gRPC and no proxy-map change.**
- **Docs checkboxes:** use `✓` (done) / `☐` (pending) — never `[x]` / `[ ]`.
- After implementation, update `docs/IMPLEMENTATION_CHECKLIST.md` and
  `docs/MASTER_PROJECT_PLAN.md` using **targeted edits** (see Phase J).

---

## Phase A — Server: `SyncFolderRegistration` entity + persistence

### A1. New entity

**File:** `src/Modules/Files/DotNetCloud.Modules.Files/Models/SyncFolderRegistration.cs`

Model it on `src/Modules/Files/DotNetCloud.Modules.Files/Models/SyncDevice.cs`:

```csharp
public sealed class SyncFolderRegistration
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public Guid RemoteFolderNodeId { get; set; }   // FileNode.Id of the chosen remote folder
    public string RemoteFolderPath { get; set; } = string.Empty; // denormalized display path at registration time
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
```

Notes:

- `RemoteFolderPath` is a denormalized, human-readable snapshot (e.g. `/Documents/Work`),
  set by the server at registration from the folder's `MaterializedPath`/name chain.
- The server stores the **remote** registration only; the local path stays device-local in
  `contexts.json` (do NOT add `LocalPath` to this entity).

### A2. EF configuration

**File:** `src/Modules/Files/DotNetCloud.Modules.Files.Data/Configuration/SyncFolderRegistrationConfiguration.cs`

Follow `SyncDeviceConfiguration.cs`:

```csharp
public sealed class SyncFolderRegistrationConfiguration
    : IEntityTypeConfiguration<SyncFolderRegistration>
{
    public void Configure(EntityTypeBuilder<SyncFolderRegistration> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.RemoteFolderPath).HasMaxLength(4000);
        builder.Property(r => r.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(r => r.UpdatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(r => r.UserId).HasDatabaseName("ix_sync_folder_registrations_user_id");
        builder.HasIndex(r => new { r.UserId, r.RemoteFolderNodeId })
               .IsUnique()
               .HasDatabaseName("uq_sync_folder_registrations_user_folder");
    }
}
```

### A3. Register the entity

**File:** `src/Modules/Files/DotNetCloud.Modules.Files.Data/FilesDbContext.cs`

- Add: `public DbSet<SyncFolderRegistration> SyncFolderRegistrations => Set<SyncFolderRegistration>();`
- In `OnModelCreating`, add `modelBuilder.ApplyConfiguration(new SyncFolderRegistrationConfiguration());`

### A4. Migrations (both providers)

Design-time factories exist (`FilesDbContextDesignTimeFactory` for PostgreSQL,
`FilesDbContextSqlServerDesignTimeFactory` for SQL Server), so you can target the Data
project directly. Run from the repo root:

```bash
# PostgreSQL
dotnet ef migrations add SyncFolderRegistration \
  --project src/Modules/Files/DotNetCloud.Modules.Files.Data \
  --context FilesDbContext

# SQL Server
dotnet ef migrations add SyncFolderRegistration_SqlServer \
  --project src/Modules/Files/DotNetCloud.Modules.Files.Data \
  --context FilesDbContext \
  --output-dir Migrations/SqlServer
```

### A5. Tests (Phase A)

**File:** `tests/DotNetCloud.Modules.Files.Tests/` — add a new test class
`Configuration/SyncFolderRegistrationConfigurationTests.cs` (if one does not exist, create
under the same folder as the other EF config tests):

- `Configure_Entity_HasUniqueIndexOnUserIdAndRemoteFolderNodeId`
- `Configure_StringColumns_HaveMaxLength` (assert `RemoteFolderPath` max length)

Use the same in-memory/SQLite EF test harness pattern as the existing
`SyncDeviceConfigurationTests` (or equivalent) in that project.

---

## Phase B — Server: registration service + REST endpoints

### B1. Service interface

**File:** `src/Modules/Files/DotNetCloud.Modules.Files/Services/ISyncFolderRegistrationService.cs`

```csharp
public interface ISyncFolderRegistrationService
{
    Task<IReadOnlyList<SyncFolderRegistrationDto>> ListAsync(CallerContext caller, CancellationToken ct = default);
    Task<SyncFolderRegistrationDto> RegisterAsync(Guid remoteFolderNodeId, CallerContext caller, CancellationToken ct = default);
    Task UnregisterAsync(Guid remoteFolderNodeId, CallerContext caller, CancellationToken ct = default);
}
```

`CallerContext` lives at `src/Core/DotNetCloud.Core/Authorization/CallerContext.cs`.

### B2. Service implementation

**File:** `src/Modules/Files/DotNetCloud.Modules.Files.Data/Services/SyncFolderRegistrationService.cs`

Injection: `FilesDbContext` (scoped). Implement:

- `ListAsync` → query `SyncFolderRegistrations.Where(r => r.UserId == caller.UserId && r.IsActive)`
  ordered by `CreatedAt`; map to DTO.
- `RegisterAsync`:
  1. Load the target `FileNode` where `Id == remoteFolderNodeId`; throw
     `NotFoundException("FileNode", remoteFolderNodeId)` if missing.
  2. Validate `node.OwnerId == caller.UserId` (else `ForbiddenException`) and
     `node.NodeType == FileNodeType.Folder` (else `ValidationException`).
  3. **Remote overlap check:** load all existing registrations for the user, join to their
     `FileNode`s, and reject if the new folder is equal to, a descendant of, or an ancestor of
     an existing registered folder. Use `MaterializedPath`:
     - equal: `new.Id == existing.RemoteFolderNodeId`
     - descendant: `new.MaterializedPath.StartsWith(existingPath + "/")`
     - ancestor: `existing.MaterializedPath.StartsWith(newPath + "/")`
  4. Insert a new row (compute `RemoteFolderPath` from the node's materialized path or name
     chain). Handle the unique-index race with `DbUpdateException` +
     `DbExceptionClassifier.IsUniqueConstraintViolation` → re-fetch and return existing
     (copy the `SyncDeviceResolver` pattern in
     `src/Modules/Files/DotNetCloud.Modules.Files.Data/Services/SyncDeviceResolver.cs`).
- `UnregisterAsync` → find by `(UserId, RemoteFolderNodeId)`; throw `NotFoundException` if
  absent; set `IsActive = false` + `UpdatedAt` (or delete the row — choose delete for
  simplicity and note it).

### B3. DTO

**File:** `src/Modules/Files/DotNetCloud.Modules.Files/DTOs/SyncDtos.cs` (append)

```csharp
public sealed record SyncFolderRegistrationDto
{
    public Guid Id { get; init; }
    public Guid RemoteFolderNodeId { get; init; }
    public required string RemoteFolderPath { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed record SyncFolderRegistrationRequestDto
{
    public Guid RemoteFolderNodeId { get; init; }
}
```

### B4. Controller

**File:** `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/SyncFoldersController.cs`

Inherit `FilesControllerBase` (same file as `FilesController.cs`).
`[ApiController]`, `[Route("api/v1/files/sync/folders")]`, `[Authorize]`.

| Verb   | Route (relative)             | Action                                                       | Notes                                          |
| ------ | ---------------------------- | ------------------------------------------------------------ | ---------------------------------------------- |
| GET    | `` (empty)                   | `ListAsync()`                                                | returns `Ok(Envelope(list))`                   |
| POST   | `` (empty)                   | `RegisterAsync([FromBody] SyncFolderRegistrationRequestDto)` | returns `Created(...)` or `Ok(Envelope(dto))`  |
| DELETE | `/{remoteFolderNodeId:guid}` | `UnregisterAsync(Guid remoteFolderNodeId)`                   | returns `Ok(Envelope(new { deleted = true }))` |

Each action calls `GetAuthenticatedCaller()`. Wrap with `ExecuteAsync(...)` (the base class's
try/catch that maps `NotFoundException`→404, `ForbiddenException`→403, `ValidationException`→400).

### B5. DI registration

**File:** `src/Modules/Files/DotNetCloud.Modules.Files.Data/FilesServiceRegistration.cs`

In `AddFilesServices` (near `services.AddScoped<ISyncService, SyncService>();` at line 71; note it is also registered at line 184 in a second method),
add:

```csharp
services.AddScoped<ISyncFolderRegistrationService, SyncFolderRegistrationService>();
```

Do **not** add anything to `Core.Server/Program.cs` — the Files host calls
`builder.Services.AddFilesServices(builder.Configuration);` (its own `Program.cs`).

### B6. Tests (Phase B)

**File:** `tests/DotNetCloud.Modules.Files.Tests/Services/SyncFolderRegistrationServiceTests.cs`

Follow the existing `SyncServiceTests.cs` harness (in-memory/SQLite `FilesDbContext`):

- `RegisterAsync_ValidFolder_CreatesRegistration`
- `RegisterAsync_NodeNotFound_ThrowsNotFoundException`
- `RegisterAsync_NodeNotOwnedByCaller_ThrowsForbiddenException`
- `RegisterAsync_NodeIsFile_ThrowsValidationException`
- `RegisterAsync_FolderInsideExistingRegistration_Rejected` (descendant)
- `RegisterAsync_ExistingRegistrationInsideFolder_Rejected` (ancestor)
- `RegisterAsync_SameFolderTwice_ReturnsExisting` (idempotent, unique-index race handled)
- `UnregisterAsync_ExistingFolder_MarksInactive`
- `UnregisterAsync_NotRegistered_ThrowsNotFoundException`
- `ListAsync_ReturnsOnlyCallersActiveRegistrations`

**File:** `tests/DotNetCloud.Modules.Files.Tests/Controllers/SyncFoldersControllerTests.cs`
(if a controller test project/pattern exists; otherwise cover via the service tests above +
an integration test if the harness supports it):

- `List_Unauthenticated_Returns401`
- `Register_InvalidBody_Returns400`

---

## Phase C — Server: recursive change-feed folder scoping

### C1. Fix the folder filter

**File:** `src/Modules/Files/DotNetCloud.Modules.Files.Data/Services/SyncService.cs`

Both methods currently filter folder-scoped changes with
`n.ParentId == folderId.Value || n.Id == folderId.Value` — **one level only**. Replace with a
recursive descendant filter using `MaterializedPath` (mirror the logic already in
`ReconcileAsync`):

- In `GetChangesSinceAsync` (line 32) and `GetChangesSinceCursorAsync` (line 119): first
  load the scope folder's `MaterializedPath` once, then filter
  `n.Id == folderId || n.MaterializedPath.StartsWith(scopePath + "/")`.

Keep the deleted-query filter consistent.

### C2. Tests (Phase C)

**File:** `tests/DotNetCloud.Modules.Files.Tests/Services/SyncServiceTests.cs`

Extend the existing `GetChangesSinceAsync_FiltersByFolder` (line 95) and add:

- `GetChangesSinceAsync_WithNestedFolder_ReturnsDescendantChanges` — create
  `folder / child / grandchild`; scope by `folder`; assert a change in `grandchild` is returned
  (this currently fails with the one-level filter — it is the regression test).
- `GetChangesSinceCursorAsync_WithNestedFolder_ReturnsDescendantChanges` — same for the
  cursor overload.

---

## Phase D — Client: API client + model

### D1. Cursor overload gains `folderId`

**Files:**

- `src/Clients/DotNetCloud.Client.Core/Api/IDotNetCloudApiClient.cs` (cursor overload at line 88)
- `src/Clients/DotNetCloud.Client.Core/Api/DotNetCloudApiClient.cs` (cursor overload at line 468)

Change:

```csharp
// BEFORE
Task<PagedSyncChangesResponse> GetChangesSinceAsync(string? cursor, int limit = 500, CancellationToken cancellationToken = default);

// AFTER
Task<PagedSyncChangesResponse> GetChangesSinceAsync(string? cursor, int limit = 500, Guid? folderId = null, CancellationToken cancellationToken = default);
```

In the implementation, append `&folderId={folderId}` to the query when `folderId.HasValue`
(the server `SyncController.GetChangesAsync` already accepts `[FromQuery] Guid? folderId`).

### D2. Registration methods

**File:** `src/Clients/DotNetCloud.Client.Core/Api/IDotNetCloudApiClient.cs` + `DotNetCloudApiClient.cs`

Add (mirror existing `GetAsync`/`PostJsonAsync`/`DeleteAsync` helpers):

```csharp
Task<IReadOnlyList<SyncFolderRegistrationResponse>> ListSyncFoldersAsync(CancellationToken ct = default);
Task<SyncFolderRegistrationResponse?> RegisterSyncFolderAsync(Guid remoteFolderNodeId, CancellationToken ct = default);
Task DeleteSyncFolderAsync(Guid remoteFolderNodeId, CancellationToken ct = default);
```

Routes: `GET/POST api/v1/files/sync/folders`, `DELETE api/v1/files/sync/folders/{id}`.

Add the response model to `src/Clients/DotNetCloud.Client.Core/Api/ApiModels.cs`:

```csharp
public sealed class SyncFolderRegistrationResponse
{
    public Guid Id { get; set; }
    public Guid RemoteFolderNodeId { get; set; }
    public string RemoteFolderPath { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

### D3. Model gains `ServerFolderId`

**Files:**

- `src/Clients/DotNetCloud.Client.Core/Sync/SyncContext.cs`
- `src/Clients/DotNetCloud.Client.Core/Sync/SyncContextRegistration.cs`
- `src/Clients/DotNetCloud.Client.Core/Sync/AddAccountRequest.cs`

Add to each:

```csharp
public Guid? ServerFolderId { get; init; }            // null = whole account
public string? ServerFolderDisplayPath { get; init; } // e.g. "/Documents/Work"
```

Persist these on `SyncContextRegistration` via the existing JSON registry
(`contexts.json` — `LoadRegistrationsAsync`/`SaveRegistrationsAsync` are already generic
`System.Text.Json`; no other change needed, but confirm the serializer uses the source
generator/options that tolerate new optional properties).

### D4. Tests (Phase D)

**File:** `tests/DotNetCloud.Client.Core.Tests/Api/DotNetCloudApiClientTests.cs` (or the
existing API-client test file for this project):

- `GetChangesSince_WithFolderId_AppendsQueryParam`
- `GetChangesSince_WithoutFolderId_OmitsQueryParam`
- `RegisterSyncFolder_PostsToFoldersEndpoint`
- `DeleteSyncFolder_SendsDeleteToFolderEndpoint`

If `DotNetCloudApiClient` has no existing test file, add one using a fake
`HttpMessageHandler` (the class has helper methods `GetAsync`/`PostJsonAsync` that use a
base address).

---

## Phase E — Client: folder-scoped engine

**File:** `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs`

### E1. Pass `ServerFolderId` into remote calls

- Line 1139: `GetFolderTreeAsync(null, ct)` → `GetFolderTreeAsync(context.ServerFolderId, ct)`.
- Line 1147: `GetChangesSinceAsync(cursor, limit: 500, ct)` →
  `GetChangesSinceAsync(cursor, limit: 500, context.ServerFolderId, ct)`.

`context` here is the `SyncContext` captured at `StartAsync` (field `_activeContext`). Keep a
field `private Guid? _serverFolderId;` set in `StartAsync` from `context.ServerFolderId`.

### E2. Treat a scoped root as a path-less anchor

The full-tree root is a virtual node with `NodeId == Guid.Empty`; a scoped tree's root is the
**real folder node** (its `Name` would otherwise be wrongly prepended as a path segment).

Update these helpers to ignore the **first** node's name when it is a real node:

- `BuildPathMap` (2369)
- `BuildServerFileMap` (2387)
- `BuildFolderPathMap` (2404)
- `BuildServerFolderMap` (2508)
- `CollectAllServerNodeIds` (2421) — still include the scoped root id (it must be mappable),
  but do not add a path segment for it.

Suggested minimal change to `BuildPathMap` (apply the same idea to the siblings):

```csharp
private static void BuildPathMap(SyncTreeNodeResponse node, string parentPath, bool isRoot, Dictionary<Guid, string> map)
{
    // Root anchor: virtual root (Guid.Empty) and a scoped root (real node) both add NO segment.
    var currentPath = isRoot
        ? parentPath
        : string.IsNullOrEmpty(parentPath) ? node.Name : Path.Combine(parentPath, node.Name);

    if (!isRoot && node.NodeId != Guid.Empty)
        map[node.NodeId] = currentPath;

    foreach (var child in node.Children)
        BuildPathMap(child, currentPath, false, map);
}
```

Call it with `isRoot: true` at the top level. (Adapt the exact signature to the existing code;
the key rule is: the first call contributes no path segment and the scoped root's `NodeId` is
still recorded.)

### E3. Re-root `EnsureParentFolderAsync` (defined at 2434; call site at 2084)

- `var relativeDir = Path.GetRelativePath(context.LocalFolderPath, parentDir);`
- When resolving the server parent for uploads:
  - If `_serverFolderId.HasValue`, start folder creation at `currentParentId = _serverFolderId`
    (instead of `null` = server root) and resolve `relativeDir` against the **scoped** tree map.
  - When `_serverFolderId` is `null`, keep the existing root-anchored behavior exactly.

Verify `ReconcileServerTreeAsync` (1245) and `ResolveLocalPathAsync` (2304) consume the
re-rooted maps — once E2 is correct, downloads and deletes land under the scoped root
automatically because they combine `context.LocalFolderPath` with map-derived relative paths.

### E4. Tests (Phase E)

**File:** `tests/DotNetCloud.Client.Core.Tests/Sync/SyncEngineTests.cs`

Add focused tests (the existing tests construct `SyncEngine` with a `SelectiveSyncConfig`
and a fake `IDotNetCloudApiClient`; add a `ServerFolderId` to the test `SyncContext`):

- `BuildPathMap_ScopedRoot_DoesNotPrependFolderName` (call the private helper via a public
  seam or make the helper `internal` + `InternalsVisibleTo`)
- `BuildPathMap_FullTreeRoot_KeepsRootRelativePaths` (regression)
- `ApplyRemoteChanges_WithServerFolderId_PassesFolderIdToTree` (assert the fake received the
  folderId)
- `EnsureParentFolder_WithServerFolderId_SeedsScopedParent` (assert `CreateFolderAsync` was
  called with the scoped root as parent, not null)

---

## Phase F — Client: UI + add-folder flow + overlap guard

### F1. Lift the single-account guard for folders

**File:** `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/SettingsViewModel.cs`

- `CanAddAccount => !IsAddingAccount && !HasAccount;` (167) — change so adding a **folder**
  is allowed when an account exists. Keep "add account" gated on no-account; introduce
  `CanAddFolder => HasAccount && !IsAddingFolder`.
- `BeginAddAccountFlowAsync` (480) and `AddAccountAsync` (508): the strings
  "Only one account is supported in this client." remain only for the **account** flow.
  Folder additions go through a new path (F2) that does not hit this guard.
- Add `public ICommand AddFolderCommand { get; }` + `BeginAddFolderFlowAsync()`.

### F2. New `AddFolderDialog`

**File:** `src/Clients/DotNetCloud.Client.SyncTray/Views/AddFolderDialog.axaml(.cs)` (new)

- **Local picker:** reuse the exact pattern from
  `AddAccountDialogViewModel.BrowseFolderAsync` (`Views/AddAccountDialog.axaml.cs` line 104):
  `_owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "...", AllowMultiple = false })`.
- **Remote picker:** reuse `FolderBrowserViewModel`/`FolderBrowserItemViewModel`
  (`ViewModels/FolderBrowserViewModel.cs`) in a **single-select** mode. The view model already
  exposes `NodeId` and `RelativePath` on each item; add a `Guid? SelectedNodeId` +
  `string? SelectedRelativePath` that is set on click, and a `Func`/event to signal selection.
- The dialog result: `record AddFolderResult(string LocalFolderPath, Guid RemoteFolderNodeId, string RemoteFolderPath)`.
- Validate: non-empty local path; a remote folder selected.

### F3. Overlap guard (client)

**File:** new `src/Clients/DotNetCloud.Client.Core/Sync/SyncFolderOverlapGuard.cs` (static helper) —
or add to `SyncEngine`'s existing helpers. Implement:

```csharp
public static bool PathsOverlap(string a, string b)
{
    var fullA = Path.GetFullPath(a);
    var fullB = Path.GetFullPath(b);
    var comparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    bool IsWithin(string root, string candidate)
    {
        var rootWithSep = root.EndsWith(Path.DirectorySeparatorChar)
            ? root : root + Path.DirectorySeparatorChar;
        return string.Equals(candidate, root, comparison)
            || candidate.StartsWith(rootWithSep, comparison);
    }

    return IsWithin(fullA, fullB) || IsWithin(fullB, fullA);
}
```

In `SettingsViewModel.BeginAddFolderFlowAsync`, before creating the context, iterate existing
`SyncContextRegistration.LocalFolderPath` values and reject when
`SyncFolderOverlapGuard.PathsOverlap(existing, newPath)` (covers nested both ways and exact
duplicates). Show the error on the dialog.

Remote overlap is also checked locally (reject equal/ancestor/descendant of an existing
`ServerFolderId` using the selected folder's `RelativePath` vs existing `ServerFolderDisplayPath`)
and is enforced authoritatively server-side in Phase B.

### F4. Account card → folder list

**File:** `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/AccountViewModel.cs`

Add `IReadOnlyList<SyncFolderViewModel> Folders` where `SyncFolderViewModel` exposes
`ContextId`, `LocalFolderPath`, `RemoteFolderPath`, and `State`.

**File:** `src/Clients/DotNetCloud.Client.SyncTray/Views/SettingsWindow.axaml`

Replace the single `ContentControl Content="{Binding PrimaryAccount}"` with an
`ItemsControl` bound to `Accounts`, each card listing its `Folders`, with per-folder
"Open Folder", "Choose Folders", and "Remove" actions. Keep the "Connect your account" card
for the no-account state. Add the "Add folder" button bound to `AddFolderCommand`.

### F5. Tests (Phase F)

**File:** `tests/DotNetCloud.Client.SyncTray.Tests/ViewModels/SettingsViewModelTests.cs`

- `BeginAddFolder_WhenAccountExists_AllowsFolder`
- `BeginAddFolder_LocalPathInsideExistingRoot_ShowsError` (and reverse:
  `BeginAddFolder_LocalPathContainsExistingRoot_ShowsError`)
- `BeginAddFolder_DuplicateLocalPath_ShowsError`
- `BeginAddFolder_RemoteFolderNestedUnderExisting_ShowsError`

**File:** `tests/DotNetCloud.Client.Core.Tests/Sync/SyncFolderOverlapGuardTests.cs`

- `PathsOverlap_SamePath_True`
- `PathsOverlap_ChildInsideParent_True`
- `PathsOverlap_ParentContainsChild_True`
- `PathsOverlap_SiblingPaths_False`
- `PathsOverlap_CaseInsensitiveOnWindows` (conditional on OS or use a comparer parameter)

---

## Phase G — Client: DB-backed sync folder rules (remove `.selective-sync.json`)

### G1. New DB entity

**File:** `src/Clients/DotNetCloud.Client.Core/LocalState/LocalStateDbContext.cs`

Add a flat row entity (same file, next to `PendingOperationDbRow` etc.):

```csharp
public sealed class SyncFolderRule
{
    public int Id { get; set; }
    public required string RelativePath { get; set; } // relative to the sync root, forward slashes
    public bool IsInclude { get; set; }
    public string Source { get; set; } = "Manual";    // "Manual" | "SizeLimit"
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

Add `public DbSet<SyncFolderRule> SyncFolderRules => Set<SyncFolderRule>();` and in
`OnModelCreating`:

```csharp
modelBuilder.Entity<SyncFolderRule>(e =>
{
    e.HasKey(r => r.Id);
    e.Property(r => r.RelativePath).IsRequired();
    e.HasIndex(r => new { r.RelativePath, r.Source }).IsUnique();
});
```

### G2. Schema evolution (MANDATORY for existing `state.db`)

**File:** `src/Clients/DotNetCloud.Client.Core/LocalState/LocalStateDb.cs`

`EnsureCreatedAsync` does **not** add tables to an existing database. Add to
`RunSchemaEvolutionAsync` (which already runs raw SQL against an open `SqliteConnection`):

```sql
CREATE TABLE IF NOT EXISTS SyncFolderRules (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    RelativePath TEXT NOT NULL,
    IsInclude INTEGER NOT NULL,
    Source TEXT NOT NULL DEFAULT 'Manual',
    UpdatedAt TEXT NOT NULL DEFAULT '0001-01-01 00:00:00'
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_sync_folder_rules_path_source
    ON SyncFolderRules (RelativePath, Source);
```

### G3. `ILocalStateDb` methods

**File:** `src/Clients/DotNetCloud.Client.Core/LocalState/ILocalStateDb.cs` + `LocalStateDb.cs`

Add (all take `string dbPath` first, like every other method):

```csharp
Task<IReadOnlyList<SyncFolderRule>> GetSyncFolderRulesAsync(string dbPath, CancellationToken ct = default);
Task ReplaceSyncFolderRulesAsync(string dbPath, IReadOnlyList<SyncFolderRule> rules, CancellationToken ct = default);
```

`ReplaceSyncFolderRulesAsync` deletes existing rows then inserts the new set in one
transaction (via a single `LocalStateDbContext` + `SaveChangesAsync`).

### G4. Rewire `ISelectiveSyncConfig`

**File:** `src/Clients/DotNetCloud.Client.Core/SelectiveSync/ISelectiveSyncConfig.cs`

Remove the file-path persistence contract and replace it with DB-backed methods:

```csharp
public interface ISelectiveSyncConfig
{
    bool IsIncluded(Guid contextId, string localPath);
    void Include(Guid contextId, string folderPath);
    void Exclude(Guid contextId, string folderPath);
    void ClearRules(Guid contextId);
    IReadOnlyList<SelectiveSyncRule> GetRules(Guid contextId);
    Task LoadAsync(ILocalStateDb stateDb, string dbPath, CancellationToken ct = default);
    Task SaveAsync(ILocalStateDb stateDb, string dbPath, CancellationToken ct = default);
}
```

(Keep `IsReservedExcludedPath` — the reserved `/_DotNetCloud` root.)

**File:** `src/Clients/DotNetCloud.Client.Core/SelectiveSync/SelectiveSyncConfig.cs`

- Replace JSON `File` I/O with `ILocalStateDb.GetSyncFolderRulesAsync` /
  `ReplaceSyncFolderRulesAsync`. Map `SyncFolderRule` ↔ `SelectiveSyncRule` (drop the
  `Source` into a `SelectiveSyncRule.Source` property, default `"Manual"`).
- Keep `IsIncluded` evaluation logic unchanged (exclude precedence, longest-match-wins).

### G5. Rewire call sites

**File:** `src/Clients/DotNetCloud.Client.Core/Sync/SyncContextManager.cs`

- Delete `GetSelectiveSyncConfigPath` (640).
- `StartContextInternalAsync` (427): replace
  `selectiveSync.LoadAsync(GetSelectiveSyncConfigPath(registration), ct)` with
  `selectiveSync.LoadAsync(stateDb, Path.Combine(registration.DataDirectory, "state.db"), ct)`.
- `UpdateSelectiveSyncAsync` (358; the `SaveAsync` call is at 376): replace
  `SaveAsync(GetSelectiveSyncConfigPath(reg), ct)` with
  `SaveAsync(running.StateDb, running.SyncContext.StateDatabasePath, ct)`.

**File:** `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/SettingsViewModel.cs`

- `ShowFolderBrowserForAccountAsync` (802): remove the
  `Path.Combine(account.LocalFolderPath, ".selective-sync.json")` computation; load rules
  from DB instead (via `_syncManager` — add a `GetSelectiveSyncRulesAsync(contextId)` on
  `ISyncContextManager` if one does not exist, delegating to `RunningContext`).

**File:** `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/FolderBrowserViewModel.cs`

- Remove the `configFilePath` constructor parameter (it is already unused for I/O).

### G6. One-time legacy import

In `SyncContextManager.StartContextInternalAsync`, after loading rules from DB: if
`File.Exists(Path.Combine(registration.LocalFolderPath, ".selective-sync.json"))`, deserialize
the legacy JSON, merge rules into the DB (manual rules only), then delete the legacy file
(best-effort, log on failure). Guard so it runs once (deleting the file is the natural
idempotency gate).

### G7. Tests (Phase G)

**File:** `tests/DotNetCloud.Client.Core.Tests/LocalState/LocalStateDbTests.cs` (new or extend)

- `SyncFolderRules_RoundTrip_Persists`
- `ReplaceSyncFolderRules_ReplacesExistingSet`
- `Initialize_ExistingDb_AddsSyncFolderRulesTable` (create a DB with an older schema, run
  `InitializeAsync`, assert the table exists — exercises `RunSchemaEvolutionAsync`)

**File:** `tests/DotNetCloud.Client.Core.Tests/SelectiveSync/SelectiveSyncConfigTests.cs`

Rewrite the two file-based tests:

- `SaveAndLoad_PersistsRules` → use an in-memory `LocalStateDb` against a temp SQLite path.
- `LoadAsync_NonExistentFile_NoOp` → `Load_EmptyDb_NoOp`.

Add: `Load_LegacyJsonImportsRules` (optional — if legacy import lives in
`SyncContextManager`, cover it in a `SyncContextManager` test instead).

---

## Phase H — Client: folder size limit

### H1. Settings model + UI

**File:** `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/SettingsViewModel.cs`

Add to the `SyncTrayLocalSettings` class (1111):

```csharp
public bool LimitFolderSizeEnabled { get; set; }
public long MaxFolderSizeBytes { get; set; } = 250L * 1024 * 1024; // 250 MiB default
```

Persist via the existing `PersistLocalSettingsAsync`/`LoadLocalSettings` path.

**File:** `src/Clients/DotNetCloud.Client.SyncTray/Views/SettingsWindow.axaml`

Add a "Sync limits" group: a toggle bound to `LimitFolderSizeEnabled` and a numeric input
(in MB, converted to/from `MaxFolderSizeBytes`) bound to `MaxFolderSizeMb`.

### H2. Size planner (new service)

**File:** `src/Clients/DotNetCloud.Client.Core/Sync/SyncFolderSizePlanner.cs` (new)

Responsibilities:

1. **Compute recursive folder sizes.**
   - Download direction: from the scoped `SyncTreeNodeResponse` returned by
     `GetFolderTreeAsync(context.ServerFolderId)` — sum `Size` over each folder's descendants
     (file nodes carry `Size`; folder nodes carry 0).
   - Upload direction: from a lazily-cached local scan (sum file lengths per directory),
     refreshed at the full-scan interval. Cache keyed by context id.

2. **Determine the exclusion set (maximize coverage).** Implement top-down:

```
Plan(node):
    size = RecursiveSize(node)
    if size <= limit: return []            // include this folder entirely
    overChildren = children where RecursiveSize(child) > limit
    if overChildren is empty:
        return [node]                      // smallest over-limit unit (drill-down ended; walk-up finds this folder)
    else:
        exclusions = []
        foreach child in overChildren: exclusions += Plan(child)
        return exclusions                  // include non-over children as-is
```

The result is the list of folders to be skipped (each is the deepest folder whose total
size still exceeds the limit).

3. **Emit ask-once prompts.** For each exclusion candidate, check the `SyncFolderRules` table
   (Source = `SizeLimit`) for a stored decision:
   - Decision exists → apply it (`IsInclude = false` → skip; `true` → do not exclude).
   - No decision → raise an event (`SizeLimitDecisionRequested`) with the folder's relative
     path + size; the UI shows a prompt and records the user's choice via
     `ReplaceSyncFolderRulesAsync`. Default if dismissed = skip.

4. **Apply decisions as engine rules.** Write `SyncFolderRule` rows with
   `Source = "SizeLimit"` and `IsInclude` per the decision; the engine's existing
   `_selectiveSync.IsIncluded` then skips excluded folders **silently + logged** (no
   transfer). Add a log line per skipped folder (engine already has `ILogger`).

### H3. Engine + tray integration

- **File:** `src/Clients/DotNetCloud.Client.SyncTray/App.axaml.cs` + `TrayIconManager.cs`
  (or `SettingsViewModel`): subscribe to `SizeLimitDecisionRequested`; show a small dialog
  ("Folder 'bigfiles' is 1.2 GB — over your 250 MB limit. Sync it or skip it?") and store the
  choice via `_syncManager` (add `ApplySizeLimitDecisionAsync(contextId, relativePath, bool sync)`).
- **File:** `src/Clients/DotNetCloud.Client.Core/Sync/SyncContextManager.cs`: run the planner
  at sync start (after rules load, before `engine.StartAsync` or inside the first pass) when
  `LimitFolderSizeEnabled` is true. Expose `ApplySizeLimitDecisionAsync`.

### H4. Tests (Phase H)

**File:** `tests/DotNetCloud.Client.Core.Tests/Sync/SyncFolderSizePlannerTests.cs` (new)

- `Plan_AllUnderLimit_NoExclusions`
- `Plan_RootOverLimitSingleBigChild_ExcludesOnlyThatChild` (the `~/Documents` + `bigfiles` case)
- `Plan_FolderOverLimitNoSingleChildOver_ExcludesWholeFolder` (walk-up case)
- `Plan_LeafOverLimitDueToOneHugeFile_ExcludesWholeFolder` (the confirmed decision)
- `Plan_NestedBigChild_ExcludesDeepestOverLimitFolder`
- `RecursiveSize_FolderAggregatesDescendantFileSizes`
- `ApplyDecision_StoredAsSizeLimitRule`
- `Plan_ExistingSkipDecision_DoesNotRePrompt`
- `Plan_NoDecision_RaisesDecisionRequested`

**File:** `tests/DotNetCloud.Client.Core.Tests/Sync/SyncEngineTests.cs`

- `ApplyRemoteChanges_SizeLimitExcludedFolder_IsSkipped` (assert excluded folder is not
  downloaded and a skip is logged).

---

## Phase I — Server registration sync

**File:** `src/Clients/DotNetCloud.Client.Core/Sync/SyncContextManager.cs`

- After a successful `AddContextAsync` → call `api.RegisterSyncFolderAsync(ServerFolderId)`
  (only when `ServerFolderId` is not null; best-effort — log and continue on failure).
- In `RemoveContextAsync` → `api.DeleteSyncFolderAsync(ServerFolderId)` (best-effort).
- On startup (`LoadContextsAsync`), reconcile: for each registered context with a
  `ServerFolderId`, ensure it exists server-side (call `ListSyncFoldersAsync` and re-register
  if missing).

### Tests (Phase I)

**File:** `tests/DotNetCloud.Client.Core.Tests/Sync/SyncContextManagerTests.cs` (new or extend)

- `AddContext_WithServerFolder_RegistersOnServer`
- `RemoveContext_WithServerFolder_UnregistersOnServer`
- `LoadContexts_ReconcilesMissingServerRegistration`

---

## Phase J — Docs (MANDATORY) + final verification

1. Update `docs/IMPLEMENTATION_CHECKLIST.md` — mark completed items `✓`, pending `☐`, using
   **targeted edits** (not full-file replacement).
2. Update `docs/MASTER_PROJECT_PLAN.md` — Quick Status Summary table AND the corresponding
   step sections (Status / Deliverables with `✓`/`☐` / Notes), also via **targeted edits**.
3. Run:

```bash
dotnet build
dotnet test
```

4. Confirm the server migration applies cleanly on both providers (see Phase A4).

---

## Complete file inventory

| File                                                                                                                          | Change                                      |
| ----------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------- |
| `src/Modules/Files/DotNetCloud.Modules.Files/Models/SyncFolderRegistration.cs`                                                | new entity                                  |
| `src/Modules/Files/DotNetCloud.Modules.Files.Data/Configuration/SyncFolderRegistrationConfiguration.cs`                       | new config                                  |
| `src/Modules/Files/DotNetCloud.Modules.Files.Data/FilesDbContext.cs`                                                          | DbSet + ApplyConfiguration                  |
| `src/Modules/Files/DotNetCloud.Modules.Files/Services/ISyncFolderRegistrationService.cs`                                      | new interface                               |
| `src/Modules/Files/DotNetCloud.Modules.Files.Data/Services/SyncFolderRegistrationService.cs`                                  | new impl                                    |
| `src/Modules/Files/DotNetCloud.Modules.Files/DTOs/SyncDtos.cs`                                                                | new DTOs                                    |
| `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/SyncFoldersController.cs`                                       | new controller                              |
| `src/Modules/Files/DotNetCloud.Modules.Files.Data/FilesServiceRegistration.cs`                                                | register service                            |
| `src/Modules/Files/DotNetCloud.Modules.Files.Data/Services/SyncService.cs`                                                    | recursive folderId filter                   |
| `src/Clients/DotNetCloud.Client.Core/Api/IDotNetCloudApiClient.cs` + `DotNetCloudApiClient.cs` + `ApiModels.cs`               | folderId overload + registration methods    |
| `src/Clients/DotNetCloud.Client.Core/Sync/SyncContext.cs`, `SyncContextRegistration.cs`, `AddAccountRequest.cs`               | `ServerFolderId`                            |
| `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs`                                                                      | folder-scoped sync + size-limit skip        |
| `src/Clients/DotNetCloud.Client.Core/Sync/SyncFolderOverlapGuard.cs`                                                          | new helper                                  |
| `src/Clients/DotNetCloud.Client.Core/Sync/SyncFolderSizePlanner.cs`                                                           | new planner                                 |
| `src/Clients/DotNetCloud.Client.Core/Sync/SyncContextManager.cs`                                                              | model plumbing, DB rules, registration sync |
| `src/Clients/DotNetCloud.Client.Core/LocalState/LocalStateDbContext.cs`, `LocalStateDb.cs`, `ILocalStateDb.cs`                | `SyncFolderRule` + methods                  |
| `src/Clients/DotNetCloud.Client.Core/SelectiveSync/SelectiveSyncConfig.cs`, `ISelectiveSyncConfig.cs`                         | DB-backed persistence                       |
| `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/SettingsViewModel.cs`, `AccountViewModel.cs`, `FolderBrowserViewModel.cs` | UI wiring, settings, folder list            |
| `src/Clients/DotNetCloud.Client.SyncTray/Views/SettingsWindow.axaml`, `AddFolderDialog.axaml(.cs)`                            | UI (new dialog)                             |

## Test file inventory

| File                                                                                              | Add/update                |
| ------------------------------------------------------------------------------------------------- | ------------------------- |
| `tests/DotNetCloud.Modules.Files.Tests/Configuration/SyncFolderRegistrationConfigurationTests.cs` | new                       |
| `tests/DotNetCloud.Modules.Files.Tests/Services/SyncFolderRegistrationServiceTests.cs`            | new                       |
| `tests/DotNetCloud.Modules.Files.Tests/Services/SyncServiceTests.cs`                              | extend (recursive filter) |
| `tests/DotNetCloud.Client.Core.Tests/Api/DotNetCloudApiClientTests.cs`                            | new/extend                |
| `tests/DotNetCloud.Client.Core.Tests/Sync/SyncEngineTests.cs`                                     | extend                    |
| `tests/DotNetCloud.Client.Core.Tests/Sync/SyncFolderOverlapGuardTests.cs`                         | new                       |
| `tests/DotNetCloud.Client.Core.Tests/Sync/SyncFolderSizePlannerTests.cs`                          | new                       |
| `tests/DotNetCloud.Client.Core.Tests/Sync/SyncContextManagerTests.cs`                             | new/extend                |
| `tests/DotNetCloud.Client.Core.Tests/LocalState/LocalStateDbTests.cs`                             | new/extend                |
| `tests/DotNetCloud.Client.Core.Tests/SelectiveSync/SelectiveSyncConfigTests.cs`                   | rewrite file-based tests  |
| `tests/DotNetCloud.Client.SyncTray.Tests/ViewModels/SettingsViewModelTests.cs`                    | extend                    |
| `tests/DotNetCloud.Client.SyncTray.Tests/ViewModels/FolderBrowserViewModelTests.cs`               | update ctor               |

## Out of scope

- No multi-root single-engine refactor (one engine per folder).
- No cross-device local-path propagation (server stores remote registrations; local paths
  stay device-local).
- No automatic re-pointing when a remote folder is moved/renamed (manual edit).
