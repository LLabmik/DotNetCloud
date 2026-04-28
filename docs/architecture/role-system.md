# Role System Design

## Current State

The project has three separate role-like systems, none fully implemented and none connected to each other.

### System 1: ASP.NET Core Identity Roles

Stored in the standard `AspNetRoles` table via `ApplicationRole : IdentityRole<Guid>`.

**What exists:**
- A single role named `"Administrator"` created by `AdminSeeder` (`src/Core/DotNetCloud.Core.Server/Initialization/AdminSeeder.cs:125-148`)
- Only the initial admin user (ben.kimball) is assigned to it via `UserManager.AddToRoleAsync` (line 114)
- No other user ever receives this or any other Identity role

**How it's used for authorization:**
- `PermissionAuthorizationHandler` (`src/Core/DotNetCloud.Core.Auth/Authorization/PermissionAuthorizationHandler.cs:24-25`) has a hardcoded check: if the required permission is `"admin"` and the user `IsInRole("Administrator")`, the requirement succeeds
- `UserManagementController.IsAdmin()` (lines 372-379) checks five different conditions including `IsInRole("Administrator")`
- `AuthSessionController` (line 109) checks `IsInRoleAsync(user, "Administrator")` to allow redirect to `/admin` paths
- `DotNetCloudClaimsTransformation` (`src/Core/DotNetCloud.Core.Auth/Security/DotNetCloudClaimsTransformation.cs:83-84`) adds Identity roles as `ClaimTypes.Role` claims on each request

**What's missing:**
- No API endpoint to add or remove Identity roles from users
- No UI for managing roles (UserEdit.razor edits only DisplayName, Locale, Timezone, StorageQuota)
- No default role assigned on registration (`AuthService.RegisterAsync` never calls `AddToRoleAsync`)
- The `"dnc:perm"` claim type is checked in multiple places but never written by any code

### System 2: Permissions.Role Table

Stored in its own table (distinct from `AspNetRoles`) via `DotNetCloud.Core.Data.Entities.Permissions.Role`.

**What exists:**
- 4 roles seeded by `DbInitializer.SeedDefaultRolesAsync` (`src/Core/DotNetCloud.Core.Data/Initialization/DbInitializer.cs:176-224`): Administrator, User, Guest, Moderator — all with random GUIDs (`Guid.NewGuid()`)
- 38 fine-grained permissions seeded by `SeedDefaultPermissionsAsync` (lines 244-333): `core.admin`, `files.upload`, `chat.send`, `calendar.create`, etc.
- `RolePermission` junction table defined and configured — but **never populated** with any data
- `CoreDbContext` uses `public new DbSet<Role> Roles` (line 131) which shadows Identity's `Roles` property

**What's missing:**
- No code anywhere queries this table at runtime
- No service, manager, or store exists for `Permissions.Role`
- The `RolePermission` associations are never created
- No UI or API references these entities

### System 3: Organization-Scoped RoleIds

Stored as a JSON array in `OrganizationMember.RoleIds` (and `TeamMember.RoleIds`).

**What exists:**
- `OrganizationMember.RoleIds` is `ICollection<Guid>`, serialized to JSON (`jsonb` in PostgreSQL, `nvarchar(max)` in SQL Server)
- JSON conversion configured in `OrganizationMemberConfiguration` (lines 30-40) with a `ValueComparer`
- Identical setup duplicated in `TeamMemberConfiguration` (lines 30-40)
- Two well-known GUIDs hardcoded in two places:
  - `CalendarService.HasManagerOrAboveRole()` (lines 227-236): checks for Manager (`a1b2c3d4-0001-4000-8000-000000000001`) or Admin (`a1b2c3d4-0002-4000-8000-000000000001`)
  - `CalendarEventService.HasManagerOrAboveRole()` (lines 457-463): exact duplicate of the same logic
- These GUIDs are referenced in tests (`OrganizationCalendarAuthorizationTests` lines 33-35)

**What's missing:**
- The well-known GUIDs are **never seeded** into any database table — the `DbInitializer` uses `Guid.NewGuid()` and the hardcoded GUIDs in CalendarService reference nothing
- `OrganizationMemberDto` drops `RoleIds` entirely — roles are invisible in the API
- `AddOrganizationMemberDto` has no `RoleIds` field — can't specify roles when adding a member
- `OrganizationsController.AddMemberAsync` creates members with empty `RoleIds`
- No API endpoint exists to manage org member roles
- No UI exists to view or assign org roles
- `IOrganizationDirectory` has no role-querying methods
- No `OrgRoleIds` constants class exists

### Summary of the Mess

```
AspNetRoles                Permissions.Role           OrganizationMember.RoleIds
┌─────────────────┐        ┌─────────────────┐        ┌──────────────────────────┐
│ "Administrator"  │        │ Administrator   │        │ GUIDs in JSON column     │
│ (used at runtime)│        │ User            │        │ a1b2c3d4-0001 (Manager)  │
│                  │        │ Guest           │        │ a1b2c3d4-0002 (Admin)    │
│ Only 1 user      │        │ Moderator       │        │                          │
│ has it           │        │                 │        │ Never seeded             │
│                  │        │ Never queried   │        │ Not in DTOs/API/UI       │
│ No UI to manage  │        │ No FK to users  │        │ No FK to anything        │
└─────────────────┘        └─────────────────┘        └──────────────────────────┘
```

## Target Design

### Role Hierarchy

```
┌──────────────────────────────────────────────────────────┐
│ System Administrator (Identity ApplicationRole)          │
│ • Create/remove organizations                            │
│ • Manage system modules                                  │
│ • System-wide configuration                              │
│ • Assign system admin role to users                      │
│ • Access all orgs (implicit)                             │
│ Stored in: AspNetRoles + AspNetUserRoles                 │
└──────────────────────────────────────────────────────────┘
                            │
        ┌───────────────────┼───────────────────┐
        ▼                   ▼                   ▼
┌───────────────┐   ┌───────────────┐   ┌───────────────┐
│ Organization A │   │ Organization B │   │ Organization C │
│               │   │               │   │               │
│ Org Admin     │   │ Org Admin     │   │ Org Admin     │
│ Org Manager   │   │ Org Manager   │   │ Org Manager   │
│ Org Member    │   │ Org Member    │   │ Org Member    │
│               │   │               │   │               │
│ Stored in:    │   │ Stored in:    │   │ Stored in:    │
│ OrgMember.    │   │ OrgMember.    │   │ OrgMember.    │
│ RoleIds[]     │   │ RoleIds[]     │   │ RoleIds[]     │
└───────────────┘   └───────────────┘   └───────────────┘
```

### Roles and Their Well-Known GUIDs

| Role | GUID | Scope | Purpose |
|------|------|-------|---------|
| System Administrator | N/A (name-based) | System | Create/remove orgs, manage modules, system settings, assign system admin |
| Org Admin | `a1b2c3d4-0002-4000-8000-000000000001` | Organization | Full org control: manage members/teams/groups, all module data, org settings |
| Org Manager | `a1b2c3d4-0001-4000-8000-000000000001` | Organization | Write access: create/edit org resources (calendars, shared folders, etc.), manage teams |
| Org Member | `a1b2c3d4-0003-4000-8000-000000000003` | Organization | Read access: view and consume org resources |

Implicit hierarchy for org roles: **Org Admin > Org Manager > Org Member**. An Org Admin automatically has Manager and Member privileges. A Manager automatically has Member privileges.

### Where Each Role Lives

| Storage | Contains | Managed By |
|---------|----------|------------|
| `AspNetRoles` + `AspNetUserRoles` | System Administrator only | `UserManager<ApplicationUser>` |
| `Permissions.Role` table | Org role definitions (Admin, Manager, Member) | Seeded by `DbInitializer`, referenced by GUID |
| `OrganizationMember.RoleIds` (JSON) | Per-member org role assignments | `OrganizationsController` |

### Authorization Flow

```
Request arrives
      │
      ▼
Authentication (cookie or JWT)
      │
      ▼
ClaimsTransformation
  ├── Adds Identity roles as ClaimTypes.Role
  ├── Adds "dnc:perm": "admin" for Administrators  ← NEW
  └── Cached 5 minutes
      │
      ▼
Policy check: [Authorize(Policy = "RequireAdmin")]
  └── PermissionAuthorizationHandler
        ├── IsInRole("Administrator")? → succeed
        └── HasClaim("dnc:perm", "admin")? → succeed  ← Now works
      │
      ▼
Module-level check: creating an org calendar
  └── CalendarService.ValidateOrgWriteAccessAsync()
        └── OrgDirectory.GetMemberAsync(orgId, userId)
              └── OrgRoleChecker.HasManagerOrAboveRole(member.RoleIds)
                    ├── Contains OrgAdmin GUID? → succeed
                    └── Contains OrgManager GUID? → succeed
```

## What Happens to Each Existing System

| System | Action |
|--------|--------|
| `ApplicationRole` (AspNetRoles) | **Keep** — used only for System Administrator. The "Administrator" role stays. |
| `Permissions.Role` table | **Repurpose** — seed with the three org role definitions (Org Admin, Org Manager, Org Member) using the well-known GUIDs, replacing the current Administrator/User/Guest/Moderator seed data that was never used. |
| `Permission` + `RolePermission` tables | **Keep but don't seed** — leave the entity definitions and table structure as scaffolding for a future fine-grained permissions system. The `SeedDefaultPermissionsAsync` code stays but is not invoked. |
| `OrganizationMember.RoleIds` | **Keep and complete** — this becomes the primary runtime role storage. Add DTO fields, API endpoints, and UI. |

## Implementation Plan

### Step 1: Create `OrgRoleIds` Constants Class

**New file:** `src/Core/DotNetCloud.Core/Authorization/OrgRoleIds.cs`

Centralize all well-known role GUIDs and the system role name into a single source of truth.

```csharp
namespace DotNetCloud.Core.Authorization;

/// <summary>
/// Well-known organization role GUIDs.
/// These are seeded into the Permissions.Role table and referenced
/// by OrganizationMember.RoleIds at runtime.
/// </summary>
public static class OrgRoleIds
{
    public static readonly Guid OrgAdmin   = Guid.Parse("a1b2c3d4-0002-4000-8000-000000000001");
    public static readonly Guid OrgManager = Guid.Parse("a1b2c3d4-0001-4000-8000-000000000001");
    public static readonly Guid OrgMember  = Guid.Parse("a1b2c3d4-0003-4000-8000-000000000003");

    /// <summary>
    /// Returns a human-readable name for a well-known org role GUID.
    /// </summary>
    public static string GetName(Guid roleId)
    {
        if (roleId == OrgAdmin)   return "Org Admin";
        if (roleId == OrgManager) return "Org Manager";
        if (roleId == OrgMember)  return "Org Member";
        return "Unknown";
    }

    /// <summary>
    /// All well-known org role GUIDs in hierarchy order (highest first).
    /// </summary>
    public static readonly IReadOnlyList<Guid> All = [OrgAdmin, OrgManager, OrgMember];
}

/// <summary>
/// Well-known system role names for Identity-based roles.
/// </summary>
public static class SystemRoleNames
{
    public const string Administrator = "Administrator";
}
```

Placed in `DotNetCloud.Core` (the SDK project) so it's accessible from all modules, Auth, Data, and Server projects.

### Step 2: Create `OrgRoleChecker` Utility

**New file:** `src/Core/DotNetCloud.Core/Authorization/OrgRoleChecker.cs`

A shared utility for role presence checks, replacing the duplicated private methods in CalendarService and CalendarEventService.

```csharp
namespace DotNetCloud.Core.Authorization;

/// <summary>
/// Stateless helper for checking org role membership against a set of role GUIDs.
/// </summary>
public static class OrgRoleChecker
{
    public static bool HasAdminRole(IEnumerable<Guid> roleIds) =>
        roleIds.Contains(OrgRoleIds.OrgAdmin);

    public static bool HasManagerOrAboveRole(IEnumerable<Guid> roleIds) =>
        roleIds.Contains(OrgRoleIds.OrgManager) || roleIds.Contains(OrgRoleIds.OrgAdmin);

    public static bool HasMemberOrAboveRole(IEnumerable<Guid> roleIds) =>
        roleIds.Contains(OrgRoleIds.OrgMember)
        || roleIds.Contains(OrgRoleIds.OrgManager)
        || roleIds.Contains(OrgRoleIds.OrgAdmin);

    /// <summary>
    /// Returns the highest org role GUID present, or null if none.
    /// </summary>
    public static Guid? GetHighestRole(IEnumerable<Guid> roleIds)
    {
        if (roleIds.Contains(OrgRoleIds.OrgAdmin))   return OrgRoleIds.OrgAdmin;
        if (roleIds.Contains(OrgRoleIds.OrgManager)) return OrgRoleIds.OrgManager;
        if (roleIds.Contains(OrgRoleIds.OrgMember))  return OrgRoleIds.OrgMember;
        return null;
    }
}
```

### Step 3: Centralize `RoleIds` JSON Column Configuration

**New file:** `src/Core/DotNetCloud.Core.Data/Configuration/Shared/RoleIdsConversion.cs`

Both `OrganizationMemberConfiguration` and `TeamMemberConfiguration` define identical JSON converters and value comparers for the `ICollection<Guid> RoleIds` property. Extract into a shared helper.

```csharp
namespace DotNetCloud.Core.Data.Configuration.Shared;

public static class RoleIdsConversion
{
    public static readonly ValueConverter<ICollection<Guid>, string> Converter = new(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => JsonSerializer.Deserialize<List<Guid>>(v, (JsonSerializerOptions?)null) ?? new List<Guid>());

    public static readonly ValueComparer<ICollection<Guid>> Comparer = new(
        (c1, c2) => c1!.SequenceEqual(c2!),
        c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
        c => (ICollection<Guid>)c.ToList());
}
```

Then update both configurations:

**`OrganizationMemberConfiguration.cs`:**
```csharp
var roleIdsProp = builder.Property(om => om.RoleIds)
    .IsRequired()
    .HasConversion(RoleIdsConversion.Converter)
    .HasDefaultValue(new List<Guid>());
roleIdsProp.Metadata.SetValueComparer(RoleIdsConversion.Comparer);
```

**`TeamMemberConfiguration.cs`:** same change.

### Step 4: Update `DbInitializer` Seed Data

**File:** `src/Core/DotNetCloud.Core.Data/Initialization/DbInitializer.cs`

#### 4a: Change `SeedDefaultRolesAsync`

Replace the current 4 roles (Administrator, User, Guest, Moderator — random GUIDs, never used) with the 3 org roles using well-known GUIDs:

```csharp
var defaultRoles = new[]
{
    new Role
    {
        Id = OrgRoleIds.OrgAdmin,
        Name = "Org Admin",
        Description = "Full control over an organization including members, teams, groups, and all module data.",
        IsSystemRole = true
    },
    new Role
    {
        Id = OrgRoleIds.OrgManager,
        Name = "Org Manager",
        Description = "Can create and edit organization resources such as calendars, shared folders, and manage teams.",
        IsSystemRole = true
    },
    new Role
    {
        Id = OrgRoleIds.OrgMember,
        Name = "Org Member",
        Description = "Can view and use organization resources. Default role for new members.",
        IsSystemRole = true
    }
};
```

#### 4b: Stop Seeding Permissions

Comment out the call to `SeedDefaultPermissionsAsync` in the initialization sequence. Keep the method code for future use but don't invoke it — the 38 permissions serve no runtime purpose in Phase 0.

Add a comment:
```csharp
// Permissions seeding is deferred to a future phase.
// The permission entities and RolePermission junction exist as scaffolding
// but are not populated or enforced at runtime.
// await SeedDefaultPermissionsAsync(cancellationToken);
```

#### 4c: Add Using

Add `using DotNetCloud.Core.Authorization;` to access `OrgRoleIds`.

### Step 5: Extend `IOrganizationDirectory` with Role Queries

**File:** `src/Core/DotNetCloud.Core/Capabilities/IOrganizationDirectory.cs`

Add three methods:

```csharp
/// <summary>
/// Checks whether a user has a specific org role.
/// </summary>
Task<bool> HasOrgRoleAsync(Guid organizationId, Guid userId, Guid roleId, CancellationToken cancellationToken = default);

/// <summary>
/// Checks whether a user has Manager or Admin role in the organization.
/// </summary>
Task<bool> HasManagerOrAboveRoleAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default);

/// <summary>
/// Gets the org role GUIDs assigned to a user in an organization.
/// </summary>
Task<IReadOnlyList<Guid>> GetUserRoleIdsAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default);
```

**File:** `src/Core/DotNetCloud.Core.Auth/Capabilities/OrganizationDirectoryService.cs`

Implement the new methods:

```csharp
/// <inheritdoc />
public async Task<bool> HasOrgRoleAsync(Guid organizationId, Guid userId, Guid roleId, CancellationToken cancellationToken = default)
{
    return await _dbContext.Set<OrganizationMember>()
        .AsNoTracking()
        .AnyAsync(m => m.OrganizationId == organizationId
                       && m.UserId == userId
                       && m.IsActive
                       && m.RoleIds.Contains(roleId), cancellationToken);
}

/// <inheritdoc />
public async Task<bool> HasManagerOrAboveRoleAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
{
    var member = await GetMemberAsync(organizationId, userId, cancellationToken);
    return member is not null && OrgRoleChecker.HasManagerOrAboveRole(member.RoleIds);
}

/// <inheritdoc />
public async Task<IReadOnlyList<Guid>> GetUserRoleIdsAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
{
    var member = await GetMemberAsync(organizationId, userId, cancellationToken);
    return member?.RoleIds ?? Array.Empty<Guid>();
}
```

### Step 6: Update Calendar Module Authorization

**Files:**
- `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Data/Services/CalendarService.cs`
- `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Data/Services/CalendarEventService.cs`

Remove the private `HasManagerOrAboveRole` method from both files. Replace call sites with `OrgRoleChecker.HasManagerOrAboveRole(member.RoleIds)`.

This also removes the hardcoded `Guid.Parse("a1b2c3d4-...")` from both services.

**CalendarService.cs changes:**
- Lines 227-236: remove `HasManagerOrAboveRole` method
- Line 218 (`ValidateOrgWriteAccessAsync`): replace `HasManagerOrAboveRole(member)` with `OrgRoleChecker.HasManagerOrAboveRole(member.RoleIds)`
- Line 195 (`CanAccessCalendarAsync`): same replacement

**CalendarEventService.cs changes:**
- Lines 457-463: remove `HasManagerOrAboveRole` method
- Line 450 (`CanAccessCalendarAsync`): replace with `OrgRoleChecker.HasManagerOrAboveRole(member.RoleIds)`

**Test file to update:**
- `tests/DotNetCloud.Modules.Calendar.Tests/OrganizationCalendarAuthorizationTests.cs`
  - Replace `private static readonly Guid ManagerRoleId = Guid.Parse(...)` and `AdminRoleId = Guid.Parse(...)` with references to `OrgRoleIds.OrgManager` and `OrgRoleIds.OrgAdmin`

### Step 7: Centralize System Admin Checks

#### 7a: Create `ClaimsPrincipalExtensions`

**New file:** `src/Core/DotNetCloud.Core.Auth/Extensions/ClaimsPrincipalExtensions.cs`

```csharp
namespace DotNetCloud.Core.Auth.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Returns true if the principal has the System Administrator role.
    /// </summary>
    public static bool IsSystemAdmin(this ClaimsPrincipal principal)
    {
        return principal.IsInRole(SystemRoleNames.Administrator)
            || principal.HasClaim(PermissionAuthorizationHandler.PermissionClaimType, "admin");
    }
}
```

#### 7b: Update `UserManagementController.IsAdmin()`

Replace the 5-condition check (lines 372-379) with:
```csharp
private bool IsAdmin() => User.IsSystemAdmin();
```

#### 7c: Update `AuthSessionController`

Replace the dual `IsInRoleAsync` check (lines 109-110) with a query that checks for the Administrator role via `UserManager`.

#### 7d: Update `PermissionAuthorizationHandler`

Replace hardcoded `"Administrator"` string (line 24) with `SystemRoleNames.Administrator`:
```csharp
if (string.Equals(requirement.Permission, "admin", StringComparison.OrdinalIgnoreCase)
    && context.User.IsInRole(SystemRoleNames.Administrator))
```

### Step 8: Expose Org Roles in DTOs and API

**File:** `src/Core/DotNetCloud.Core/DTOs/OrganizationDtos.cs`

#### 8a: Add `RoleIds` and `RoleNames` to `OrganizationMemberDto`

```csharp
/// <summary>
/// Gets or sets the organization-scoped role IDs assigned to this member.
/// </summary>
public IReadOnlyList<Guid> RoleIds { get; set; } = Array.Empty<Guid>();

/// <summary>
/// Gets or sets the human-readable role names for display.
/// </summary>
public IReadOnlyList<string> RoleNames { get; set; } = Array.Empty<string>();
```

#### 8b: Add `RoleIds` to `AddOrganizationMemberDto`

```csharp
/// <summary>
/// Gets or sets optional org role IDs to assign. Defaults to [OrgMember] if empty.
/// </summary>
public List<Guid>? RoleIds { get; set; }
```

**File:** `src/Core/DotNetCloud.Core.Server/Controllers/OrganizationsController.cs`

#### 8c: Update `ListMembersAsync` to populate roles

In the member mapping (around line 188), add:
```csharp
RoleIds = om.RoleIds.ToList(),
RoleNames = om.RoleIds.Select(OrgRoleIds.GetName).ToList()
```

#### 8d: Add role management endpoints

```
PUT    /api/v1/core/admin/organizations/{orgId}/members/{userId}/roles
DELETE /api/v1/core/admin/organizations/{orgId}/members/{userId}/roles/{roleId}
```

`PUT` replaces all roles for a member. `DELETE` removes a single role.

Both protected by `[Authorize(Policy = "RequireAdmin")]`.

### Step 9: Auto-Assign Default Org Role on Member Creation

**File:** `src/Core/DotNetCloud.Core.Server/Controllers/OrganizationsController.cs`

In `AddMemberAsync` (around line 220), populate default roles:

```csharp
var member = new OrganizationMember
{
    OrganizationId = id,
    UserId = dto.UserId,
    JoinedAt = DateTime.UtcNow,
    IsActive = true,
    RoleIds = dto.RoleIds?.Count > 0
        ? dto.RoleIds
        : new List<Guid> { OrgRoleIds.OrgMember }
};
```

This ensures every org member always has at least the Org Member role.

### Step 10: Add System Role Management API

**File:** `src/Core/DotNetCloud.Core.Server/Controllers/UserManagementController.cs`

Add two endpoints:

```
PUT    /api/v1/core/users/{userId}/roles
DELETE /api/v1/core/users/{userId}/roles/{roleName}
```

`PUT` accepts `{ "roles": ["Administrator"] }` and calls `UserManager.AddToRoleAsync` / `RemoveFromRoleAsync` to set the user's roles to exactly the provided list.

`DELETE` removes a single named role from the user.

Both protected by `[Authorize(Policy = "RequireAdmin")]`.

### Step 11: Add Role Management UI

#### 11a: Organizations Member Modal

**File:** `src/UI/DotNetCloud.UI.Web.Client/Pages/Admin/Organizations.razor`

In the members list modal, add:
- A **Roles** column showing human-readable role names (using `OrgRoleIds.GetName()`)
- **Promote** / **Demote** buttons for each member:
  - Member → Manager (adds Manager GUID)
  - Manager → Admin (adds Admin GUID)
  - Demote reverses the chain
- The `AddOrganizationMemberDto` should default to Org Member

#### 11b: User Detail Page

**File:** `src/UI/DotNetCloud.UI.Web.Client/Pages/Admin/UserDetail.razor`

Add a **System Administrator** toggle:
- Shows a badge if the user has the Administrator role
- Button to "Grant Admin" / "Revoke Admin" (only visible to current system admins)
- Calls the role management endpoints from Step 10

#### 11c: API Client Methods

**File:** `src/UI/DotNetCloud.UI.Web.Client/Services/DotNetCloudApiClient.cs`

Add methods:
```csharp
Task SetUserRolesAsync(Guid userId, List<string> roles);
Task SetOrgMemberRolesAsync(Guid orgId, Guid userId, List<Guid> roleIds);
Task RemoveOrgMemberRoleAsync(Guid orgId, Guid userId, Guid roleId);
```

### Step 12: Update Claims Transformation

**File:** `src/Core/DotNetCloud.Core.Auth/Security/DotNetCloudClaimsTransformation.cs`

In `BuildAdditionalClaimsAsync`, after adding Identity roles, also add a `"dnc:perm"` claim for administrators:

```csharp
// If the user has the Administrator role, add the admin permission claim.
// This makes HasClaim("dnc:perm", "admin") work for policy-based authorization.
if (roles.Contains(SystemRoleNames.Administrator))
{
    additionalClaims.Add(new Claim(PermissionAuthorizationHandler.PermissionClaimType, "admin"));
}
```

This closes the loop: the `"dnc:perm"` claim that `PermissionAuthorizationHandler` and `UserManagementController.IsAdmin()` already check for will now actually be present on admin users' claims principals.

### Step 13: Document the `new DbSet<Role>` Shadowing

**File:** `src/Core/DotNetCloud.Core.Data/Context/CoreDbContext.cs`

Add a comment above the `public new DbSet<Role> Roles` property explaining the shadowing:

```csharp
// NOTE: This uses 'new' to shadow IdentityDbContext<...>.Roles (which returns DbSet<ApplicationRole>).
// These are completely separate tables:
//   - this.Roles       → Permissions.Role entity (org role definitions, well-known GUIDs)
//   - base.Roles       → ApplicationRole entity (AspNetRoles, system-level Identity roles)
// Access base Identity roles via _context.Set<ApplicationRole>() or RoleManager<ApplicationRole>.
public new DbSet<Role> Roles => Set<Role>();
```

## Implementation Order

Steps 1 and 2 have no dependencies and can be done first. Steps 3-5 can proceed in parallel. Steps 6-12 depend on the earlier foundational work.

```
Step 1: OrgRoleIds constants       ──┐
Step 2: OrgRoleChecker utility     ──┤ Foundation (no deps)
Step 3: Centralize JSON config     ──┘
                                      │
Step 4: Update DbInitializer       ◄──┤ depends on Step 1
Step 5: Extend IOrganizationDir    ◄──┤ depends on Steps 1,2
                                      │
Step 6: Update Calendar auth       ◄──┤ depends on Steps 1,2
Step 7: Centralize admin checks    ◄──┤ depends on Step 1
                                      │
Step 8: DTOs + org role API        ◄──┤ depends on Steps 1,2,5
Step 9: Auto-assign default role   ◄──┤ depends on Step 1
Step 10: System role API           ◄──┤ depends on Step 7
                                      │
Step 11: Role management UI        ◄──┤ depends on Steps 8,10
Step 12: Claims transformation     ◄──┤ depends on Step 1
Step 13: Document DbSet shadowing  ───┘ no deps
```

## Files Changed

| File | Change |
|------|--------|
| `src/Core/DotNetCloud.Core/Authorization/OrgRoleIds.cs` | **NEW** — well-known GUID constants |
| `src/Core/DotNetCloud.Core/Authorization/OrgRoleChecker.cs` | **NEW** — shared role check utility |
| `src/Core/DotNetCloud.Core.Data/Configuration/Shared/RoleIdsConversion.cs` | **NEW** — shared JSON converter |
| `src/Core/DotNetCloud.Core.Auth/Extensions/ClaimsPrincipalExtensions.cs` | **NEW** — IsSystemAdmin extension |
| `src/Core/DotNetCloud.Core.Data/Initialization/DbInitializer.cs` | Change seed roles, stop seeding permissions |
| `src/Core/DotNetCloud.Core.Data/Context/CoreDbContext.cs` | Document `new` DbSet shadowing |
| `src/Core/DotNetCloud.Core.Data/Configuration/Organizations/OrganizationMemberConfiguration.cs` | Use shared RoleIdsConversion |
| `src/Core/DotNetCloud.Core.Data/Configuration/Organizations/TeamMemberConfiguration.cs` | Use shared RoleIdsConversion |
| `src/Core/DotNetCloud.Core/Capabilities/IOrganizationDirectory.cs` | Add role query methods |
| `src/Core/DotNetCloud.Core.Auth/Capabilities/OrganizationDirectoryService.cs` | Implement role queries |
| `src/Core/DotNetCloud.Core/DTOs/OrganizationDtos.cs` | Add RoleIds/RoleNames to DTOs |
| `src/Core/DotNetCloud.Core.Server/Controllers/OrganizationsController.cs` | Role endpoints, populate roles, default role |
| `src/Core/DotNetCloud.Core.Server/Controllers/UserManagementController.cs` | System role endpoints, use IsSystemAdmin |
| `src/Core/DotNetCloud.Core.Auth/Authorization/PermissionAuthorizationHandler.cs` | Use SystemRoleNames constant |
| `src/Core/DotNetCloud.Core.Auth/Security/DotNetCloudClaimsTransformation.cs` | Emit dnc:perm claim for admins |
| `src/Core/DotNetCloud.Core.Server/Controllers/AuthSessionController.cs` | Use centralized admin check |
| `src/Modules/Calendar/.../CalendarService.cs` | Use OrgRoleChecker instead of private method |
| `src/Modules/Calendar/.../CalendarEventService.cs` | Use OrgRoleChecker instead of private method |
| `tests/.../OrganizationCalendarAuthorizationTests.cs` | Use OrgRoleIds constants |
| `src/UI/.../Pages/Admin/Organizations.razor` | Role display/management in member list |
| `src/UI/.../Pages/Admin/UserDetail.razor` | System Administrator toggle |
| `src/UI/.../Services/DotNetCloudApiClient.cs` | Role management API methods |

## Verification

1. **Build:** `dotnet build -f DotNetCloud.CI.slnf` compiles with 0 errors
2. **Tests:** `dotnet test` — all existing tests pass after updating Calendar auth test constants
3. **Manual smoke test:**
   - Register a new user → user is created with no system role (expected)
   - Add user to an org via admin panel → user auto-gets Org Member role
   - Promote user to Org Manager → user can now create org calendars
   - Promote user to Org Admin → user can manage members
   - Grant System Administrator role → user can access `/admin` pages
   - Revoke System Administrator → user loses admin access
   - Non-member of org → cannot see org calendars
   - Org Member without Manager+ → cannot create org calendars (read-only)
