# Plan: Unify Share Dialogs (Files-style) + Team Sharing + Team-Share Notifications

> **Status:** Draft — pending implementation
> **Branch:** `fix/sharing`
> **Owner:** Core/Web UI (module UI + core capability + module backends)
> **Scope:** Files, Notes, Contacts, Calendar, Photos modules + `DotNetCloud.UI.Shared` + core capability surface
> **Last updated:** 2026-09-08

---

## 1. Problem Statement

Sharing from Notes (and Contacts, Calendar, Photos) asks the user to type a raw **User ID (GUID)** and, once shared, the UI shows a truncated GUID instead of the user's **display name**:

- Notes — `src/Modules/Notes/DotNetCloud.Modules.Notes/UI/NotesPage.razor` (~L203–232)
  `User: @share.SharedWithUserId.ToString("N")[..8]…`, input `placeholder="User ID"`.
- Contacts — `src/Modules/Contacts/DotNetCloud.Modules.Contacts/UI/ContactsPage.razor` (~L169–200) — same pattern.
- Calendar — `src/Modules/Calendar/DotNetCloud.Modules.Calendar/UI/CalendarPage.razor` (~L115–140) — same pattern.
- Photos — `src/Modules/Photos/DotNetCloud.Modules.Photos/UI/PhotosPage.razor` (~L695–713) — `"Share with (User ID)"` modal.

The **Files** module already has the correct UX — a type-ahead search that shows **display names**, a permission dropdown, and an existing-shares list:

- `src/Modules/Files/DotNetCloud.Modules.Files/UI/ShareDialog.razor` + `ShareDialog.razor.cs`
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/BulkShareDialog.razor` + `BulkShareDialog.razor.cs`
- Share-dialog CSS: `/* --- Share Dialog --- */` block in `src/UI/DotNetCloud.UI.Web/wwwroot/css/app.css` (~L3521–3950)

**Goal:** all share dialogs/panels across modules **look and work the same as Files'**, and — per user decision — add real **team sharing** across all five modules (Files/Contacts/Calendar already persist team shares but never expose or enforce them; Notes/Photos need backend support), **team-share notifications**, and a **short-lived membership cache**.

---

## 2. Decisions (confirmed with user)

- ☐ **Shared reusable component**: Extract Files' share dialog into `DncShareDialog` in `DotNetCloud.UI.Shared`; **Files migrates to it too** (single source of truth).
- ☐ **Modal dialog everywhere** (Notes/Contacts/Calendar lose inline panels) — identical look/behavior to Files.
- ☐ **Public link section is Files-only** (`EnablePublicLink`); hidden in other modules.
- ☐ **Team shares on all five modules** (Files, Notes, Contacts, Calendar, Photos).
- ☐ **Team picker source**: users platform-wide + teams the **current user belongs to** (`ITeamDirectory.GetTeamsForUserAsync`).
- ☐ **Team-share notifications required** — bell notification fans out to every team member except the sharer.
- ☐ **Short-lived membership cache** — module-host team lookups cached ~15–30 s.
- ☐ **Revoke-only for non-Files modules** — no inline permission editing for now (Files keeps it).
- ☐ **Files bulk share** supported through `DncShareDialog` bulk mode.

Out of scope (this pass):

- ☐ Group shares in the picker/creation (display-name resolution only where groups already exist).
- ☐ Inline permission editing endpoints for Notes/Contacts/Calendar/Photos (future work).

---

## 3. Verified Architecture & Facts

### 3.1 Module UI hosting

- Module UIs (`FileBrowser`, `NotesPage`, …) are Blazor components loaded into the **Core.Server shell** process via `ModulePageHost` + `ModuleUiRegistry` (see `src/Core/DotNetCloud.Core.Server/Initialization/ModuleUiRegistrationHostedService.cs`).
- Shell DI registers the capability interfaces a page needs:
  `IUserDirectory`, `IGroupDirectory`, `ITeamDirectory`, `IUserManagementService`, `AuthenticationStateProvider`, etc.
  (see `src/Core/DotNetCloud.Core.Auth/Extensions/AuthServiceExtensions.cs` ~L298–315).
- Module UIs talk to their **own module host process** over HTTP/gRPC via module API clients registered in `src/Core/DotNetCloud.Core.Server/Program.cs` (e.g. `INotesApiClient` → `NotesApiClient`).
- Every module UI project references `DotNetCloud.UI.Shared` (verified in every `src/Modules/*/DotNetCloud.Modules.*/*.csproj`); `DotNetCloud.UI.Shared` references `DotNetCloud.Core` and `Microsoft.AspNetCore.Components.Web`.

### 3.2 User/team/group lookup (the building blocks)

- `IUserDirectory` (Public tier, `src/Core/DotNetCloud.Core/Capabilities/IUserDirectory.cs`):
  - `SearchUsersAsync(term, max)` → `UserSearchResult(Id, DisplayName, Email)`
  - `GetDisplayNamesAsync(IEnumerable<Guid>)`
- `ITeamDirectory` (Restricted tier, same folder): `GetTeamsForUserAsync(userId)`, `GetTeamAsync(teamId)` (returns `TeamInfo` incl. `Name`), `GetTeamMembersAsync(teamId)`.
- Current-user id pattern used by Chat/Music/Tracks/Photos module UIs:
  claim `System.Security.Claims.ClaimTypes.NameIdentifier`, else `sub`.

### 3.3 Cross-process capability surface (module hosts → core)

- `src/Core/DotNetCloud.Core.Grpc/Protos/module_capabilities.proto` (`CoreCapabilities` service).
  Currently exposes `GetUser`, `SearchUsers`, `GetCurrentUser`, `GetGroup`, notifications, events, settings, etc.
  **There is no team RPC**, and `GetGroupsForUser`/`GetTeamsForUser` do not exist.
- Server implementation: `CoreCapabilitiesServiceImpl` in `src/Core/DotNetCloud.Core.Server/Grpc/Services/GrpcHealthServiceImpl.cs`.
- Module-host directory-client patterns to copy:
  - Chat/Tracks hosts: `GrpcUserDirectoryService : IUserDirectory`
  - Files host: `GrpcGroupDirectory : IGroupDirectory` (`src/Modules/Files/DotNetCloud.Modules.Files.Data/Services/GrpcGroupDirectory.cs`) — note its membership methods are **stubbed empty**.
- Files' `CapabilityShareAccessMembershipResolver` (`.../Files.Data/Services/CapabilityShareAccessMembershipResolver.cs`) uses nullable `ITeamDirectory`/`IGroupDirectory`. In the module host neither is registered → **it resolves nothing today** → team/group access is currently unenforced.

### 3.4 Share data model per module

| Module   | Store                                                        | Team column?          | Share events emitted                                                                      |
| -------- | ------------------------------------------------------------ | --------------------- | ----------------------------------------------------------------------------------------- |
| Files    | `FileShare` (`ShareType`, `SharedWithUserId/TeamId/GroupId`) | Yes                   | `FileSharedEvent` (has `ShareType`, `SharedWithUserId`, `SharedByUserId`; **no team id**) |
| Contacts | `ContactShare` (`SharedWithUserId/TeamId`)                   | Yes (proto `team_id`) | `ResourceSharedEvent`                                                                     |
| Calendar | `CalendarShare` (`SharedWithUserId/TeamId`)                  | Yes (proto `team_id`) | `ResourceSharedEvent`                                                                     |
| Notes    | `NoteShare` (`SharedWithUserId` only)                        | **No**                | `ResourceSharedEvent`                                                                     |
| Photos   | `PhotoShare`/albums (`SharedWithUserId` only)                | **No**                | `AlbumSharedEvent` (not subscribed by notification producer)                              |

### 3.5 Notification pipeline (core)

- Modules publish share events over the module→core event bus.
- `src/Core/DotNetCloud.Core.Server/Services/NotificationEventSubscriber.cs` subscribes `NotificationProducer` to 8 events (incl. `FileSharedEvent`, `ResourceSharedEvent`).
- `src/Core/DotNetCloud.Core.Server/Services/NotificationProducer.cs` builds one `NotificationDto` per recipient → `INotificationService.SendAsync` → raises `NotificationCreatedEvent` → `NotificationFanOutDispatcher` (realtime/push).
- Files' `FileSharedEvent` handler **skips** when `SharedWithUserId is null` (so team/public-link shares never notify today).
- `ResourceSharedEvent.SharedWithUserId` is currently `required Guid` (user-only).
- Photos share events are **not** subscribed → Photos sends no bell notification today.

### 3.6 Notes read-access path (module host)

`src/Modules/Notes/DotNetCloud.Modules.Notes.Data/Services/NoteService.cs` (~L125/132/156/316/343):
a note is visible when `OwnerId == caller.UserId || Shares.Any(s => s.SharedWithUserId == caller.UserId)`.
Team sharing requires also matching `Shares.Any(s => s.SharedWithTeamId ∈ callerTeams)`.

---

## 4. Implementation Phases

### Phase 0 — Core capability: caller team membership + short-lived cache (enabler — do first)

- ✓ Extend `module_capabilities.proto` with `GetTeamsForUser(GetTeamsForUserRequest) → (GetTeamsForUserResponse)` and a `TeamInfoMessage` (id, organization_id, name, member_count).
- ✓ Implement the RPC in `CoreCapabilitiesServiceImpl` (`GrpcHealthServiceImpl.cs`) via scoped `ITeamDirectory` (mirror `SearchUsers`; log + graceful error handling).
- ✓ Add a shared `GrpcTeamDirectory : ITeamDirectory` in `DotNetCloud.Core.Grpc` (referenced by all module hosts) that supports `GetTeamsForUserAsync` + `GetTeamAsync` over the `CoreCapabilities` client (pattern: Files `GrpcGroupDirectory`, Chat `GrpcUserDirectoryService`). Gracefully handle `Unimplemented`/`Unavailable`/timeouts like `GrpcGroupDirectory`.
- ✓ Add `CachedTeamDirectory` decorator (IMemoryCache, short TTL ~15–30 s) caching:
  - `GetTeamsForUserAsync(userId)` keyed by userId,
  - `GetTeamAsync(teamId)` keyed by teamId.
    Register as the module-host `ITeamDirectory`, wrapping `GrpcTeamDirectory`.
- ✓ Register `ITeamDirectory` (cached gRPC) in module-host DI for **Files, Notes, Photos, Contacts, Calendar** hosts (wherever share/read services run).
- ✓ Verify Files' `CapabilityShareAccessMembershipResolver` now resolves teams.
- ✓ Tests: RPC unit/integration; resolver returns teams; cache returns cached values within TTL and refreshes after.

**Risks / notes:**

- ⚠️ Proto change requires rebuild + redeploy of Core.Server **and** all module hosts together; keep the graceful `Unimplemented` handling already used by `GrpcGroupDirectory`.
- Membership fetch adds a core RPC per access query → the cache addresses latency; choose TTL ~15–30 s (membership changes are infrequent; no cross-process coherence guarantee needed).

### Phase 1 — Shared UI building blocks (`DotNetCloud.UI.Shared`)

- ✓ Add `src/UI/DotNetCloud.UI.Shared/Components/Dialogs/DncShareDialog.razor` + `.razor.cs` — generalize Files' `ShareDialog`.
- ✓ Public models in the same namespace:
  - `ShareRecipientOption(Id, DisplayName, Type, SecondaryText)`
  - `ShareEntry(ShareId, RecipientName, RecipientType, Permission, ExpiresAt?, Note?)`
  - `SharePermissionOption(Value, Label)`
  - Create / permission-changed / removed event-args types.
- ✓ Parameters: `Title`/`ItemName`, `ExistingShares` + `IsLoadingShares`, `OnShareCreated`, `OnShareRemoved`, nullable `OnPermissionChanged` (hidden when null), `EnablePublicLink` + public-link events (toggle / password / max downloads / expiry — Files only), `PermissionOptions`, `IncludeTeams`, `BulkItemCount` (bulk mode hides existing-shares + public-link; button "Share with N items").
- ✓ Add `IShareRecipientSearchService` (+ impl) in `DotNetCloud.UI.Shared`:
  - users via `IUserDirectory.SearchUsersAsync`,
  - teams via `ITeamDirectory.GetTeamsForUserAsync(currentUser)` filtered by typed text,
  - current user from `AuthenticationStateProvider`.
    Expose a registration extension and call it from Core.Server's `Program.cs`. `DncShareDialog` consumes the service (or accepts an `OnSearch` override).
- ✓ Move the `/* --- Share Dialog --- */` CSS block from `app.css` into `src/UI/DotNetCloud.UI.Shared/wwwroot/shared-components.css` (dedupe `.badge-user` rules).
- ✓ Bump the `?v=` cache-buster on the shared-css link in `src/UI/DotNetCloud.UI.Web/Components/App.razor`.

### Phase 2 — Migrate Files (validates Phases 0–1)

- ✓ `src/Modules/Files/DotNetCloud.Modules.Files/UI/FileBrowser.razor` (~L403–412) → render `<DncShareDialog>` for single-item share **and** bulk share (bulk mode).
- ✓ `FileBrowser.razor.cs`:
  - Adapt `HandleShareSearchAsync` (users+teams), `HandleShareCreatedAsync/UpdatedAsync/RemovedAsync`, `HandlePublicLinkToggledAsync` to shared event types.
  - **Fix `ShowShareDialogAsync`** (~L1728) so existing-share `RecipientName` is resolved to display names via `IUserDirectory` (users), `ITeamDirectory` (teams), `IGroupDirectory` (groups) instead of `s.SharedWithUserId?.ToString()`.
- ✓ Delete `ShareDialog.razor(.cs)`, `BulkShareDialog.razor(.cs)` and now-unused local types in `src/Modules/Files/DotNetCloud.Modules.Files/UI/ViewModels.cs` (`ShareViewModel`, `ShareSearchResult`, `ShareCreatedEventArgs`, `ShareUpdatedEventArgs`, bulk args) after confirming no other references.
- ✓ Verify: single share, bulk share, public link, team-share create + team-member access.

### Phase 3 — Notes (backend + UI)

- ✓ Data: add `SharedWithTeamId` (nullable) to `NoteShare` (`src/Modules/Notes/DotNetCloud.Modules.Notes/Models/NoteShare.cs`) + migrations (Postgres & SqlServer under `DotNetCloud.Modules.Notes.Data`).
- ✓ DTO: add `SharedWithTeamId` to `NoteShareDto` (`src/Core/DotNetCloud.Core/DTOs/NoteDtos.cs`).
- ✓ Host API: `NoteShareMessage` proto + `NotesGrpcService` + controller + `INotesApiClient.ShareNoteAsync` accept a team target (user XOR team).
- ✓ `NoteShareService.ShareNoteAsync` (`.../Notes.Data/Services/NoteShareService.cs`): accept user **or** team; dedupe by target; keep list/revoke semantics.
- ✓ Read access (module host, uses Phase 0 membership): extend the visible-note predicates in `NoteService.cs` (L125/132/156/316/343) and "shared with me" to also match team shares (`SharedWithTeamId ∈ callerTeams`).
- ✓ Publish `ResourceSharedEvent` with the team target when a team share is created (see Phase 6).
- ✓ Tests: share/list/read/revoke incl. team membership.
- ✓ UI: `NotesPage.razor` + `.cs` + `.css` → `DncShareDialog` (IncludeTeams); remove inline share panel + dead fields/css; resolve existing-share names via `IUserDirectory`/`ITeamDirectory`.

### Phase 4 — Photos (backend + UI) — DONE ✅

- ✓ Data: add `SharedWithTeamId` (nullable) to `PhotoShare` (photo + album share paths) + migrations (both providers: `AddPhotoShareTeamId`, `AddPhotoShareTeamId_SqlServer`).
- ✓ DTO/controller/`IPhotoShareService` (`SharePhotoAsync`/`ShareAlbumAsync`) accept a team target (user XOR team). (Proto note: Photos `photos_service.proto` has **no** share RPCs — nothing to extend there.)
- ✓ Read access + "shared with me" aggregation include team membership (Phase 0): `PhotoService.GetPhotoAsync`, `AlbumService.GetAlbumAsync`/`GetAlbumPhotosAsync`, `PhotoShareService.GetSharedWithMeAsync`.
- ✓ Publish `AlbumSharedEvent` with team target (`SharedWithTeamId`) — event relaxed to nullable `SharedWithUserId` + added `SharedWithTeamId`; handler skips team shares (direct-user notification only).
- ✓ Tests — `PhotosTeamShareTests` (create/dedupe/no-target/both-targets/revoke/team-member read + "shared with me") + updated `AlbumSharedNotificationHandlerTests`.
- ✓ UI: `PhotosPage.razor`/`.razor.cs` raw-User-ID modal → `DncShareDialog` (users + teams via built-in search; revoke-only; no public link); removed dead share fields; added album share entry; resolves recipient names via `IUserDirectory`/`ITeamDirectory`. Permission surface: ReadOnly ("Read") + Download ("ReadWrite") only.

### Phase 5 — Contacts & Calendar (align + migrate UI)

- ✓ Audit current team-access enforcement in `ContactShareService`/`CalendarShareService` and the read/list paths; enable team-membership read access via the Phase 0 resolver (schemas/protos already carry `team_id`).
- ✓ Publish `ResourceSharedEvent` with the team target on team shares.
- ✓ UI: `ContactsPage.razor` / `CalendarPage.razor` (+ `.cs`/`.css`) → `DncShareDialog` (IncludeTeams); remove inline share panels + dead fields/css; resolve existing-share names.

### Phase 6 — Team-share notifications (core; depends on Phases 3–5 event emits)

- ✓ Events:
  - Add optional `SharedWithTeamId` to `FileSharedEvent` (`src/Core/DotNetCloud.Core/Events/FileSharedEvent.cs`) and `ResourceSharedEvent` (`.../Events/NotificationEvents.cs`); relax `ResourceSharedEvent.SharedWithUserId` to `Guid?` where required.
  - ✓ Ensure Photos' share event(s) carry the team target — `AlbumSharedEvent` (`PhotoEvents.cs`) now has `Guid? SharedWithTeamId` + nullable `SharedWithUserId`; `PhotoShareService.ShareAlbumAsync` publishes it with the team target (done in Phase 4).
- ✓ `NotificationProducer` (`src/Core/DotNetCloud.Core.Server/Services/NotificationProducer.cs`): refactor the `ResourceSharedEvent` and `FileSharedEvent` handlers:
  - user target → existing single-notification path;
  - team target → resolve members **once** via `ITeamDirectory.GetTeamMembersAsync(teamId)`, exclude the sharer, and `SendAsync` one `NotificationDto` per member (title e.g. "`<Entity> shared with your team`").
- ✓ Add Photos' album/photo share event(s) to `NotificationEventSubscriber`'s producer subscription list (Photos currently gets no bell notifications).
- ✓ Unit tests: fan-out to N members, excludes sharer, no-members/no-op, user path unchanged.

### Phase 7 — Docs, tracking, verification

- ✓ Update help/user docs that instruct "enter the user ID": `CalendarHelpContent.razor`, `PhotosHelpContent.razor`, `docs/user/PHOTOS.md`, and any others discovered.
- ☐ Update `docs/IMPLEMENTATION_CHECKLIST.md` and `docs/MASTER_PROJECT_PLAN.md` with ✓/☐ per repo convention (targeted edits).
- ☐ `dotnet build` (warnings-as-errors) and `dotnet test` — all new/updated tests pass.
- ☐ **Live manual verification before any commit** (repo rule: no commit until verified): for each module — share by typing a display name (users **and** teams), confirm the existing-shares list shows names (not GUIDs), confirm a team member can open the shared item **and** receives a bell notification, confirm revoke works; Files regression: single + bulk share + public link (password/expiry/max downloads); notification realtime fan-out.

---

## 5. Verification Checklist (Summary)

- [ ] `dotnet build` clean (TreatWarningsAsErrors)
- [ ] `dotnet test` green
- [ ] Team-membership RPC + resolver + cache tests
- [ ] Notes/Photos team share create/list/read/revoke + team-member visibility tests
- [ ] Notification fan-out tests (team → members, excludes sharer)
- [ ] Manual end-to-end verification per module (see Phase 7) — done **before** commit
- [ ] Docs updated (help content + IMPLEMENTATION_CHECKLIST.md + MASTER_PROJECT_PLAN.md)
- [ ] No unexpected untracked files left before commit (`git status --short`)

---

## 6. Relevant Files (index)

**Core capability / membership**

- `src/Core/DotNetCloud.Core.Grpc/Protos/module_capabilities.proto`
- `src/Core/DotNetCloud.Core.Server/Grpc/Services/GrpcHealthServiceImpl.cs` (CoreCapabilitiesServiceImpl)
- `src/Modules/Files/DotNetCloud.Modules.Files.Data/Services/{GrpcGroupDirectory.cs, CapabilityShareAccessMembershipResolver.cs, FilesServiceRegistration.cs}`
- Capabilities (reused, no change): `src/Core/DotNetCloud.Core/Capabilities/{IUserDirectory,ITeamDirectory,IGroupDirectory}.cs`

**Notifications**

- `src/Core/DotNetCloud.Core.Server/Services/{NotificationProducer.cs, NotificationEventSubscriber.cs, NotificationFanOutDispatcher.cs}`
- `src/Core/DotNetCloud.Core/Events/{FileSharedEvent.cs, NotificationEvents.cs}`

**Shared UI**

- `src/UI/DotNetCloud.UI.Shared/Components/Dialogs/DncShareDialog.razor` (+ `.cs`) — new
- `src/UI/DotNetCloud.UI.Shared/wwwroot/shared-components.css`
- `src/UI/DotNetCloud.UI.Web/wwwroot/css/app.css`
- `src/UI/DotNetCloud.UI.Web/Components/App.razor`

**Module UIs**

- Files: `src/Modules/Files/DotNetCloud.Modules.Files/UI/{FileBrowser.razor(.cs), ShareDialog.razor(.cs), BulkShareDialog.razor(.cs), ViewModels.cs}`
- Notes: `src/Modules/Notes/DotNetCloud.Modules.Notes/UI/{NotesPage.razor(.cs/.css)}` + backend under `.../Models`, `.../Data`, `.../Host`
- Photos: `src/Modules/Photos/DotNetCloud.Modules.Photos/UI/{PhotosPage.razor(.cs)}` + backend under `.../Models`, `.../Data`, `.../Host`
- Contacts: `src/Modules/Contacts/DotNetCloud.Modules.Contacts/UI/{ContactsPage.razor(.cs/.css)}` + backend
- Calendar: `src/Modules/Calendar/DotNetCloud.Modules.Calendar/UI/{CalendarPage.razor(.cs/.css)}` + backend

**Docs to update**

- `docs/IMPLEMENTATION_CHECKLIST.md`
- `docs/MASTER_PROJECT_PLAN.md`
- `src/Modules/Calendar/DotNetCloud.Modules.Calendar/UI/CalendarHelpContent.razor`
- `src/Modules/Photos/DotNetCloud.Modules.Photos/UI/PhotosHelpContent.razor`
- `docs/user/PHOTOS.md`
