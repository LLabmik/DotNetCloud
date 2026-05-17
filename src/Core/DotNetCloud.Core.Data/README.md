# DotNetCloud.Core.Data

**Version:** 1.0.0  
**Status:** ✅ Complete (Phase 0.2)  
**Target Framework:** .NET 10

## Overview


## Key Features

✅ **Multi-Database Provider Support**
- PostgreSQL (Npgsql) - Primary target
- SQL Server - Enterprise support

✅ **Flexible Table Naming Strategies**
- PostgreSQL: Schema-based (`core.users`, `files.documents`)
- SQL Server: Schema-based

✅ **Comprehensive Entity Models**
- ASP.NET Core Identity integration with custom user/role models
- Organization hierarchy (Organizations, Teams, Groups)
- Permission system (Roles, Permissions)
- Settings (System, Organization, User-scoped)
- Module registry and capability grants
- Device tracking

✅ **Automated Database Management**
- Database migrations for PostgreSQL and SQL Server
- Automatic timestamp management via interceptors
- Soft-delete query filters
- Database initialization and seeding

## Project Structure

```
DotNetCloud.Core.Data/
├── Context/
│   └── CoreDbContext.cs                  # Main EF Core DbContext
├── Entities/
│   ├── Identity/                         # ASP.NET Core Identity entities
│   │   ├── ApplicationUser.cs
│   │   └── ApplicationRole.cs
│   ├── Organizations/                    # Org hierarchy entities
│   │   ├── Organization.cs
│   │   ├── Team.cs
│   │   ├── TeamMember.cs
│   │   ├── Group.cs
│   │   ├── GroupMember.cs
│   │   └── OrganizationMember.cs
│   ├── Permissions/                      # Permission system entities
│   │   ├── Permission.cs
│   │   ├── Role.cs
│   │   └── RolePermission.cs
│   ├── Settings/                         # Settings entities
│   │   ├── SystemSetting.cs
│   │   ├── OrganizationSetting.cs
│   │   └── UserSetting.cs
│   └── Modules/                          # Module registry entities
│       ├── InstalledModule.cs
│       ├── ModuleCapabilityGrant.cs
│       └── UserDevice.cs
├── Configuration/                        # EF Core entity configurations
│   ├── Identity/
│   ├── Organizations/
│   ├── Permissions/
│   ├── Settings/
│   └── Modules/
├── Naming/                               # Database provider naming strategies
│   ├── ITableNamingStrategy.cs
│   ├── PostgreSqlNamingStrategy.cs
│   ├── SqlServerNamingStrategy.cs
│   ├── DatabaseProvider.cs
│   └── DatabaseProviderDetector.cs
├── Infrastructure/                       # DbContext factory
│   ├── IDbContextFactory.cs
│   └── DefaultDbContextFactory.cs
├── Initialization/                       # Database seeding
│   └── DbInitializer.cs
├── Interceptors/                         # EF Core interceptors
│   └── TimestampInterceptor.cs
└── Migrations/                           # Database migrations
    ├── 20260302195528_InitialCreate.cs   # PostgreSQL initial migration
    └── SqlServer/                        # SQL Server migrations
        └── 20260302203100_InitialCreate_SqlServer.cs
```

## Entity Models

### Identity Entities

#### ApplicationUser
Extends `IdentityUser<Guid>` with additional properties:
- `DisplayName` - User's display name
- `AvatarUrl` - Profile picture URL
- `Locale` - User's preferred language (default: "en-US")
- `Timezone` - User's timezone (default: "UTC")
- `CreatedAt` - Account creation timestamp
- `LastLoginAt` - Last successful login timestamp
- `IsActive` - Account active status

#### ApplicationRole
Extends `IdentityRole<Guid>` with:
- `Description` - Role description
- `IsSystemRole` - Indicates system-managed role

### Organization Hierarchy

#### Organization
Top-level organizational unit:
- `Name` - Organization name
- `Description` - Optional description
- `CreatedAt` - Creation timestamp
- `IsDeleted`, `DeletedAt` - Soft-delete support

#### Team
Organization sub-units:
- `OrganizationId` - Parent organization
- `Name` - Team name
- Soft-delete support

#### Group
Cross-team permission groups:
- `OrganizationId` - Parent organization
- `Name` - Group name

#### Membership Entities
- `TeamMember` - User membership in teams
- `GroupMember` - User membership in groups
- `OrganizationMember` - User membership in organizations with role assignments

### Permission System

#### Permission
Defines individual permissions:
- `Code` - Unique permission code (e.g., "files.upload")
- `DisplayName` - Human-readable name
- `Description` - Optional description

#### Role
Groups permissions together:
- `Name` - Role name
- `Description` - Optional description
- `IsSystemRole` - System-managed flag
- `Permissions` - Navigation to assigned permissions

#### RolePermission
Many-to-many junction table between roles and permissions.

### Settings (Three Scopes)

#### SystemSetting
System-wide configuration:
- Composite key: (`Module`, `Key`)
- `Value` - JSON-serializable value
- `Description` - Optional description
- `UpdatedAt` - Last modification timestamp

#### OrganizationSetting
Organization-scoped settings:
- `OrganizationId` - FK to organization
- `Module`, `Key`, `Value` - Setting triple
- Unique constraint: (OrganizationId, Module, Key)

#### UserSetting
User-scoped preferences:
- `UserId` - FK to user
- `Module`, `Key`, `Value` - Setting triple
- `IsEncrypted` - Encryption flag for sensitive data
- Unique constraint: (UserId, Module, Key)

### Module Registry

#### InstalledModule
Tracks installed modules:
- `ModuleId` - Primary key (e.g., "dotnetcloud.files")
- `Version` - Module version
- `Status` - Module status (Enabled, Disabled, UpdateAvailable)
- `InstalledAt`, `UpdatedAt` - Timestamps

#### ModuleCapabilityGrant
Tracks granted capabilities per module:
- `ModuleId` - FK to module
- `CapabilityName` - Granted capability
- `GrantedAt` - Grant timestamp
- `GrantedByUserId` - Admin who granted (nullable)

#### UserDevice
Tracks user devices for push notifications:
- `UserId` - FK to user
- `Name` - Device name
- `DeviceType` - Device type enum
- `PushToken` - Push notification token
- `LastSeenAt` - Last activity timestamp

## Database Provider Support

### PostgreSQL (Primary)
- **Provider:** Npgsql.EntityFrameworkCore.PostgreSQL
- **Version:** 10.0.0
- **Naming:** Schema-based (e.g., `core.users`, `core.organizations`)
- **Migration Folder:** `Migrations/`

### SQL Server
- **Provider:** Microsoft.EntityFrameworkCore.SqlServer
- **Version:** 10.0.0
- **Naming:** Schema-based (e.g., `core.users`)
- **Migration Folder:** `Migrations/SqlServer/`

- **Version:** Awaiting .NET 10 support
- **Naming:** Prefix-based (e.g., `core_users`, `core_organizations`)

## Naming Strategies

The `ITableNamingStrategy` interface provides database-specific table naming:

```csharp
public interface ITableNamingStrategy
{
    string GetTableName(string tableName, string? schema = null);
    string GetSchemaName(string? schema = null);
    DatabaseProvider Provider { get; }
}
```

### PostgreSQL Strategy
Uses native schemas:
```sql
CREATE TABLE core.users (...);
CREATE TABLE files.documents (...);
```

### SQL Server Strategy
Uses native schemas (same as PostgreSQL):
```sql
CREATE TABLE core.users (...);
CREATE TABLE files.documents (...);
```

Uses table prefixes (schemas not fully supported):
```sql
CREATE TABLE core_users (...);
CREATE TABLE files_documents (...);
```

## Database Initialization

The `DbInitializer` class handles:

1. **Database Creation & Migration**
   - Ensures database exists
   - Applies pending migrations
   - Verifies connectivity

2. **Default Data Seeding**
   - System roles (Administrator, User, Guest)
   - Default permissions
   - System settings for core modules

3. **Transaction Management**
   - All seeding operations run in a transaction
   - Automatic rollback on failure

### Usage Example

```csharp
services.AddScoped<DbInitializer>();

// In Program.cs or startup
var initializer = serviceProvider.GetRequiredService<DbInitializer>();
await initializer.InitializeAsync(cancellationToken);
```

## Migrations

### Creating Migrations

#### PostgreSQL (Default)
```powershell
dotnet ef migrations add <MigrationName> --project src\Core\DotNetCloud.Core.Data --startup-project src\Core\DotNetCloud.Core.Data --context CoreDbContext
```

#### SQL Server
```powershell
dotnet ef migrations add <MigrationName>_SqlServer --project src\Core\DotNetCloud.Core.Data --startup-project src\Core\DotNetCloud.Core.Data --context CoreDbContext --output-dir Migrations\SqlServer
```

### Applying Migrations

Migrations are automatically applied by `DbInitializer` during application startup.

Manual application:
```powershell
dotnet ef database update --project src\Core\DotNetCloud.Core.Data --context CoreDbContext
```

## Configuration

### Connection String Examples

#### PostgreSQL
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=dotnetcloud;Username=dotnetcloud;Password=***"
  }
}
```

#### SQL Server
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=DotNetCloud;User Id=sa;Password=***;TrustServerCertificate=True"
  }
}
```

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=dotnetcloud;User=dotnetcloud;Password=***"
  }
}
```

### Dependency Injection Setup

```csharp
// Detect database provider from connection string
var connectionString = configuration.GetConnectionString("DefaultConnection");
var provider = DatabaseProviderDetector.DetectProvider(connectionString);

// Register naming strategy
services.AddSingleton<ITableNamingStrategy>(provider switch
{
    DatabaseProvider.PostgreSql => new PostgreSqlNamingStrategy(),
    DatabaseProvider.SqlServer => new SqlServerNamingStrategy(),
    _ => throw new NotSupportedException($"Database provider {provider} is not supported.")
});

// Register DbContext
services.AddDbContext<CoreDbContext>((serviceProvider, options) =>
{
    var namingStrategy = serviceProvider.GetRequiredService<ITableNamingStrategy>();
    
    switch (provider)
    {
        case DatabaseProvider.PostgreSql:
            options.UseNpgsql(connectionString);
            break;
        case DatabaseProvider.SqlServer:
            options.UseSqlServer(connectionString);
            break;
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
            break;
    }
});

// Register initializer
services.AddScoped<DbInitializer>();
```

## Automatic Features

### Timestamp Management
The `TimestampInterceptor` automatically sets:
- `CreatedAt` on entity creation
- `UpdatedAt` on entity modification

### Soft Delete Query Filters
Entities with `IsDeleted` property are automatically filtered from queries unless explicitly included.

## Testing

### Unit Tests
Located in `tests/DotNetCloud.Core.Data.Tests/`:
- Entity validation tests
- Configuration tests
- Naming strategy tests
- DbInitializer tests

### Running Tests
```powershell
dotnet test tests\DotNetCloud.Core.Data.Tests\DotNetCloud.Core.Data.Tests.csproj
```

**Test Coverage:** 103 tests passing ✅

## Dependencies

```xml
<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Relational" Version="10.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.0" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.0" />
```

## Known Limitations

3. **Migration Management:** Separate migration folders required for SQL Server

## Future Enhancements

- [ ] Implement read-only replicas support
- [ ] Add database sharding support for large deployments
- [ ] Implement audit logging interceptor
- [ ] Add data encryption at rest

## Related Projects

- **DotNetCloud.Core** - Core abstractions and interfaces
- **DotNetCloud.Core.Tests** - Unit tests for core functionality
- **DotNetCloud.Core.Data.Tests** - Data layer integration tests

## Contributing

See [CONTRIBUTING.md](../../../CONTRIBUTING.md) for guidelines.

## License

AGPL-3.0 - See [LICENSE](../../../LICENSE) for details.

---

**Last Updated:** 2026-03-02  
**Maintainer:** DotNetCloud Development Team
