# Plan: Shared-with-Me Virtual Module Folders

> **Status:** COMPLETE on `fix/sharing` (2026-09-08). Phases 1–5 all done; live E2E verified (sharer + recipient, user and team) before commit.
> **Branch:** `fix/sharing`
> **Owner:** Files module (virtual tree) + core capability aggregator + module "shared with me" surfaces + module UIs
> **Scope:** Files, Notes, Photos, Contacts, Calendar
> **Related:** `docs/SHARE_DIALOG_UNIFICATION_AND_TEAM_SHARES_PLAN.md`

---

## 1. Problem Statement

When a note (or any module resource) is shared with a user, the recipient currently has no single, obvious
place to find "everything shared with me":

- The Files module's virtual `_DotNetCloud/SharedWithMe` folder (`VirtualMountedNodeRegistry.SharedWithMeRootId`)
  only lists **File** shares (`FileService.ListSharedWithMeChildrenAsync`). Notes/Photos/Contacts/Calendar items
  are invisible there.
- The Notes module's own "Shared with Me" sidebar item lists every note the user can see (including their own)
  with no ownership/shared badge and no read-only affordance — a recipient can open a read-only share and only
  discovers it cannot be saved on save.

**Goal (user-confirmed):** `_DotNetCloud/SharedWithMe` gains one virtual folder **per module** (Files, Notes,
Photos, Contacts, Calendar). Each folder contains the **actual shared items** (virtual entries) for that module;
clicking an item **deep-links to the owning module** (e.g. `/notes?id=…`). Separately, module lists surface a
**shared/read-only** affordance.

---

## 2. Decisions (confirmed with user)

- ☑ Per-module virtual folders live **inside Files' existing `_DotNetCloud/SharedWithMe` folder**.
- ☑ Cover **all sharing modules**: Files, Notes, Photos, Contacts, Calendar.
- ☑ Module folders contain the **actual shared items** as virtual entries; clicking opens the item in the
  owning module.
- ☑ Module lists get **badges + read-only UI** so recipients know an item is shared and whether they can edit.

Out of scope (this pass):

- ☐ Revoke/manage shares from the aggregated view (still done in the owning module).
- ☐ Team-specific sub-grouping inside a module folder (teams flatten into the module folder).
- ☐ Cross-module "Shared by me" (mirror surface) — future.

---

## 3. Architecture & Facts

### 3.1 The existing Files virtual tree

- `VirtualMountedNodeRegistry` (`Files.Data/Services/VirtualMountedNodeRegistry.cs`):
  - `DotNetCloudRootId` = stable guid for `virtual::_dotnetcloud-root`.
  - `SharedWithMeRootId` = stable guid for `virtual::_dotnetcloud-shared-with-me`.
  - admin-shared folders use the same stable-guid + `MountedNodeEntry` pattern.
- `FileService` synthesizes the `_DotNetCloud` root, a `SharedWithMe` folder (`sourceKind: "SharedWithMeRoot"`),
  and `ListSharedWithMeChildrenAsync` returns File-share virtual `FileNodeDto`s (`VirtualSourceKind =
"SharedWithMe"`, `IsVirtual`, `IsReadOnly`).
- `FileDirectoryService` routes listing of `SharedWithMeRootId` to that method; UI (FileBrowser) renders virtual
  nodes and already has `FileNodeDto.IsVirtual`/`VirtualSourceKind`/`LinkTarget` and read-only handling.

### 3.2 Module isolation (MANDATORY constraint)

Modules MUST NOT call each other. Aggregation is a **core capability** (no user-owned domain of its own —
like search). Two clean options, decided at implementation:

- **(A) Core aggregator service** in `DotNetCloud.Core.Server` that fetches "shared with me" from each module
  using the same channels the UI already uses (in-process module UI services for Notes/Photos/Files;
  process-isolated HTTP/gRPC module API clients for Contacts/Calendar), then feeds the Files virtual listing.
- **(B) Per-module "shared with me" RPCs** exposed to core (like `CoreCapabilities`), called by the aggregator.

Prefer (A) first (no new cross-process protocol): Files UI/listing runs in-process in Core.Server where the
module UI services and Contacts/Calendar HTTP clients already live.

### 3.3 "Shared with me" sources per module

| Module   | Source                                        | Existing "shared with me" query                                                           |
| -------- | --------------------------------------------- | ----------------------------------------------------------------------------------------- |
| Files    | `FileShare` (user/team/group/public excluded) | `FileService`/`SharedWithMe` virtual listing                                              |
| Notes    | `NoteShare` (user + team)                     | `NoteService.ListNotesAsync(caller)` → filter `OwnerId != caller` (add a dedicated query) |
| Photos   | `PhotoShare` (photo + album, user + team)     | `PhotoShareService.GetSharedWithMeAsync`                                                  |
| Contacts | `ContactShare`                                | `ContactShareService` list (audit)                                                        |
| Calendar | `CalendarShare` (events/calendars)            | `CalendarShareService` list (audit)                                                       |

### 3.4 Virtual entries & deep links

Represent each shared item as a virtual `FileNodeDto` under `SharedWithMeRootId/<Module>`:

- stable virtual node id per module root: `virtual::swm-<module>` (add to `VirtualMountedNodeRegistry`).
- stable virtual id per shared item: `virtual::swm-<module>::<entityId>` (no DB persistence needed; entries are
  derived at list time — the mounted-admin pattern needs DB rows because they point at real folders; these don't).
- `VirtualSourceKind` = `"SharedWithMe"`, plus a new field carrying `(moduleId, entityId, entityType)` so the UI
  can build the deep link and title/icon.
- `FileNodeDto` additions (additive): `LinkUrl`/`DeepLinkUrl` (or reuse `LinkTarget`) + module id + source id.
- Clicking a virtual item → navigate with `NavigationManager` to the module route:
  - Files → opens file (existing)
  - Notes → `/notes?id={noteId}` (NotesPage already deep-links `NoteId`)
  - Photos → `/photos?album={albumId}` or photo view (audit existing deep-link params)
  - Calendar → `/calendar?eventId=…` / `?calendar=…`
  - Contacts → `/contacts?id={contactId}` (audit)
- Items are `IsReadOnly` in the tree (they are shortcuts; editing happens in the module UI).

### 3.5 Read-only affordance in module lists (Notes done as pilot)

Notes (implemented 2026-09-08):

- `NoteDto.ViewerPermission` (`NoteSharePermission?`, null = owner) computed by `NoteService` for the caller
  (user or team share).
- NotesPage: list badges "Shared"/"Read-only"; detail banner "Shared with you — read only"; Edit/Favorite/Share/
  Delete/Restore hidden/limited by owner + permission; "Shared with me" section now lists only non-owned notes.

---

## 4. Implementation Phases

### Phase 1 — Notes recipient clarity (DONE on `fix/sharing`, 2026-09-08)

- ✓ `NoteDto.ViewerPermission`; `NoteService` computes it (owner → null).
- ✓ NotesPage badges, read-only notice, action gating, "Shared with me" = non-owned notes.
- ✓ Tests (`NoteTeamShareTests`, +5 viewer-permission tests).

### Phase 2 — Files virtual tree: per-module folders + Notes items (pilot) — DONE on `fix/sharing` (2026-09-08)

- ✓ Module-neutral contract in `DotNetCloud.Core/SharedWithMe/` (`SharedWithMeModuleItem`, `SharedWithMeModule`,
  `ISharedWithMeProvider`, `ISharedWithMeModuleRegistry` facade) — Files.Data never references other modules.
- ✓ Module-root + item virtual-id helpers in `VirtualMountedNodeRegistry`
  (`GetSharedWithMeModuleFolderId(moduleId)`, `GetSharedWithMeItemId(moduleId, entityType, entityId)`).
- ✓ `FileService.ListSharedWithMeChildrenAsync`: under `SharedWithMeRootId` returns per-module folders that have
  shared items — the Files folder (wrapping mounted file-share items) plus one folder per registered provider
  (Notes today; Photos/Contacts/Calendar add providers later). Legacy file-share-only layout preserved when no
  registry is present (Files.Host / unit tests).
- ✓ `GetVirtualNodeAsync`/`GetVirtualChildrenAsync` route module-folder ids → folder node + provider items mapped
  to virtual read-only `FileNodeDto` entries (`VirtualSourceKind="SharedWithMeModule"`, `DeepLinkUrl`, `ModuleId`,
  `EntityType`, `SourceEntityId`, stable ids).
- ✓ `FileNodeDto` + `FileNodeViewModel` deep-link/module metadata (`ModuleId`, `EntityType`, `SourceEntityId`,
  `DeepLinkUrl`, `IconName`); FileBrowser opens virtual shared items by `NavigationManager` to the module route
  (e.g. `/apps/notes?noteId=…`), uses module icons, and excludes shortcuts from context-menu/bulk operations.
- ✓ Wire Notes "shared with me": `NotesSharedWithMeProvider` (Core.Server) adapts in-process `NoteService` —
  resolves `INoteService` per DI scope, lists notes visible to the caller, filters `OwnerId != caller` (and
  `!IsDeleted`), maps to deep-link items (`/apps/notes?noteId={id}`). Registered alongside
  `SharedWithMeModuleRegistry` in `Core.Server/Program.cs`.
- ✓ Tests + build: 7 new `FileServiceSharedWithMeModuleTests` (Files 767 ✓), 3 new `NotesSharedWithMeProviderTests`
  (Core.Server 658 ✓), Notes 148 ✓; Files.Data/Files/Notes.Data/Notes/Core.Server build 0 warnings/0 errors.
- ☐ Live E2E verification (sharer + recipient end-to-end through the running UI) — deferred to Phase 5.
  Deep-link verified to use `/apps/notes?noteId=` (Notes module route) rather than the stub `/notes?id=` in this doc.

### Phase 3 — Photos

- ✓ `PhotosSharedWithMeProvider` (Core.Server) adapts in-process `IPhotoShareService.GetSharedWithMeAsync`
  (user + team, unexpired) and resolves titles via `IAlbumService`/`IPhotoService` (album title / photo file name),
  mapping to deep-link items: album → `/apps/photos?albumId={id}`, photo → `/apps/photos?photoId={id}`. Dedupes
  repeated shares, skips unresolvable entities, degrades to empty on module failure. Registered in
  `Core.Server/Program.cs`.
- ✓ Photos album deep-link support: the `/apps/photos` shell page now reads `albumId` and PhotosPage opens the
  shared album (owned or shared read) in the Albums section.
- ✓ Tests: 5 new `PhotosSharedWithMeProviderTests` (Core.Server); Photos 244 ✓.

### Phase 4 — Contacts & Calendar

- ✓ New "shared with me" queries added to both process-isolated modules (user + team, mirroring Notes/Photos):
  `ContactShareService.ListSharedWithMeAsync` and `CalendarShareService.ListSharedWithMeAsync` (optional team
  directory; contacts also filter unexpired/non-deleted), exposed as `GET api/v1/contacts/shared-with-me` and
  `GET api/v1/calendars/shared-with-me`, surfaced on the module HTTP clients (`IContactsApiClient`/
  `ICalendarApiClient` → `ListSharedWithMeAsync`).
- ✓ `ContactsSharedWithMeProvider` (Core.Server) resolves the module HTTP client and maps contact items
  (`/apps/contacts?contactId={id}`); `CalendarSharedWithMeProvider` maps calendar items
  (`/apps/calendar?calendarId={id}`). Self-created/owned items excluded; graceful empty on module failure. Both
  registered in `Core.Server/Program.cs`.
- ✓ Calendar deep-link support: CalendarPage opens the shared calendar via `?calendarId=` (mirrors `?eventId=`).
- ✓ Tests: 4 `ContactsSharedWithMeProviderTests` + 5 `CalendarSharedWithMeProviderTests` (Core.Server);
  5 `ContactShareServiceSharedWithMeTests` (Contacts 147 → 152 ✓) + 4 `CalendarShareServiceSharedWithMeTests`
  (Calendar 196 → 200 ✓).

### Phase 5 — Docs, tracking, verification — DONE on `fix/sharing` (2026-09-08)

- ✓ Help/UX text updated (Photos/Contacts/Calendar help content); `IMPLEMENTATION_CHECKLIST.md` /
  `MASTER_PROJECT_PLAN.md` updated; full `DotNetCloud.CI.slnf` build 0 warnings/0 errors.
- ✓ Live E2E passed on mint22 deploy (`sudo ./scripts/deploy.sh --force --verify`, all 15 targets, health 200):
  per-module folders (Files/Notes/Photos/Contacts/Calendar) appear under `_DotNetCloud/SharedWithMe` and
  deep-link to `/apps/notes?noteId=`, `/apps/photos?photoId=|albumId=`, `/apps/contacts?contactId=`,
  `/apps/calendar?calendarId=`; read-only recipient badges + action gating verified; real file shares still
  listed under Files (regression ✓); share-dialog + team-share notification fan-out regression ✓.
- ✓ Deployed to mint22 and committed to `fix/sharing`.

---

## 5. Risks / Notes

- Aggregation lives in core (module-isolation boundary) — no `Files.Data → Notes.Data` references.
- Virtual items are ephemeral (computed at list time), so they must not be persisted to `MountedNodeEntry`.
- Team shares flatten into the same module folder (no per-team folders this pass).
- `NoteService` "shared with me" query must reuse the team-membership predicates added in the share-dialog plan.
- Deep-link contract per module must be audited (NotesPage `NoteId` exists; Photos/Calendar/Contacts params to
  confirm).
