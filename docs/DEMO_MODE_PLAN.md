# Demo Mode — Restricted Trial Accounts

**Status:** Draft | **Date:** May 7, 2026

## TL;DR

Add a `DemoModeEnabled` system setting that, when active, makes self-registered accounts trial accounts: 750 MB storage, no email sending, auto-deleted after 5 days, with a days-remaining banner on every page. Requires building user-deletion cascade infrastructure as a prerequisite.

## Key Design Decisions

- **Only self-registered users** become demo users; admin-created accounts are exempt
- **Disabling Demo Mode** does NOT upgrade existing demo users — they stay restricted
- **Mutual exclusion:** Demo Mode cannot be enabled if Closed System is enabled, and vice versa
- **Cascade deletion** built first as a prerequisite (proper `UserDeletedEvent` + Files cleanup)
- **Prominent, non-dismissible banner** on every page showing days remaining

---

## Phases

### Phase 0: User Deletion Cascade Infrastructure (PREREQUISITE)

*All subsequent phases depend on this. Must be completed first.*

#### Step 0.1 — Create `UserDeletedEvent`
- New event class: `UserDeletedEvent` with `UserId` (Guid), `DeletedAt` (DateTime)
- Place in `src/Core/DotNetCloud.Core/Events/UserDeletedEvent.cs`
- Follows existing event patterns (`EmailSentEvent`, `QuotaWarningEvent`, etc.)

#### Step 0.2 — Publish event from `UserManagementService.DeleteUserAsync`
- Inject `IEventBus` into `UserManagementService`
- Publish `UserDeletedEvent` after successful `UserManager.DeleteAsync()`
- File: `src/Core/DotNetCloud.Core.Auth/Services/UserManagementService.cs`

#### Step 0.3 — Files module: subscribe to `UserDeletedEvent` and clean up
- Create `UserDeletedEventSubscriber` in `src/Modules/Files/DotNetCloud.Modules.Files/Services/`
- Implements `IEventHandler<UserDeletedEvent>`
- On handle, clean up for the deleted user:
  - Delete `FileQuota` record
  - Delete `SyncDevice` records
  - Delete `UserSyncCounter` records
  - Delete `ChunkedUploadSession` records
  - Handle `FileNode` records (delete user-owned files; for demo users this means all files)
  - Clean up physical files on disk via `IFileStorageEngine` (but be aware of content-addressed chunk sharing — only delete chunks no longer referenced by any FileNode)
- Register subscriber in Files module DI
- Reference pattern: `TrashCleanupService` for file deletion, `QuotaService` for quota management, `LocalFileStorageEngine` for disk I/O

#### Step 0.4 — Handle Restrict FK constraints
- `TeamMember`, `OrganizationMember`, `GroupMember` have `OnDelete(DeleteBehavior.Restrict)` — these block user deletion
- For demo users this is unlikely to be an issue (new accounts won't be in teams/orgs)
- For now: catch and log if deletion fails due to FK constraints; demo cleanup will retry next cycle
- Future enhancement: cascade or nullify these memberships on user deletion

---

### Phase 1: Data Model & System Setting

*Depends on Phase 0 being complete (builds on same entity/config files).*

#### Step 1.1 — Add `IsDemoUser` to `ApplicationUser`
- Add `public bool IsDemoUser { get; set; } = false;` property
- File: `src/Core/DotNetCloud.Core.Data/Entities/Identity/ApplicationUser.cs`

#### Step 1.2 — Update EF configuration for `ApplicationUser`
- Add `.IsRequired().HasDefaultValue(false)` for `IsDemoUser`
- File: `src/Core/DotNetCloud.Core.Data/Configuration/Identity/ApplicationUserConfiguration.cs`

#### Step 1.3 — Add `DemoModeEnabled` to `SystemSettingKeys`
- New constant: `DemoModeEnabled = "DemoModeEnabled"` (module: `"dotnetcloud.core"`, default: `"false"`)
- File: `src/Core/DotNetCloud.Core/Constants/SystemSettingKeys.cs`

#### Step 1.4 — Scaffold EF migration
- Migration adds `IsDemoUser` column to `AspNetUsers` table
- Follow repo conventions for migration cleanup
- Command: `dotnet ef migrations add AddIsDemoUser --project src/Core/DotNetCloud.Core.Data --context CoreDbContext`

---

### Phase 2: Registration Gate — Demo User Creation

*Depends on Phase 1.*

#### Step 2.1 — Modify `AuthService.RegisterAsync` for demo mode
- After existing Closed System check, add Demo Mode check:
  - Query `DemoModeEnabled` setting
  - If `"true"` AND caller is NOT admin (self-registration): set `user.IsDemoUser = true`
  - If `"true"` AND caller IS admin: do NOT set `IsDemoUser` (admin-created accounts exempt)
- Add mutual exclusion validation: if BOTH `ClosedSystemEnabled` and `DemoModeEnabled` are `"true"`, throw (shouldn't happen due to admin validation, but defense in depth)
- File: `src/Core/DotNetCloud.Core.Auth/Services/AuthService.cs`

#### Step 2.2 — Set 750 MB quota for demo users
- After `UserManager.CreateAsync` succeeds, if `user.IsDemoUser`:
  - Resolve `IQuotaService` (may need to add as dependency or use service locator)
  - Call `SetQuotaAsync(user.Id, 750 * 1024 * 1024)` — 786,432,000 bytes
- Note: `IQuotaService` lives in Files module. May need to access via gRPC or event-driven approach. Alternatives:
  - Publish `UserCreatedEvent` with `IsDemoUser` flag; Files module subscribes and sets quota
  - Direct DI reference if Files module is referenced (check project dependencies)
- File: `src/Core/DotNetCloud.Core.Auth/Services/AuthService.cs`

#### Step 2.3 — Update registration UI for demo mode awareness
- `Register.razor` already checks `ClosedSystemEnabled`
- Add demo mode notice: when registering during demo mode, show informational text:
  "🎉 You're creating a free 5-day trial account with 750 MB storage. Email sending is not available in trial mode."
- File: `src/UI/DotNetCloud.UI.Web/Components/Pages/Auth/Register.razor`

---

### Phase 3: Email Sending Block

*Depends on Phase 1 (needs `IsDemoUser` flag). Can run in parallel with Phase 2.*

#### Step 3.1 — Block email sending for demo users
- In `EmailSendService.SendAsync()`, after account validation but before provider resolution:
  - Look up the user by `caller.UserId`
  - If `user.IsDemoUser`: throw `ValidationException` with code `EMAIL_SENDING_DISABLED_DEMO` and message "Email sending is not available in demo mode. Upgrade to a full account to send emails."
- Need access to `UserManager<ApplicationUser>` or a user lookup service in Email module
- File: `src/Modules/Email/DotNetCloud.Modules.Email.Data/Services/EmailSendService.cs`

#### Step 3.2 — Update email compose UI (optional polish)
- If user is demo, show a non-blocking notice in the compose form: "Email sending is disabled in demo mode"
- Or: disable the Send button with a tooltip explanation
- File: `src/Modules/Email/DotNetCloud.Modules.Email/UI/` (EmailPage.razor or compose component)

---

### Phase 4: Auto-Delete Background Service

*Depends on Phase 0 (cascade deletion) and Phase 1 (IsDemoUser flag).*

#### Step 4.1 — Create `DemoAccountCleanupService`
- New file: `src/Core/DotNetCloud.Core.Server/Services/DemoAccountCleanupService.cs`
- Inherits `BackgroundService`
- Pattern follows `BackupHostedService`:
  - Polling loop: runs every 1 hour (checking for expired accounts)
  - Creates scoped DI container per iteration
  - Queries: `UserManager.Users.Where(u => u.IsDemoUser && u.CreatedAt < DateTime.UtcNow.AddDays(-5))`
  - For each expired user: calls `UserManagementService.DeleteUserAsync(userId)`
  - Logs count of deleted users
  - Uses `IBackgroundServiceTracker.RecordRun()` for metrics
- On first run, also schedule an immediate check (don't wait 1 hour)

#### Step 4.2 — Register in DI
- Add `services.AddHostedService<DemoAccountCleanupService>()` in server Program.cs
- Wrap in demo mode check? No — always register, the service itself checks if there's work to do. Keeps DI simple.
- File: `src/Core/DotNetCloud.Core.Server/Program.cs`

---

### Phase 5: UI — Demo Banner on Every Page

*Depends on Phase 1 (IsDemoUser flag). Can run in parallel with Phases 3-4.*

#### Step 5.1 — Expose demo status in `UserDto`
- Add `IsDemoUser` (bool) and `DemoExpiresAt` (DateTime?) to `UserDto`
- `DemoExpiresAt` is computed: `CreatedAt.AddDays(5)` if `IsDemoUser`, else null
- Update `UserManagementService.GetUserAsync` or mapping logic to populate these fields
- File: `src/Core/DotNetCloud.Core/DTOs/UserDtos.cs`

#### Step 5.2 — Create `DemoBanner.razor` component
- New file: `src/UI/DotNetCloud.UI.Web/Components/Shared/DemoBanner.razor`
- Injects `AuthenticationStateProvider` and user profile API client
- On initialization: extracts user ID from claims, calls `GET /api/v1/core/users/{id}` to get `UserDto`
- If `IsDemoUser`: renders prominent, non-dismissible banner
- Calculates days remaining: `Math.Max(0, (CreatedAt.AddDays(5) - DateTime.UtcNow).Days)`
- Visual design:
  - Prominent banner at top of content area
  - Warning/amber styling using existing `.alert-warning` CSS class
  - Shows: "⏳ Demo Account — X days remaining. Your account and all data will be deleted on {expiry date}. Upgrade to keep your data."
  - When 1 day remaining: switch to `.alert-danger` styling
  - When 0 days: "Your demo account has expired and will be deleted soon."

#### Step 5.3 — Integrate banner into `MainLayout.razor`
- Add `<DemoBanner />` component between the topbar and `<main>` content area
- This ensures it's visible on ALL pages, not just home
- File: `src/UI/DotNetCloud.UI.Web/Components/Layout/MainLayout.razor`

#### Step 5.4 — Show storage usage on home page (bonus)
- Fetch user's `FileQuota` via `GET /api/v1/files/quota`
- Add a dashboard card on `Home.razor` showing: used / 750 MB with a progress bar
- File: `src/UI/DotNetCloud.UI.Web/Components/Pages/Home.razor`

---

### Phase 6: Admin Settings Validation

*Depends on Phase 1. Can run in parallel with Phases 3-5.*

#### Step 6.1 — Mutual exclusion validation on settings upsert
- In `AdminSettingsService.UpsertSettingAsync`, or in a dedicated validation layer:
  - If setting `DemoModeEnabled` to `"true"`: check that `ClosedSystemEnabled` is NOT `"true"`. If it is, throw `ValidationException` ("Cannot enable Demo Mode while Closed System mode is active.")
  - If setting `ClosedSystemEnabled` to `"true"`: check that `DemoModeEnabled` is NOT `"true"`. If it is, throw `ValidationException` ("Cannot enable Closed System mode while Demo Mode is active.")
- File: `src/Core/DotNetCloud.Core.Auth/Services/AdminSettingsService.cs`

#### Step 6.2 — Admin settings UI awareness
- The existing `Settings.razor` admin page handles generic CRUD — no changes needed
- Optional: add a dedicated Demo Mode card/toggle for convenience (deferrable)

---

## Relevant Files

| File | Action |
|------|--------|
| `src/Core/DotNetCloud.Core/Events/UserDeletedEvent.cs` | **Create** — new event class |
| `src/Core/DotNetCloud.Core/Constants/SystemSettingKeys.cs` | **Modify** — add `DemoModeEnabled` constant |
| `src/Core/DotNetCloud.Core/DTOs/UserDtos.cs` | **Modify** — add `IsDemoUser`, `DemoExpiresAt` |
| `src/Core/DotNetCloud.Core.Data/Entities/Identity/ApplicationUser.cs` | **Modify** — add `IsDemoUser` |
| `src/Core/DotNetCloud.Core.Data/Configuration/Identity/ApplicationUserConfiguration.cs` | **Modify** — configure `IsDemoUser` |
| `src/Core/DotNetCloud.Core.Auth/Services/UserManagementService.cs` | **Modify** — publish `UserDeletedEvent` |
| `src/Core/DotNetCloud.Core.Auth/Services/AuthService.cs` | **Modify** — set `IsDemoUser` on self-registration |
| `src/Core/DotNetCloud.Core.Auth/Services/AdminSettingsService.cs` | **Modify** — mutual exclusion validation |
| `src/Core/DotNetCloud.Core.Server/Program.cs` | **Modify** — register `DemoAccountCleanupService` |
| `src/Core/DotNetCloud.Core.Server/Services/DemoAccountCleanupService.cs` | **Create** — background cleanup service |
| `src/Modules/Files/DotNetCloud.Modules.Files/Services/UserDeletedEventSubscriber.cs` | **Create** — cleanup subscriber |
| `src/Modules/Email/DotNetCloud.Modules.Email.Data/Services/EmailSendService.cs` | **Modify** — block sends for demo users |
| `src/UI/DotNetCloud.UI.Web/Components/Shared/DemoBanner.razor` | **Create** — demo banner component |
| `src/UI/DotNetCloud.UI.Web/Components/Layout/MainLayout.razor` | **Modify** — integrate banner |
| `src/UI/DotNetCloud.UI.Web/Components/Pages/Home.razor` | **Modify** — storage display (optional) |
| `src/UI/DotNetCloud.UI.Web/Components/Pages/Auth/Register.razor` | **Modify** — demo mode notice |

---

## Verification

1. **Phase 0 verification:** Delete a test user → verify `UserDeletedEvent` is published → verify Files module cleans up FileQuota, SyncDevices, FileNodes, physical files
2. **Phase 1 verification:** Migration applies cleanly → `IsDemoUser` column exists with default `false`
3. **Phase 2 verification:** Enable Demo Mode → register a new user → verify `IsDemoUser=true` → verify quota is 750MB → admin-created user is NOT demo
4. **Phase 3 verification:** Demo user tries to send email → gets clear error message → email module UI still visible and navigable
5. **Phase 4 verification:** Create demo user with `CreatedAt` set to 6 days ago → run cleanup service → user and all their data are deleted
6. **Phase 5 verification:** Demo user logs in → sees prominent amber banner with correct days remaining → 1 day left shows red → non-demo user sees no banner
7. **Phase 6 verification:** Try to enable Demo Mode when Closed System is on → rejected with clear error → vice versa
8. **Integration:** `dotnet build DotNetCloud.CI.slnf` succeeds → `dotnet test DotNetCloud.CI.slnf` passes

---

## Deliberate Exclusions

- **No "upgrade from demo" path** — users cannot convert a demo account to a full account within the trial period (can be added later)
- **No email notification before deletion** — users aren't emailed "your account expires in X days" (the banner serves this purpose)
- **No per-module demo restrictions beyond email** — other modules (Files, Calendar, Chat) remain fully functional within the 750MB constraint
- **No admin UI dedicated toggle card** — the generic Settings page already supports toggling boolean flags
- **No organization/team membership cleanup** in Phase 0.4 — handled as log-and-retry; demo users won't realistically be in orgs/teams
