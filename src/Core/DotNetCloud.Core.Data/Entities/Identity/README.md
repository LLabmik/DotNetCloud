# Identity Models

This directory contains ASP.NET Core Identity entity models for the DotNetCloud application.

## Overview

The Identity models extend ASP.NET Core Identity's base classes (`IdentityUser` and `IdentityRole`) with application-specific properties and use `Guid` as the primary key type.

## Entity Models

### ApplicationUser

Extends `IdentityUser<Guid>` with the following additional properties:

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `DisplayName` | `string` | Yes | - | User-friendly display name shown in the UI |
| `AvatarUrl` | `string?` | No | `null` | URL to the user's avatar image |
| `Locale` | `string` | Yes | `"en-US"` | User's preferred language/locale (e.g., "en-US", "fr-FR") |
| `Timezone` | `string` | Yes | `"UTC"` | User's timezone identifier (e.g., "America/New_York") |
| `CreatedAt` | `DateTime` | Yes | `DateTime.UtcNow` | When the user account was created |
| `LastLoginAt` | `DateTime?` | No | `null` | Last time the user logged in |
| `IsActive` | `bool` | Yes | `true` | Whether the user account is active (inactive users cannot log in) |

**Configuration:**
- Display name: max 200 characters
- Avatar URL: max 500 characters
- Locale: max 10 characters
- Timezone: max 50 characters
- Indexes: DisplayName, Email, IsActive, LastLoginAt

### ApplicationRole

Extends `IdentityRole<Guid>` with the following additional properties:

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `Description` | `string?` | No | `null` | Description of the role's purpose and permissions |
| `IsSystemRole` | `bool` | Yes | `false` | Whether this is a system role (cannot be deleted) |

**Configuration:**
- Description: max 500 characters
- Indexes: IsSystemRole, Name

**System Roles Examples:**
- `System Administrator` - Full system access
- `Module Manager` - Can manage modules and capabilities

## Entity Framework Configuration

Both entities have corresponding configuration classes using EF Core's Fluent API:

- `ApplicationUserConfiguration` - Configures ApplicationUser entity
- `ApplicationRoleConfiguration` - Configures ApplicationRole entity

These configurations are automatically applied when the `CoreDbContext` is initialized.

## Database Context

The `CoreDbContext` extends `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`, which provides:

- ASP.NET Core Identity table structure
- User management (CRUD operations)
- Role management
- User-role associations
- User claims, logins, tokens
- Role claims

All Identity tables are automatically named according to the active naming strategy (PostgreSQL, SQL Server, or MariaDB).

## Usage Example

### Creating a User

```csharp
var user = new ApplicationUser
{
    Id = Guid.NewGuid(),
    UserName = "johndoe",
    Email = "john@example.com",
    DisplayName = "John Doe",
    Locale = "en-US",
    Timezone = "America/New_York",
    IsActive = true
};

// Use UserManager from ASP.NET Core Identity
await userManager.CreateAsync(user, password);
```

### Creating a Role

```csharp
var role = new ApplicationRole
{
    Id = Guid.NewGuid(),
    Name = "Administrator",
    Description = "Full system access",
    IsSystemRole = false
};

// Use RoleManager from ASP.NET Core Identity
await roleManager.CreateAsync(role);
```

### Assigning a Role to a User

```csharp
await userManager.AddToRoleAsync(user, "Administrator");
```

## Testing

Comprehensive unit tests are available in `tests/DotNetCloud.Core.Data.Tests/Entities/Identity/`:

- `ApplicationUserTests.cs` - 12 test methods
- `ApplicationRoleTests.cs` - 10 test methods

All tests verify:
- Default values
- Property getters/setters
- Guid primary key type
- Inheritance from Identity base classes
- Round-trip serialization

## Integration with ASP.NET Core Identity

These models integrate seamlessly with ASP.NET Core Identity services:

```csharp
// In Startup.cs or Program.cs
services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    // Configure Identity options
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Lockout.MaxFailedAccessAttempts = 5;
})
.AddEntityFrameworkStores<CoreDbContext>()
.AddDefaultTokenProviders();
```

## Multi-Database Support

The Identity models work with all supported database providers:

- ✅ **PostgreSQL** - Uses schema `core` with snake_case naming
- ✅ **SQL Server** - Uses schema `[core]` with PascalCase naming
- ⏳ **MariaDB** - Will use table prefix `core_` with snake_case naming (awaiting Pomelo package update)

The naming strategy is automatically applied based on the connection string.

## Next Steps

After Identity Models (phase-0.2.2), the next steps are:

1. **phase-0.2.3** - Organization Hierarchy Models (Organization, Team, TeamMember, Group, etc.)
2. **phase-0.2.4** - Permission System Models (Permission, Role, RolePermission)
3. **phase-0.2.5** - Settings Models (SystemSetting, OrganizationSetting, UserSetting)

## References

- [ASP.NET Core Identity Documentation](https://learn.microsoft.com/aspnet/core/security/authentication/identity)
- [Identity Model Customization](https://learn.microsoft.com/aspnet/core/security/authentication/customize-identity-model)
- [EF Core with Identity](https://learn.microsoft.com/ef/core/what-is-new/ef-core-9.0/whatsnew#identity-support)

## License

This code is part of the DotNetCloud project and is licensed under AGPL-3.0.
