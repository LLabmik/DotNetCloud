# Closed System Mode — Implementation Plan

**Date:** May 6, 2026  
**Status:** ☐ Not started  

---

## Overview

Add a "closed system" mode (enabled via a new system setting `ClosedSystemEnabled`) where users cannot self-register. Instead, admins create accounts with an initial password, and users are forced to change their password on first login.

---

## Phases & Steps

### Phase A: Data Model — `PasswordChangeRequired` Flag
*(No dependencies — can start immediately)*

#### Step phase-a.1 — Add `PasswordChangeRequired` property to `ApplicationUser`
**Status:** ☐ not-started  
**Deliverables:**
- ☐ `bool PasswordChangeRequired { get; set; } = false` property on `ApplicationUser`
- ☐ XML doc comment

**File:** `src/Core/DotNetCloud.Core.Data/Entities/Identity/ApplicationUser.cs`

#### Step phase-a.2 — Update EF configuration
**Status:** ☐ not-started  
**Deliverables:**
- ☐ `.IsRequired().HasDefaultValue(false)` for new property

**File:** `src/Core/DotNetCloud.Core.Data/Configuration/Identity/ApplicationUserConfiguration.cs`

#### Step phase-a.3 — Scaffold EF migration
**Status:** ☐ not-started  
**Deliverables:**
- ☐ Migration `AddPasswordChangeRequired` adds column to `AspNetUsers` table
- ☐ Migration cleaned up per repo conventions

**Command:** `dotnet ef migrations add AddPasswordChangeRequired --project src/Core/DotNetCloud.Core.Data --context CoreDbContext`

---

### Phase B: Closed System Setting
*(No dependencies — can run parallel with Phase A)*

#### Step phase-b.1 — Define setting constants
**Status:** ☐ not-started  
**Deliverables:**
- ☐ `SystemSettingKeys.ClosedSystemEnabled` constant (`"ClosedSystemEnabled"`)
- ☐ `SystemSettingKeys.ClosedSystemModule` constant (`"dotnetcloud.core"`)
- ☐ Default value `"false"`

**File:** `src/Core/DotNetCloud.Core/Constants/SystemSettingKeys.cs` (new or add to existing)

#### Step phase-b.2 — Verify admin can toggle via existing Settings UI
**Status:** ☐ not-started  
**Deliverables:**
- ☐ Confirm `/admin/settings` page supports CRUD for `dotnetcloud.core` / `ClosedSystemEnabled`
- ☐ Add dedicated Security card if generic UI is insufficient (stretch goal)

**File:** `src/UI/DotNetCloud.UI.Web.Client/Pages/Admin/Settings.razor`

---

### Phase C: Registration Gate — Block Self-Registration in Closed Mode
*(Depends on Phase B)*

#### Step phase-c.1 — Add closed-system check to `AuthService.RegisterAsync`
**Status:** ☐ not-started  
**Deliverables:**
- ☐ Inject `IAdminSettingsService` into `AuthService`
- ☐ Query `ClosedSystemEnabled` setting before creating user
- ☐ If `"true"` and `!caller.HasRole("Administrator")`: throw `InvalidOperationException` with clear message
- ☐ If `"true"` and caller IS admin: set `user.PasswordChangeRequired = true`
- ☐ If `"false"` or setting missing: normal flow (no change)

**File:** `src/Core/DotNetCloud.Core.Auth/Services/AuthService.cs`

#### Step phase-c.2 — Update `AuthController.RegisterAsync` for proper HTTP response
**Status:** ☐ not-started  
**Deliverables:**
- ☐ Catch `InvalidOperationException` from service, return `403 Forbidden` with message
- ☐ Alternatively, pre-check before calling service

**File:** `src/Core/DotNetCloud.Core.Server/Controllers/AuthController.cs`

#### Step phase-c.3 — Update self-registration UI (`Register.razor`)
**Status:** ☐ not-started  
**Deliverables:**
- ☐ On page load, check `ClosedSystemEnabled` via API client
- ☐ If enabled: hide form, show message: *"Self-registration is currently disabled. Please contact your system administrator to request an account."*
- ☐ If disabled: show normal registration form

**File:** `src/UI/DotNetCloud.UI.Web/Components/Pages/Auth/Register.razor`

---

### Phase D: Password Change on First Login
*(Depends on Phase A)*

#### Step phase-d.1 — Create `ChangePassword.razor` page
**Status:** ☐ not-started  
**Deliverables:**
- ☐ Route: `/auth/change-password`
- ☐ Accepts `returnUrl` query parameter
- ☐ Fields: Current Password, New Password, Confirm New Password
- ☐ Validation: current password correct, new password meets Identity requirements, passwords match
- ☐ On success: clear `PasswordChangeRequired`, redirect to `returnUrl` or `/`
- ☐ Heading: *"You must change your password before continuing"*
- ☐ Authorized-only (enforced by middleware + `[Authorize]` attribute)

**File:** `src/UI/DotNetCloud.UI.Web/Components/Pages/Auth/ChangePassword.razor` (new)

#### Step phase-d.2 — Create form-post endpoint for password change
**Status:** ☐ not-started  
**Deliverables:**
- ☐ `POST /auth/session/change-password` action in `AuthSessionController`
- ☐ Parameters: `currentPassword`, `newPassword`, `returnUrl`
- ☐ Validates current password, changes via `UserManager.ChangePasswordAsync`
- ☐ Sets `PasswordChangeRequired = false` on success
- ☐ Redirects to `returnUrl` or `/`

**File:** `src/Core/DotNetCloud.Core.Server/Controllers/AuthSessionController.cs`

#### Step phase-d.3 — Add API endpoint for password change
**Status:** ☐ not-started  
**Deliverables:**
- ☐ `POST /api/v1/core/auth/change-password` in `AuthController`
- ☐ DTO: `ChangePasswordRequest { CurrentPassword, NewPassword }`
- ☐ Calls `IAuthService.ChangePasswordAsync` or uses `UserManager` directly

**File:** `src/Core/DotNetCloud.Core.Server/Controllers/AuthController.cs`  
**File:** `src/Core/DotNetCloud.Core/DTOs/AuthDtos.cs`

#### Step phase-d.4 — Modify session login flow to redirect when `PasswordChangeRequired`
**Status:** ☐ not-started  
**Deliverables:**
- ☐ In `AuthSessionController.LoginAsync`, after successful `PasswordSignInAsync`
- ☐ Check `PasswordChangeRequired` flag
- ☐ If `true`: redirect to `/auth/change-password?returnUrl=...` instead of normal target
- ☐ Protocol: change-password page preserves original `returnUrl` and redirects there after success

**File:** `src/Core/DotNetCloud.Core.Server/Controllers/AuthSessionController.cs`

#### Step phase-d.5 — Modify API login flow (`AuthService.LoginAsync`)
**Status:** ☐ not-started  
**Deliverables:**
- ☐ After successful authentication, before returning `LoginResponse`
- ☐ If `PasswordChangeRequired == true`: throw `InvalidOperationException("PASSWORD_CHANGE_REQUIRED")`
- ☐ `AuthController.LoginAsync` returns `403 Forbidden` with `PASSWORD_CHANGE_REQUIRED` error code
- ☐ API clients should not receive tokens while password change is required

**File:** `src/Core/DotNetCloud.Core.Auth/Services/AuthService.cs`  
**File:** `src/Core/DotNetCloud.Core.Server/Controllers/AuthController.cs`

---

### Phase E: Middleware — Enforce Password Change
*(Depends on Phase D step d.1)*

#### Step phase-e.1 — Create `PasswordChangeRequiredMiddleware`
**Status:** ☐ not-started  
**Deliverables:**
- ☐ New middleware class following existing conventions (cf. `SecurityHeadersMiddleware`)
- ☐ Logic:
  - After authentication: check `context.User.Identity?.IsAuthenticated`
  - Look up current user via `UserManager<ApplicationUser>`
  - If `PasswordChangeRequired == true`:
    - **Allowed paths:** `/auth/change-password`, `/auth/logout`, `/auth/session/change-password`, static assets (`/css/`, `/js/`, `/_framework/`, `/_content/`, `/favicon.ico`)
    - **All other paths:** redirect to `/auth/change-password?returnUrl={encoded current path}`
  - If `PasswordChangeRequired == false`: pass through

**File:** `src/Core/DotNetCloud.Core.ServiceDefaults/Middleware/PasswordChangeRequiredMiddleware.cs` (new)

#### Step phase-e.2 — Register middleware in pipeline
**Status:** ☐ not-started  
**Deliverables:**
- ☐ `app.UseMiddleware<PasswordChangeRequiredMiddleware>()` added AFTER authentication/authorization middleware but BEFORE endpoint routing
- ☐ Must run after `UseAuthentication()` and `UseAuthorization()`

**File:** `src/Core/DotNetCloud.Core.Server/Program.cs`

---

### Phase F: Admin User Creation UI Updates
*(Depends on Phase A)*

#### Step phase-f.1 — Update `UserCreate.razor` with `PasswordChangeRequired` checkbox
**Status:** ☐ not-started  
**Deliverables:**
- ☐ Checkbox: *"Require password change on first login"*
- ☐ Default: checked when `ClosedSystemEnabled = true`, unchecked otherwise
- ☐ Pass value through to registration API

**File:** `src/UI/DotNetCloud.UI.Web.Client/Pages/Admin/UserCreate.razor`

#### Step phase-f.2 — Update `RegisterRequest` DTO
**Status:** ☐ not-started  
**Deliverables:**
- ☐ Add `bool PasswordChangeRequired` property (ignored during self-registration, used by admin create)
- ☐ Set via admin form, defaults to `false`

**File:** `src/Core/DotNetCloud.Core/DTOs/AuthDtos.cs`

---

### Phase G: Testing & Verification
*(Depends on all prior phases)*

#### Step phase-g.1 — Unit tests for `AuthService`
**Status:** ☐ not-started  
**Deliverables:**
- ☐ `RegisterAsync` rejects self-registration when `ClosedSystemEnabled = true`
- ☐ `RegisterAsync` sets `PasswordChangeRequired = true` for admin-created users in closed mode
- ☐ `RegisterAsync` allows self-registration when `ClosedSystemEnabled = false`
- ☐ `LoginAsync` throws `PASSWORD_CHANGE_REQUIRED` when flag is set
- ☐ `LoginAsync` proceeds normally when flag is `false`

**File:** `tests/DotNetCloud.Core.Tests/Services/AuthServiceTests.cs`

#### Step phase-g.2 — Integration tests
**Status:** ☐ not-started  
**Deliverables:**
- ☐ Self-registration endpoint returns `403` in closed mode
- ☐ Admin-created user forced to change password on first login
- ☐ After password change, user can access normal pages
- ☐ Middleware redirects to change-password page when flag is set
- ☐ Middleware allows access to static assets even when flag is set

#### Step phase-g.3 — Manual verification checklist
**Status:** ☐ not-started  
**Deliverables:**
- ☐ Enable closed system mode via admin settings
- ☐ Verify `/auth/register` shows "registration disabled" message
- ☐ Create user via admin panel with "require password change"
- ☐ Log in as new user → verify redirect to change password page
- ☐ Enter current + new password → verify success redirect
- ☐ Verify normal pages accessible after password change
- ☐ Verify `/auth/login` and `/auth/logout` remain accessible during forced change
- ☐ Disable closed system mode → verify self-registration works again

---

## Files Summary

### Modified (13 files)
| File | Change |
|------|--------|
| `src/Core/DotNetCloud.Core.Data/Entities/Identity/ApplicationUser.cs` | Add `PasswordChangeRequired` property |
| `src/Core/DotNetCloud.Core.Data/Configuration/Identity/ApplicationUserConfiguration.cs` | Configure new column |
| `src/Core/DotNetCloud.Core/DTOs/AuthDtos.cs` | Add `PasswordChangeRequired` to `RegisterRequest`; new `ChangePasswordRequest` |
| `src/Core/DotNetCloud.Core/Services/IAuthService.cs` | Add `ChangePasswordAsync` method signature |
| `src/Core/DotNetCloud.Core.Auth/Services/AuthService.cs` | Closed-system check in `RegisterAsync`; `PASSWORD_CHANGE_REQUIRED` in `LoginAsync`; new `ChangePasswordAsync` |
| `src/Core/DotNetCloud.Core.Server/Controllers/AuthController.cs` | Handle closed-system 403; add change-password API endpoint |
| `src/Core/DotNetCloud.Core.Server/Controllers/AuthSessionController.cs` | Redirect to change-password on login; form-based change-password action |
| `src/UI/DotNetCloud.UI.Web/Components/Pages/Auth/Register.razor` | Show "disabled" message in closed mode |
| `src/UI/DotNetCloud.UI.Web.Client/Pages/Admin/UserCreate.razor` | Add `PasswordChangeRequired` checkbox |
| `src/Core/DotNetCloud.Core.Server/Program.cs` | Register `PasswordChangeRequiredMiddleware` |
| `tests/DotNetCloud.Core.Tests/Services/AuthServiceTests.cs` | New unit tests |
| `docs/IMPLEMENTATION_CHECKLIST.md` | Add new tasks |
| `docs/MASTER_PROJECT_PLAN.md` | Add new phase entry |

### Created (3 files)
| File | Purpose |
|------|---------|
| `src/UI/DotNetCloud.UI.Web/Components/Pages/Auth/ChangePassword.razor` | Change password page |
| `src/Core/DotNetCloud.Core.ServiceDefaults/Middleware/PasswordChangeRequiredMiddleware.cs` | Enforcement middleware |
| `src/Core/DotNetCloud.Core/Constants/SystemSettingKeys.cs` | Setting key constants |

### Generated (1 file)
| File | Purpose |
|------|---------|
| `src/Core/DotNetCloud.Core.Data/Migrations/*_AddPasswordChangeRequired.cs` | EF migration |

---

## Key Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Setting key | `dotnetcloud.core` / `ClosedSystemEnabled` = `"true"`/`"false"` | Follows existing `SystemSetting` convention |
| Flag type | `bool PasswordChangeRequired` on `ApplicationUser` | Simplest approach; sufficient for the requirement |
| Caller detection | `caller.HasRole("Administrator")` | Already built into `CallerContext` |
| Middleware placement | After `UseAuthentication()`/`UseAuthorization()`, before routing | Ensures user identity is available |
| DTO approach | Add `PasswordChangeRequired` to `RegisterRequest` | Pragmatic; avoids duplicating the registration pipeline |
| Static asset allowlist | Paths starting with `/css/`, `/js/`, `/_framework/`, `/_content/`, `/favicon.ico` | Ensures Blazor can load runtime during forced password change |
| API token issuance | Return `PASSWORD_CHANGE_REQUIRED` error; do NOT issue tokens | Prevents bypassing requirement via API clients |

---

## Scope Boundaries

| In Scope | Out of Scope |
|----------|-------------|
| Web UI (Blazor Server) registration | Android/MAUI client login flow |
| Web UI (Blazor Server) login & password change | Desktop SyncTray client login flow |
| Admin user creation with force-password-change | Email confirmation interaction (currently disabled) |
| Enforcement middleware for web requests | API client (JWT bearer) middleware enforcement |
| Unit & integration tests for web flow | Client-side tests for Android/MAUI/SyncTray |

---

## Verification

```bash
# Build
dotnet build DotNetCloud.CI.slnf

# Tests
dotnet test DotNetCloud.CI.slnf
```

**Manual verification:**
1. Enable closed system mode via admin settings
2. Verify `/auth/register` shows "registration disabled"
3. Create user via admin panel with "require password change"
4. Log in as new user → redirected to `/auth/change-password`
5. Enter current + new password → redirected to home
6. Verify normal pages are accessible
7. Verify `/auth/login` and `/auth/logout` remain accessible during forced change
8. Disable closed system mode → self-registration works again
