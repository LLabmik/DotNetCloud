# Shared File Folder Implementation Plan

> Goal: Deliver admin-managed shared folders in the Files module through a virtual `_DotNetCloud` root, backed by organization-scoped groups, group-filtered search, and per-user media-library source selection for Music, Photos, and Video.

> Scope: Core group administration, admin shared-folder definitions, Files virtual-folder composition, nested mounted-directory browsing, search indexing and visibility, media-module scan source selection, APIs, Blazor UI, validation, and rollout.

> Status: In Progress

> Progress: Workstreams 4.1 and 4.2 are complete, and 4.3 is now in progress. Files now has persisted admin shared-folder definition/group-grant entities, the matching `AddAdminSharedFolders` migration, and a rooted path validator that canonicalizes candidate source paths, verifies directory existence, and rejects duplicate or overlapping registrations beneath the configured admin shared-folder root. Next focus: admin CRUD/group-assignment surfaces on top of that model, then `_DotNetCloud` browse composition.

---

## 1. Success Criteria

- Admins can create and manage groups, including a protected built-in `All Users` group per organization or default organization.
- Admins can register server-local shared folders, assign one or more groups to each folder, and manage rescan/reindex operations.
- Every user sees a virtual `_DotNetCloud` folder in Files.
- Each eligible admin shared folder appears as a top-level child under `_DotNetCloud`.
- Nested subdirectories inside each admin shared folder are preserved and browseable as nested virtual nodes.
- `Shared With Me` remains limited to explicit user-to-user shares and does not absorb admin shared folders.
- Files permission evaluation consistently honors direct user shares, team shares, and group shares.
- Search includes mounted shared-folder content in v1, with group-aware visibility and navigation back into the correct virtual path.
- Search indexes file contents in v1 where supported extractors already exist, and falls back to filename, path, and metadata indexing where they do not.
- Music, Photos, and Video let each user choose which eligible shared folders to scan on a per-module basis.
- Shared-folder-backed media appears in each user library without pretending that the scanned user owns the original FileNode.
- Sync clients ignore `_DotNetCloud` admin shares in v1.

---

## 2. Dependencies And Preconditions

- Existing organization-scoped group entities in Core.Data remain the foundation for group membership.
- Authorization continues to use the existing admin policy and capability patterns.
- Files share support is aligned with the documented share types and expanded to fully enforce team and group visibility.
- Files virtual-folder support is introduced before search and media integration rely on mounted shared-folder traversal.
- Search infrastructure remains available as the primary full-text path for Files searches.
- Music, Photos, and Video continue to build on the common media-library scanner abstraction, but that abstraction must expand beyond owned FileNode folder scanning.
- The first delivery targets web and API behavior; sync-client parity is explicitly deferred.

---

## 3. Architecture Summary

The feature is not a simple extension of owned FileNode trees. The current Files model assumes a single `OwnerId`, content-addressed storage, module-managed blobs, trash/version semantics, and owner-scoped indexing. Admin shared folders are fundamentally different: they are server-local paths that DotNetCloud exposes read-only through a virtual provider.

The implementation should therefore split into four architectural layers:

1. Identity and administration:
   Group management, built-in `All Users` semantics, and admin APIs and UI.
2. Files virtual provider:
   Admin shared-folder definitions, nested directory enumeration, path validation, and `_DotNetCloud` composition.
3. Visibility and discovery:
   Group-aware permission evaluation and search visibility for mounted content.
4. Consumer modules:
   Media modules selecting shared folders as scan sources without requiring those sources to be owned FileNodes.

---

## 4. Work Breakdown Structure

## 4.1 Group Foundation And Admin Surfaces

### Objectives

- Reuse the existing organization-scoped group model instead of introducing a parallel global-group system.
- Make `All Users` a real built-in concept rather than a fragile naming convention.
- Deliver admin-facing CRUD and membership management for groups.

### Deliverables

- Extend the existing group model with either:
  - a built-in or system flag, or
  - a membership mode distinguishing manual groups from implicit all-organization-user groups.
- Add migration and backfill logic to create exactly one `All Users` group per organization or default organization.
- Prevent rename, delete, and manual membership mutation of the protected built-in group.
- Add Group DTOs mirroring the existing Team DTO patterns.
- Add `IGroupDirectory` and `IGroupManager` contracts aligned with the existing Team capability model.
- Add service implementations for group queries, CRUD, and membership changes.
- Add admin REST endpoints for:
  - listing groups
  - viewing a group
  - creating a group
  - updating a group
  - deleting a group
  - listing members
  - adding members
  - removing members
- Add dedicated admin UI for group management.

### Exit Criteria

- Group CRUD works through admin API and UI.
- `All Users` exists consistently across organizations and is protected from accidental mutation.
- Group membership resolution is available to downstream Files, Search, and media flows.

---

## 4.2 Files Share-Model Hardening

### Objectives

- Close the gap between the documented sharing model and the actual Files implementation.
- Ensure group and team share visibility is real before admin shared folders are layered on top.

### Deliverables

- Update the Files permission engine so effective permissions honor:
  - direct user shares
  - team shares
  - group shares
  - inherited parent-folder shares
- Add a membership-resolution abstraction inside the Files module so it can resolve caller teams and groups without embedding identity queries directly into Files data services.
- Keep `Shared With Me` scoped to explicit user-targeted shares only.
- Add a separate listing path for team/group-accessible content feeding the `_DotNetCloud` experience.
- Align Files sharing documentation and API contract notes with actual behavior.

### Exit Criteria

- Permissions are consistent across user, team, and group share paths.
- `Shared With Me` remains user-share-only.
- Admin shared folders can build on a stable group-aware Files access model.

---

## 4.3 Admin Shared Folder Model

### Objectives

- Introduce a first-class Files-side model for admin-managed shared folders.
- Keep v1 simple, cross-platform, and operationally predictable.

### Deliverables

- Add Files-module entities for admin shared-folder definitions and their granted groups.
- Recommended shared-folder fields:
  - organization ID
  - display name
  - source path
  - enabled flag
  - read-only access mode
  - created and updated audit fields
  - crawl mode
  - last indexed timestamp
  - next scheduled scan
  - last scan status
  - reindex state
- Restrict v1 source type to server-local paths that the OS already mounted.
- Restrict v1 admin shares to folder roots only.
- Reject duplicate display names under `_DotNetCloud`.
- Implement path validation and canonicalization that:
  - rejects overlaps
  - rejects duplicates
  - rejects traversal outside the configured root
  - verifies path existence
  - verifies the path is a directory
  - safely resolves nested relative paths inside the configured root
- Add admin API and UI for shared-folder CRUD and group assignment.
- Add admin controls for scheduled rescans and manual reindex actions.

### Explicit v1 Constraints

- DotNetCloud does not mount SMB, NFS, or Windows shares itself.
- Admins provide paths that already exist on the host.
- Mounted shared folders are read-only in v1.
- Single-file admin share roots are out of scope.

### Exit Criteria

- Admins can register and manage shared folders safely.
- Folder validation blocks invalid and ambiguous configurations.
- Shared-folder definitions are ready for virtual enumeration and search crawling.

---

## 4.4 Virtual `_DotNetCloud` Root And Browse Flow

### Objectives

- Surface admin shared folders inside Files without forcing them into the owned FileNode model.
- Preserve the real nested directory tree of each shared folder.

### Deliverables

- Introduce a virtual-folder service or browse-composition layer capable of mixing virtual nodes with real FileNode listings.
- Update root listing so every user sees a synthetic `_DotNetCloud` folder.
- Inside `_DotNetCloud`, render:
  - top-level admin shared folders the caller can access through group membership
  - a synthetic `Shared With Me` folder
- When a user opens an admin shared folder:
  - enumerate the real directory tree beneath its configured source path
  - surface child folders and files as nested virtual nodes
  - preserve the underlying hierarchy instead of flattening descendants
- Keep admin shared folders top-level under `_DotNetCloud`; do not merge them into `Shared With Me`.
- Extend Files API and UI view models so virtual nodes carry stable identity, such as:
  - shared-folder definition ID
  - source kind
  - relative path
- Enforce read-only behavior across root and nested mounted paths.

### Exit Criteria

- `_DotNetCloud` exists for every user.
- Eligible shared folders show up under `_DotNetCloud`.
- Nested directories remain nested.
- Reads work and mutations are blocked.

---

## 4.5 Search In The First Delivery

### Objectives

- Include mounted shared-folder content in v1 search.
- Move beyond the current owner-scoped search visibility model.

### Deliverables

- Extend the current Search schema and query model beyond `OwnerId`-only visibility.
- Add a mount indexing model keyed by:
  - shared-folder definition ID
  - relative path
  - entity type
- Add visibility data for mounted search entries using either:
  - organization plus granted group scope, or
  - a normalized access table keyed to index entries.
- Add a crawler and rescan pipeline for admin shared folders.
- Use scheduled rescans plus a manual reindex button in v1.
- Update Files search endpoints and Search providers so results include:
  - direct ownership
  - explicit user shares
  - team shares
  - group shares
  - mounted shared-folder visibility
- Support search result navigation back into the correct virtual path under `_DotNetCloud`.
- Index file contents in v1 where supported extractors already exist.
- Fall back to filename, path, and metadata indexing for unsupported file types.

### Exit Criteria

- Mounted shared-folder content appears in search for eligible users only.
- Search results point back to the correct virtual path.
- Supported file types contribute extracted content in v1.

---

## 4.6 Media Module Shared-Folder Scan Integration

### Objectives

- Let users select shared folders as scan sources for Music, Photos, and Video.
- Keep the selection per user and per module.
- Avoid faking shared sources as owned FileNodes.

### Deliverables

- Replace the single-folder settings pattern:
  - `media-library:music-folder-id/path`
  - `media-library:photos-folder-id/path`
  - `media-library:video-folder-id/path`
  with per-user, per-module multi-source selection.
- Recommended persistence model:
  dedicated user media-library source records keyed by user, module, source kind, source identifier, display path, enabled flag, and last scan state.
- Keep source selection per module so users can choose different shared folders for Music, Photos, and Video.
- Preserve existing owned FileNode folder scanning, but widen the source abstraction from owned folder IDs to source kinds such as:
  - `OwnedFileNode`
  - `SharedMount`
- Extend or replace `IMediaLibraryScanner.ScanFolderAsync(Guid? ...)` and `MediaFolderImportService` so shared-folder scans can traverse virtual admin shares under `_DotNetCloud`.
- Update indexing, playback, thumbnail, and streaming flows because current media records are anchored to `FileNodeId` and `OwnerId`.
- Recommended v1 direction:
  keep resulting media library entries user-owned, but attach source references to shared content instead of pretending the scanned user owns the original FileNode.
- Add cleanup rules for:
  - deselecting a shared folder
  - losing group access
  - source deletions
- Update Music, Photos, and Video UI pages to:
  - browse eligible shared folders
  - show selected scan sources
  - run scans across all selected sources

### Explicit v1 Media Rules

- Media scanning of admin shared folders under `_DotNetCloud` is in scope.
- Media scanning of `Shared With Me` items is out of scope for v1.
- `Shared With Me` media scanning should remain documented as a future feature with its own provenance and cleanup rules.

### Exit Criteria

- Each media module can select shared folders independently.
- Scans work across selected shared sources.
- Playback, thumbnails, and streaming work for shared-source items.
- Access-loss cleanup behaves predictably.

---

## 4.7 Verification And Rollout

### Objectives

- Validate the identity model, Files behavior, search visibility, and media integration together.

### Deliverables

- Unit tests for:
  - `All Users` seeding and protection
  - group membership resolution
  - Files permission evaluation for user, team, and group shares
  - shared-folder path validation
  - media source selection persistence
- Integration tests for:
  - group admin endpoints
  - shared-folder admin endpoints
  - `_DotNetCloud` listing
  - nested virtual browsing
  - read-only enforcement across root and nested mounted paths
  - search visibility and navigation
  - media shared-source scan enumeration and cleanup
- Manual verification using at least:
  - two users in different groups
  - one user only in `All Users`
  - a real server-local mounted directory tree
- Manual validation for:
  - nested directory browsing
  - duplicate display-name rejection
  - denied write attempts
  - search indexing and content extraction for supported types
  - Music, Photos, and Video selecting different shared folders
  - playback, thumbnails, and streaming for shared-source items
- Validation that sync clients ignore `_DotNetCloud` admin shares in v1.

### Exit Criteria

- Group and shared-folder behavior is stable end to end.
- Search only returns mounted content to eligible users.
- Media modules can scan approved shared folders without collapsing provenance.
- Sync clients leave `_DotNetCloud` content alone in v1.

---

## 5. Key Decisions

- v1 shared folders use server-local paths only.
- DotNetCloud does not initiate SMB, NFS, or Windows share mounts itself in v1.
- Groups remain organization-scoped.
- The platform seeds a reserved `All Users` group per organization or default organization.
- `_DotNetCloud` contains direct children for eligible admin shared folders plus a `Shared With Me` folder.
- Each admin shared folder preserves its underlying subdirectory hierarchy beneath its virtual root.
- Admin shares are folder-only in v1.
- Duplicate admin-share display names under `_DotNetCloud` are rejected.
- `Shared With Me` contains only explicit user-to-user shares.
- Mounted shared folders are read-only in v1.
- Search is included in the first delivery.
- Mounted shared-folder search uses per-group and per-mount visibility, not the current FileNode-only owner-scoped model.
- Search freshness for mounted content uses scheduled rescans plus a manual reindex action.
- Search includes extracted file content where supported extractors already exist, with fallback to filename, path, and metadata indexing otherwise.
- Music, Photos, and Video each get per-user, per-module shared-folder scan selection rather than one global shared-media source list.
- V1 media shared-folder scanning targets admin shared folders exposed under `_DotNetCloud`.
- Media scanning of `Shared With Me` items is explicitly out of scope for v1 and documented as a future feature.
- Sync clients ignore `_DotNetCloud` admin shares in v1.

---

## 6. Relevant Implementation Surfaces

- `src/Core/DotNetCloud.Core.Data/Entities/Organizations/Group.cs`
- `src/Core/DotNetCloud.Core.Data/Entities/Organizations/GroupMember.cs`
- `src/Core/DotNetCloud.Core.Data/Context/CoreDbContext.cs`
- `src/Core/DotNetCloud.Core/Capabilities/ITeamDirectory.cs`
- `src/Core/DotNetCloud.Core/Capabilities/ITeamManager.cs`
- `src/Core/DotNetCloud.Core.Auth/Capabilities/TeamManagerService.cs`
- `src/Core/DotNetCloud.Core.Server/Controllers/UserManagementController.cs`
- `src/UI/DotNetCloud.UI.Web.Client/Pages/Admin/Organizations.razor`
- `src/Modules/Files/DotNetCloud.Modules.Files.Data/Services/PermissionService.cs`
- `src/Modules/Files/DotNetCloud.Modules.Files.Data/Services/ShareService.cs`
- `src/Modules/Files/DotNetCloud.Modules.Files.Data/Services/FileService.cs`
- `src/Modules/Files/DotNetCloud.Modules.Files/Models/FileNode.cs`
- `src/Modules/Files/DotNetCloud.Modules.Files/Services/LocalFileStorageEngine.cs`
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/FileBrowser.razor.cs`
- `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/FilesController.cs`
- `src/Modules/Files/DotNetCloud.Modules.Files.Host/Program.cs`
- `src/Core/DotNetCloud.Core/Services/IMediaLibraryScanner.cs`
- `src/Core/DotNetCloud.Core.Server/Services/MediaFolderImportService.cs`
- `src/Core/DotNetCloud.Core.Auth/Services/UserSettingsService.cs`
- `src/Modules/Music/DotNetCloud.Modules.Music/UI/MusicPage.razor.cs`
- `src/Modules/Photos/DotNetCloud.Modules.Photos/UI/PhotosPage.razor.cs`
- `src/Modules/Video/DotNetCloud.Modules.Video/UI/VideoPage.razor.cs`
- `src/Modules/Music/DotNetCloud.Modules.Music.Data/Models/Track.cs`
- `src/Modules/Photos/DotNetCloud.Modules.Photos.Data/Models/Photo.cs`
- `src/Modules/Video/DotNetCloud.Modules.Video.Data/Models/Video.cs`
- `src/Modules/Search/DotNetCloud.Modules.Search.Data/Models/SearchIndexEntry.cs`
- `src/Modules/Search/DotNetCloud.Modules.Search/Services/SearchQueryService.cs`
- `src/Modules/Search/DotNetCloud.Modules.Search/Services/PostgreSqlSearchProvider.cs`
- `docs/modules/SHARING.md`

---

## 7. Verification Checklist

- Group seeding produces exactly one protected `All Users` group per organization or default organization.
- Group membership resolution works for manual groups and implicit `All Users` membership.
- Files permission evaluation is correct for direct user shares, team shares, group shares, and inherited folder access.
- `_DotNetCloud` always appears and renders eligible shared folders only.
- Nested mounted directories remain nested during browsing.
- Mounted paths reject uploads, renames, moves, deletes, and re-sharing in v1.
- Search results for mounted content are filtered by granted groups.
- Search results navigate back into the correct virtual subdirectory path.
- Supported file types contribute extracted content to search in v1.
- Music, Photos, and Video can each choose different shared folders as scan sources.
- Shared-source media indexing cleans up when access is removed or a source is deselected.
- Shared-source playback, thumbnail generation, and streaming work without requiring owned FileNode provenance for the scanning user.
- Sync clients ignore `_DotNetCloud` admin shares.

---

## 8. Future Features And Deferred Scope

- Shared With Me folder scanning for Music, Photos, and Video.
- App-managed SMB, NFS, or Windows share connection workflows.
- Write-capable admin shared folders.
- Filesystem watcher support for near-real-time mounted-folder freshness.
- Sync-client browsing and mirroring of `_DotNetCloud` admin shares.
- Expanded search extractors for unsupported mounted file types.

For the deferred `Shared With Me` media-scanning feature specifically, the later design should define separate provenance, cleanup, and access-loss rules rather than reusing the admin shared-folder model unchanged.
