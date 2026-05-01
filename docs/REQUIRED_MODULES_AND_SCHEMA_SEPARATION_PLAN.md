# Required Modules & Schema Separation Plan

## Context

Three intertwined requirements:

1. **Architecturally required modules** — `dotnetcloud.files`, `dotnetcloud.chat`, and `dotnetcloud.search` must be impossible to disable or uninstall at runtime.
2. **Schema separation** — Required modules share the `core` schema with core tables. Optional modules get dedicated schemas (`contacts`, `calendar`, etc.). Supports both PostgreSQL and SQL Server.
3. **Lazy schema creation** — A module's schema is only created when the module is installed. Uninstalled modules never get a schema.

The existing `dotnetcloud_dev` database has 117 tables in `public` from 8+ modules — we drop it and start fresh (test data only, nothing to preserve).

---

## Current State

### Required-vs-optional

Only exists as a hardcoded array in `src/CLI/DotNetCloud.CLI/Commands/SetupCommand.cs:457-465`:

```csharp
var requiredModules = new[] { "dotnetcloud.files", "dotnetcloud.chat" };
var optionalModules = new[]
{
    "dotnetcloud.contacts", "dotnetcloud.calendar",
    "dotnetcloud.notes", "dotnetcloud.tracks"
};
```

Search is not listed at all. No enforcement exists at runtime.

### Database schemas (current `dotnetcloud_dev`)

| Schema | Tables | Modules |
|--------|--------|---------|
| `core` | 4 | Core identity + settings |
| `public` | **117** | Files, Chat, Search, Contacts, Calendar, Notes, AI, Example, some Tracks — all jumbled |
| `music` | 13 | Music |
| `video` | 8 | Video |
| `photos` | 7 | Photos |
| `tracks` | 2 | Some Tracks tables |

Root cause: most module DbContexts don't call `HasDefaultSchema`, so tables land in the connection default (`public` / `dbo`).

### Module migration ownership

Module migrations are **not** run by module Host processes (they all use `UseInMemoryDatabase`). The **core server** (`DotNetCloud.Core.Server/Program.cs`) registers every module DbContext with the same connection string and runs all of their migrations on startup — unconditionally. There is no check against the `InstalledModules` table before migrating.

### Current core server migration strategies (Program.cs lines 120-209)

| Module | Strategy |
|--------|----------|
| Files | `MigrateAsync()` with legacy migration history baseline |
| Chat | `MigrateAsync()` |
| Photos | `MigrateAsync()` |
| Music | `MigrateAsync()` |
| Video | `MigrateAsync()` |
| Contacts | `EnsureModuleTablesCreatedAsync` (no EF migrations — uses `CreateTablesAsync`) |
| Calendar | `EnsureModuleTablesCreatedAsync` |
| Notes | `EnsureModuleTablesCreatedAsync` |
| AI | `EnsureModuleTablesCreatedAsync` |
| Search | `EnsureModuleTablesCreatedAsync` |
| Tracks | `TracksDbInitializer.InitializeAsync()` (calls `MigrateAsync`) |

### Existing infrastructure we'll leverage

- `ITableNamingStrategy` with three implementations (PostgreSQL, SQL Server, MariaDB) — currently only used by `CoreDbContext`
- `DatabaseProviderDetector` — detects provider from connection string
- `DataServiceExtensions.AddDotNetCloudDbContext` — registers naming strategy + DbContext in DI
- `EnsureModuleTablesCreatedAsync` helper in core server `Program.cs` — calls `IRelationalDatabaseCreator.CreateTablesAsync` for a module without EF migrations
- `ServiceProviderFactory` in CLI — creates a DI container from a connection string (currently used for CoreDbContext only)

---

## Design

### 1. Authority for required modules

New file: `src/Core/DotNetCloud.Core/Modules/RequiredModules.cs`

```csharp
public static class RequiredModules
{
    public static readonly IReadOnlySet<string> ModuleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "dotnetcloud.files",
        "dotnetcloud.chat",
        "dotnetcloud.search"
    };

    public static bool IsRequired(string moduleId)
    {
        var shortName = moduleId.StartsWith("dotnetcloud.", StringComparison.OrdinalIgnoreCase)
            ? moduleId["dotnetcloud.".Length..]
            : moduleId;
        return ModuleIds.Contains("dotnetcloud." + shortName);
    }

    /// <summary>
    /// Returns the database schema name for a module.
    /// Required modules share the <c>core</c> schema; optional modules get a dedicated schema
    /// named after their short module name (e.g., "contacts", "calendar").
    /// Works for both PostgreSQL (schema names) and SQL Server (schema names).
    /// </summary>
    public static string GetSchemaName(string moduleId)
    {
        if (IsRequired(moduleId))
            return "core";

        return moduleId.StartsWith("dotnetcloud.", StringComparison.OrdinalIgnoreCase)
            ? moduleId["dotnetcloud.".Length..].ToLowerInvariant()
            : moduleId.ToLowerInvariant();
    }
}
```

### 2. Schema mapping via `ITableNamingStrategy`

Update all three naming strategy implementations so `GetSchemaForModule` delegates to `RequiredModules.GetSchemaName`:

```
RequiredModules.GetSchemaName("files")    -> "core"      (required)
RequiredModules.GetSchemaName("chat")     -> "core"      (required)
RequiredModules.GetSchemaName("search")   -> "core"      (required)
RequiredModules.GetSchemaName("contacts") -> "contacts"
RequiredModules.GetSchemaName("calendar") -> "calendar"
...
```

Works identically for PostgreSQL (`schema.table`) and SQL Server (`[schema].[table]`). MariaDB uses table prefixes instead of schemas — required modules get `core_` prefix, optional modules get their own prefix.

### 3. `IsRequired` column on `InstalledModules`

A new `bool IsRequired` column persists the flag in the database. Since we drop and recreate, a single EF migration on CoreDbContext adds it.

### 4. Lazy schema creation

A module's database schema (tables, indexes, etc.) is only created when the module is **installed**. The trigger points are:

- **Setup wizard**: `SyncEnabledModulesToDatabaseAsync` creates the `InstalledModule` record, then calls a helper to create the schema.
- **CLI `module install`**: Creates the record, then creates the schema.
- **Module seed**: `ModuleUiRegistrationHostedService.SeedKnownModulesAsync` creates records and triggers schema creation for newly inserted modules.
- **Core server startup**: `DbInitializer` queries `InstalledModules` and only runs migrations for modules that are present. On a fresh database with no installed modules, only core tables are created.

The schema creation helper (`EnsureModuleSchemaAsync`) lives in the core server and is callable from the CLI via a shared service or direct DbContext usage.

### 5. Enforcement points

- **API**: `AdminModuleService.StopModuleAsync` throws `InvalidOperationException` if `IsRequired` is true. Controller returns 400 with error code `MODULE_REQUIRED`.
- **CLI**: `ModuleCommands` guards stop and uninstall paths.
- **Setup wizard**: `SyncEnabledModulesToDatabaseAsync` never disables required modules.
- **Process supervisor**: Unchanged — enforcement is at the admin-action boundary.

---

## Detailed Changes

### Phase 1 — Authority and database foundation

#### 1.1 New file: `src/Core/DotNetCloud.Core/Modules/RequiredModules.cs`

(as shown in Design section 1 above)

#### 1.2 `src/Core/DotNetCloud.Core.Data/Entities/Modules/InstalledModule.cs`

Add property after `Status`:

```csharp
/// <summary>
/// Whether this module is architecturally required. Required modules cannot be disabled
/// or uninstalled and share the core database schema. Set at install/seed time.
/// </summary>
public bool IsRequired { get; set; }
```

#### 1.3 `src/Core/DotNetCloud.Core.Data/Configuration/Modules/InstalledModuleConfiguration.cs`

```csharp
builder.Property(m => m.IsRequired)
    .IsRequired()
    .HasDefaultValue(false);
```

#### 1.4 Generate EF migration for `IsRequired`

```bash
dotnet ef migrations add AddIsRequiredToInstalledModule \
    --project src/Core/DotNetCloud.Core.Data \
    --startup-project src/Core/DotNetCloud.Core.Server \
    --context CoreDbContext
```

#### 1.5 `src/Core/DotNetCloud.Core/DTOs/ModuleDtos.cs`

Add to `ModuleDto`:

```csharp
public bool IsRequired { get; set; }
```

#### 1.6 Drop and recreate database

```bash
# PostgreSQL
psql -h localhost -U postgres -c "DROP DATABASE IF EXISTS dotnetcloud_dev;"
psql -h localhost -U postgres -c "CREATE DATABASE dotnetcloud_dev OWNER dotnetcloud;"

# SQL Server (equivalent)
sqlcmd -S localhost -U sa -P '...' -Q "DROP DATABASE IF EXISTS dotnetcloud_dev; CREATE DATABASE dotnetcloud_dev;"
```

After recreation, only core migrations are applied (see Phase 3). Module schemas are created lazily when modules are installed.

---

### Phase 2 — Schema enforcement in naming strategies (PostgreSQL + SQL Server + MariaDB)

#### 2.1 `src/Core/DotNetCloud.Core.Data/Naming/PostgreSqlNamingStrategy.cs`

```csharp
public string? GetSchemaForModule(string moduleName)
{
    return RequiredModules.GetSchemaName(moduleName);
}
```

#### 2.2 `src/Core/DotNetCloud.Core.Data/Naming/SqlServerNamingStrategy.cs`

```csharp
public string? GetSchemaForModule(string moduleName)
{
    return RequiredModules.GetSchemaName(moduleName);
}
```

#### 2.3 `src/Core/DotNetCloud.Core.Data/Naming/MariaDbNamingStrategy.cs`

MariaDB has no schemas — it uses table prefixes. Update `GetTableName`:

```csharp
public string GetTableName(string entityName, string moduleName)
{
    var prefix = RequiredModules.GetSchemaName(moduleName); // returns "core" or module short name
    return $"{prefix}_{entityName.ToSnakeCase()}";
}
```

#### 2.4-2.14 Update each module DbContext (11 files)

Every module DbContext:

1. Adds `using DotNetCloud.Core.Data.Naming;`
2. Adds constructor parameter `ITableNamingStrategy namingStrategy`, stored in `_namingStrategy` field
3. In `OnModelCreating`, adds `HasDefaultSchema(_namingStrategy.GetSchemaForModule("shortName"))` before `base.OnModelCreating`

The short name is the module suffix (e.g., `"files"`, `"chat"`). RequiredModules handles the lookup.

| # | File | Short name | Result schema |
|---|---|---|---|
| 2.4 | `src/Modules/Files/...Data/FilesDbContext.cs` | `"files"` | `core` |
| 2.5 | `src/Modules/Chat/...Data/ChatDbContext.cs` | `"chat"` | `core` |
| 2.6 | `src/Modules/Search/...Data/SearchDbContext.cs` | `"search"` | `core` |
| 2.7 | `src/Modules/Contacts/...Data/ContactsDbContext.cs` | `"contacts"` | `contacts` |
| 2.8 | `src/Modules/Calendar/...Data/CalendarDbContext.cs` | `"calendar"` | `calendar` |
| 2.9 | `src/Modules/Notes/...Data/NotesDbContext.cs` | `"notes"` | `notes` |
| 2.10 | `src/Modules/Tracks/...Data/TracksDbContext.cs` | `"tracks"` | `tracks` |
| 2.11 | `src/Modules/Photos/...Data/PhotosDbContext.cs` | `"photos"` | `photos` |
| 2.12 | `src/Modules/Music/...Data/MusicDbContext.cs` | `"music"` | `music` |
| 2.13 | `src/Modules/Video/...Data/VideoDbContext.cs` | `"video"` | `video` |
| 2.14 | `src/Modules/AI/...Data/AiDbContext.cs` | `"ai"` | `ai` |

For DbContexts that already hardcode `HasDefaultSchema` (Photos, Music, Video), replace the hardcoded string with the strategy call. For all others, add the call.

---

### Phase 3 — Lazy schema creation

This is the key architectural change: module schemas are only created when a module is installed, not unconditionally on startup.

#### 3.0 Design for third-party modules

First-party modules (shipped with DotNetCloud) have their DbContext types known at compile time — the core can resolve and migrate them directly. Third-party modules are separate processes with DbContexts the core does not reference. They need a different path.

Two schema management strategies:

| Strategy | Who migrates | Used by |
|----------|-------------|---------|
| **Core-managed** | Core server resolves the module's DbContext from DI and calls `MigrateAsync` | First-party modules |
| **Self-managed** | The module process connects to the database and runs its own migrations on startup | Third-party modules |

The module's `manifest.json` declares its strategy via a `schemaProvider` field:

```json
{
  "id": "dotnetcloud.files",
  "schemaProvider": "core"
}
```

```json
{
  "id": "org.example.customapp",
  "schemaProvider": "self"
}
```

If omitted, `schemaProvider` defaults to `"self"` (safe default for unknown modules).

#### 3.1 `IModuleSchemaProvider` interface

New file: `src/Core/DotNetCloud.Core/Modules/IModuleSchemaProvider.cs`

```csharp
namespace DotNetCloud.Core.Modules;

/// <summary>
/// Creates or migrates database schemas for a module.
/// Implementations handle both first-party (core-driven) and
/// third-party (self-managed) schema strategies.
/// </summary>
public interface IModuleSchemaProvider
{
    /// <summary>
    /// Ensures the module's database schema exists and is up to date.
    /// For core-managed modules this runs EF migrations.
    /// For self-managed modules this is a no-op (the module process handles it).
    /// </summary>
    Task EnsureSchemaAsync(string moduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops the module's database schema. Only applicable to core-managed modules.
    /// </summary>
    Task DropSchemaAsync(string moduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if this module's schema is managed by the core server
    /// (as opposed to being self-managed by the module process).
    /// </summary>
    bool IsCoreManaged(string moduleId);
}
```

#### 3.2 `ModuleManifestData` — add `SchemaProvider` field

In `src/Core/DotNetCloud.Core.Server/ModuleLoading/ModuleManifestLoader.cs`, add to `ModuleManifestData`:

```csharp
/// <summary>
/// How the module's database schema is managed.
/// "core" = core server runs migrations. "self" = module process self-migrates.
/// Defaults to "self" when absent.
/// </summary>
public string SchemaProvider { get; init; } = "self";
```

#### 3.3 `DbContextSchemaProvider` (first-party modules)

New file: `src/Core/DotNetCloud.Core.Data/Services/DbContextSchemaProvider.cs`

```csharp
namespace DotNetCloud.Core.Data.Services;

/// <summary>
/// Core-managed schema provider. Resolves a module's DbContext from DI and
/// applies EF migrations. Used by first-party modules whose DbContext types
/// are known at compile time.
/// </summary>
public class DbContextSchemaProvider : IModuleSchemaProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DbContextSchemaProvider> _logger;

    // Only first-party modules whose DbContext types are compiled into the core
    private static readonly Dictionary<string, Type> ModuleDbContextTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dotnetcloud.files"]     = typeof(FilesDbContext),
        ["dotnetcloud.chat"]      = typeof(ChatDbContext),
        ["dotnetcloud.search"]    = typeof(SearchDbContext),
        ["dotnetcloud.contacts"]  = typeof(ContactsDbContext),
        ["dotnetcloud.calendar"]  = typeof(CalendarDbContext),
        ["dotnetcloud.notes"]     = typeof(NotesDbContext),
        ["dotnetcloud.tracks"]    = typeof(TracksDbContext),
        ["dotnetcloud.photos"]    = typeof(PhotosDbContext),
        ["dotnetcloud.music"]     = typeof(MusicDbContext),
        ["dotnetcloud.video"]     = typeof(VideoDbContext),
        ["dotnetcloud.ai"]        = typeof(AiDbContext),
    };

    public DbContextSchemaProvider(IServiceProvider serviceProvider, ILogger<DbContextSchemaProvider> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public bool IsCoreManaged(string moduleId) => ModuleDbContextTypes.ContainsKey(moduleId);

    public async Task EnsureSchemaAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        if (!ModuleDbContextTypes.TryGetValue(moduleId, out var contextType))
            return; // not core-managed, silently skip

        var context = (DbContext)_serviceProvider.GetRequiredService(contextType);
        var creator = context.GetService<IRelationalDatabaseCreator>();

        if (await creator.ExistsAsync(cancellationToken))
        {
            var pending = await context.Database.GetPendingMigrationsAsync(cancellationToken);
            if (pending.Any())
            {
                _logger.LogInformation("Applying {Count} pending migrations for {ModuleId}",
                    pending.Count(), moduleId);
                await context.Database.MigrateAsync(cancellationToken);
            }
            return;
        }

        _logger.LogInformation("Creating schema for module {ModuleId}", moduleId);
        await context.Database.MigrateAsync(cancellationToken);
        _logger.LogInformation("Created schema for module {ModuleId}", moduleId);
    }

    public async Task DropSchemaAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        if (RequiredModules.IsRequired(moduleId))
            throw new InvalidOperationException($"Cannot drop schema for required module '{moduleId}'.");

        if (!ModuleDbContextTypes.TryGetValue(moduleId, out var contextType))
            return;

        var context = (DbContext)_serviceProvider.GetRequiredService(contextType);
        await context.Database.EnsureDeletedAsync(cancellationToken);
    }
}
```

#### 3.4 `SelfManagedSchemaProvider` (third-party modules)

New file: `src/Core/DotNetCloud.Core.Data/Services/SelfManagedSchemaProvider.cs`

```csharp
namespace DotNetCloud.Core.Data.Services;

/// <summary>
/// Schema provider for third-party modules that manage their own database schema.
/// The core server takes no action — the module process runs its own migrations on startup.
/// </summary>
public class SelfManagedSchemaProvider : IModuleSchemaProvider
{
    public bool IsCoreManaged(string moduleId) => false;

    public Task EnsureSchemaAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        // No-op: the module process handles its own schema on startup.
        // The core's job is just to start the process and pass it a connection string.
        return Task.CompletedTask;
    }

    public Task DropSchemaAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        // No-op: the module process is responsible for its own schema cleanup.
        // The uninstall flow handles file removal; the database schema is the module's concern.
        return Task.CompletedTask;
    }
}
```

#### 3.5 `ModuleSchemaService` — dispatch to the right provider

New file: `src/Core/DotNetCloud.Core.Data/Services/ModuleSchemaService.cs`

```csharp
namespace DotNetCloud.Core.Data.Services;

/// <summary>
/// Dispatches schema operations to the correct provider based on the module's
/// declared schema management strategy (core-managed vs self-managed).
/// </summary>
public class ModuleSchemaService
{
    private readonly DbContextSchemaProvider _coreManaged;
    private readonly SelfManagedSchemaProvider _selfManaged;
    private readonly ILogger<ModuleSchemaService> _logger;

    public ModuleSchemaService(
        DbContextSchemaProvider coreManaged,
        SelfManagedSchemaProvider selfManaged,
        ILogger<ModuleSchemaService> logger)
    {
        _coreManaged = coreManaged;
        _selfManaged = selfManaged;
        _logger = logger;
    }

    public async Task EnsureModuleSchemaAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        if (_coreManaged.IsCoreManaged(moduleId))
        {
            await _coreManaged.EnsureSchemaAsync(moduleId, cancellationToken);
        }
        else
        {
            _logger.LogInformation("Module {ModuleId} is self-managed; skipping core-driven schema creation", moduleId);
            await _selfManaged.EnsureSchemaAsync(moduleId, cancellationToken);
        }
    }

    public async Task DropModuleSchemaAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        if (_coreManaged.IsCoreManaged(moduleId))
        {
            await _coreManaged.DropSchemaAsync(moduleId, cancellationToken);
        }
        else
        {
            _logger.LogInformation("Module {ModuleId} is self-managed; skipping core-driven schema drop", moduleId);
        }
    }
}
```

#### 3.6 DI registration

In `src/Core/DotNetCloud.Core.Data/Extensions/DataServiceExtensions.cs` (or core server `Program.cs`):

```csharp
builder.Services.AddSingleton<DbContextSchemaProvider>();
builder.Services.AddSingleton<SelfManagedSchemaProvider>();
builder.Services.AddSingleton<ModuleSchemaService>();
```

#### 3.7 How third-party module processes self-migrate

Third-party modules receive the database connection string from the core server via an environment variable (`DOTNETCLOUD_CONNECTION_STRING`) set when the process is launched. On startup, the module:

```csharp
// Third-party module Host Program.cs pattern:
var connectionString = Environment.GetEnvironmentVariable("DOTNETCLOUD_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<MyModuleDbContext>(opts =>
{
    opts.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
        "__EFMigrationsHistory", "my_module_schema"));
});

var app = builder.Build();

// Self-migrate on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MyModuleDbContext>();
    await db.Database.MigrateAsync();
}
```

The core server's `ProcessSupervisor` sets the `DOTNETCLOUD_CONNECTION_STRING` environment variable when launching module processes, so third-party modules get it automatically.

#### 3.8 First-party module manifest.json updates

Each first-party module's `manifest.json` gets `"schemaProvider": "core"`:

```json
{
  "id": "dotnetcloud.files",
  "name": "Files",
  "version": "1.0.0",
  "schemaProvider": "core",
  ...
}
```

#### 3.9 Core server `DbInitializer` — only migrate installed modules

In `src/Core/DotNetCloud.Core.Server/Program.cs`, modify `InitializeDatabaseAsync`:

```csharp
// After core migrations, check which modules are installed
var installedModules = await dbContext.InstalledModules
    .Where(m => m.Status == "Enabled" || m.Status == "Installing")
    .Select(m => m.ModuleId)
    .ToListAsync(cancellationToken);

// Only migrate installed modules
if (installedModules.Contains("dotnetcloud.files"))
    await MigrateFilesAsync(...);
if (installedModules.Contains("dotnetcloud.chat"))
    await MigrateChatAsync(...);
// ... etc for all modules
```

On a fresh database, `InstalledModules` is empty, so only core tables are created. Module schemas appear as modules are installed.

#### 3.10 `ModuleUiRegistrationHostedService.SeedKnownModulesAsync` — trigger schema creation

When seeding a new `InstalledModule` record, also create the schema:

```csharp
var isNew = !existingIds.Contains(descriptor.ModuleId);
if (isNew)
{
    dbContext.InstalledModules.Add(new InstalledModule
    {
        ModuleId = descriptor.ModuleId,
        Version = "1.0.0",
        Status = "Enabled",
        InstalledAt = DateTime.UtcNow,
        IsRequired = RequiredModules.IsRequired(descriptor.ModuleId)
    });
    await dbContext.SaveChangesAsync(cancellationToken);

    // Create the module's database schema
    var schemaService = scope.ServiceProvider.GetRequiredService<ModuleSchemaService>();
    await schemaService.EnsureModuleSchemaAsync(descriptor.ModuleId, cancellationToken);
}
```

#### 3.11 `SetupCommand.SyncEnabledModulesToDatabaseAsync` — trigger schema creation

When enabling a module that wasn't previously enabled, create its schema:

```csharp
foreach (var moduleId in selectedModules)
{
    if (!installedById.TryGetValue(moduleId, out var installed))
    {
        dbContext.InstalledModules.Add(new InstalledModule
        {
            ModuleId = moduleId,
            Version = "1.0.0",
            Status = "Enabled",
            InstalledAt = DateTime.UtcNow,
            IsRequired = RequiredModules.IsRequired(moduleId)
        });

        // Schedule schema creation
        modulesNeedingSchema.Add(moduleId);
    }
    // ...
}

await dbContext.SaveChangesAsync();

// Create schemas for newly enabled modules
var schemaService = scope.ServiceProvider.GetRequiredService<ModuleSchemaService>();
foreach (var moduleId in modulesNeedingSchema)
{
    await schemaService.EnsureModuleSchemaAsync(moduleId, CancellationToken.None);
}
```

#### 3.12 CLI `ModuleCommands.InstallModuleAsync` — trigger schema creation

When installing a module via CLI, create its schema after the DB record.

#### 3.13 Uninstall — drop module schema

When a module is uninstalled, drop its schema via `ModuleSchemaService.DropModuleSchemaAsync`. Required modules are rejected. Self-managed modules are skipped (the module process handles its own cleanup).

```csharp
var schemaService = scope.ServiceProvider.GetRequiredService<ModuleSchemaService>();
await schemaService.DropModuleSchemaAsync(moduleId, cancellationToken);
```

---

### Phase 4 — Seeding and DTO mapping

#### 4.1 `ModuleUiRegistrationHostedService.SeedKnownModulesAsync`

Already covered in 3.3 — sets `IsRequired` and triggers schema creation.

#### 4.2 `AdminModuleService.MapToDto`

Add `IsRequired = entity.IsRequired` to the mapping.

---

### Phase 5 — Enforcement in API, CLI, and supervisor

#### 5.1 `AdminModuleService.StopModuleAsync`

```csharp
if (module.IsRequired)
{
    throw new InvalidOperationException(
        $"Module '{moduleId}' is architecturally required and cannot be stopped.");
}
```

#### 5.2 `AdminController.StopModuleAsync`

Catch `InvalidOperationException`, return 400:

```csharp
catch (InvalidOperationException ex)
{
    return BadRequest(new { success = false, error = new { code = "MODULE_REQUIRED", message = ex.Message } });
}
```

#### 5.3 `ProcessSupervisor.SyncDiscoveredModulesToDatabaseAsync`

Set `IsRequired` on new records:

```csharp
IsRequired = RequiredModules.IsRequired(module.ModuleId)
```

#### 5.4 `CLI ModuleCommands`

Guard stop: `if (newStatus == "Disabled" && RequiredModules.IsRequired(moduleId))` -> error.
Guard uninstall: `if (RequiredModules.IsRequired(moduleId))` -> error.

#### 5.5 `SetupCommand.cs`

Three changes:
- `requiredModules` array -> `RequiredModules.ModuleIds`
- User message updated to use `string.Join`
- `SyncEnabledModulesToDatabaseAsync` skips disabling required modules

---

### Phase 6 — install.sh

The install script delegates database setup to `dotnetcloud setup`. The changes to `setup` handle everything. However, two areas in install.sh need verification:

1. **`post_upgrade()` (line 646)**: Calls `dotnetcloud setup --migrate-only`. This must continue to work — it should migrate the core DB and any installed module DBs.

2. **Fresh install flow (line 1212)**: Calls `dotnetcloud setup --beginner`. The setup wizard now creates module schemas lazily during `SyncEnabledModulesToDatabaseAsync`. No install.sh changes needed — the wizard handles it.

3. **New consideration**: On upgrade, if a previously-uninstalled module is now installed (or vice versa), the migrate-only step should handle it. The `--migrate-only` path needs to use the same `ModuleSchemaService` logic.

No structural changes to install.sh are required. The `dotnetcloud setup` and `dotnetcloud setup --migrate-only` commands carry the new behavior.

---

## Database Reset Commands

After all code changes, reset the database:

```bash
# PostgreSQL
psql -h localhost -U postgres -c "DROP DATABASE IF EXISTS dotnetcloud_dev;"
psql -h localhost -U postgres -c "CREATE DATABASE dotnetcloud_dev OWNER dotnetcloud;"

# Apply core migrations only (creates core.* schema with all core tables including InstalledModules)
dotnet ef database update \
    --project src/Core/DotNetCloud.Core.Data \
    --startup-project src/Core/DotNetCloud.Core.Server \
    --context CoreDbContext

# Module schemas are created lazily — when setup wizard runs or modules are installed via CLI.
# After running `dotnetcloud setup`, the database will have:
#
#   core      — Core, Files, Chat, Search
#   contacts  — Contacts (if enabled)
#   calendar  — Calendar (if enabled)
#   notes     — Notes (if enabled)
#   tracks    — Tracks (if enabled)
#   photos    — Photos (if enabled)
#   music     — Music (if enabled)
#   video     — Video (if enabled)
#   ai        — AI (if enabled)
```

For SQL Server, same logic — only the connection string and `sqlcmd` syntax differ. The `ITableNamingStrategy` handles schema naming identically across both providers.

**Important**: Before dropping the database, stop all dotnetcloud processes to prevent them from writing errors to the logs while the database is unavailable:

```bash
sudo systemctl stop dotnetcloud.service
# Also stop any running module host processes
pkill -f "dotnetcloud.example" 2>/dev/null || true
pkill -f "DotNetCloud.Modules" 2>/dev/null || true
```

---

### Phase 7 — Update Example Module as third-party reference

The Example module (`src/Modules/Example/`) is the reference implementation for third-party module developers. It must demonstrate the new patterns: self-managed schema, `ITableNamingStrategy` usage, and schema creation on startup.

#### 7.1 `manifest.json` — add `schemaProvider`

```json
{
  "id": "dotnetcloud.example",
  "name": "Example",
  "version": "1.0.0",
  "schemaProvider": "self",
  ...
}
```

`schemaProvider: "self"` tells the core server: "this module manages its own database schema, do not try to migrate it." This is the correct setting for third-party modules.

#### 7.2 `ExampleDbContext` — add schema via `ITableNamingStrategy`

Currently `ExampleDbContext` does not set a schema. Update it to inject `ITableNamingStrategy` and call `HasDefaultSchema`:

```csharp
using DotNetCloud.Core.Data.Naming;

public class ExampleDbContext : DbContext
{
    private readonly ITableNamingStrategy _namingStrategy;

    public ExampleDbContext(DbContextOptions<ExampleDbContext> options, ITableNamingStrategy namingStrategy)
        : base(options)
    {
        _namingStrategy = namingStrategy;
    }

    public DbSet<ExampleNote> Notes => Set<ExampleNote>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(_namingStrategy.GetSchemaForModule("example"));
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new ExampleNoteConfiguration());
    }
}
```

`GetSchemaForModule("example")` returns `"example"` (not required, so gets its own schema).

#### 7.3 `Program.cs` — self-migrate on startup, register `ITableNamingStrategy`

Update the module Host `Program.cs` to demonstrate the self-managed pattern:

```csharp
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Modules.Example;
using DotNetCloud.Modules.Example.Data;
using DotNetCloud.Modules.Example.Host.Services;
using Microsoft.EntityFrameworkCore;

public static partial class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Register the Example module as a singleton
        builder.Services.AddSingleton<ExampleModule>();

        // Register ITableNamingStrategy (required by ExampleDbContext)
        builder.Services.AddSingleton<ITableNamingStrategy, PostgreSqlNamingStrategy>();

        // Get connection string from environment (set by core server) or config
        var connectionString = Environment.GetEnvironmentVariable("DOTNETCLOUD_CONNECTION_STRING")
            ?? builder.Configuration.GetConnectionString("DefaultConnection");

        // Register EF Core — use in-memory for development, real DB when connection string is provided
        if (string.IsNullOrEmpty(connectionString))
        {
            builder.Services.AddDbContext<ExampleDbContext>(options =>
                options.UseInMemoryDatabase("ExampleModule"));
        }
        else
        {
            builder.Services.AddDbContext<ExampleDbContext>(options =>
                options.UseNpgsql(connectionString, npgsql =>
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "example")));
        }

        // gRPC + health checks
        builder.Services.AddGrpc();
        builder.Services.AddHealthChecks().AddCheck<ExampleHealthCheck>("example_module");

        var app = builder.Build();

        // Self-migrate on startup when using a real database
        if (!string.IsNullOrEmpty(connectionString))
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ExampleDbContext>();
            await db.Database.MigrateAsync();
        }

        app.MapGrpcService<ExampleGrpcService>();
        app.MapGrpcService<ExampleLifecycleService>();
        app.MapHealthChecks("/health");
        app.MapGet("/", () => Results.Ok(new { module = "dotnetcloud.example", version = "1.0.0", status = "running" }));

        await app.RunAsync();
    }
}
```

Key changes from current:
- `Main` becomes `async Task Main` (needed for `MigrateAsync` / `RunAsync`)
- Registers `ITableNamingStrategy` in DI
- Reads `DOTNETCLOUD_CONNECTION_STRING` environment variable (set by core server's `ProcessSupervisor`)
- Falls back to in-memory database when no connection string (dev mode)
- Calls `MigrateAsync()` on startup when using a real database — this creates/migrates the `example` schema lazily
- Migration history table is scoped to the `example` schema to avoid collisions

#### 7.4 `README.md` — document the new patterns

Update the Example module's README to include a section on database schema management:

- Explain `schemaProvider: "self"` in manifest.json
- Explain `ITableNamingStrategy` injection and `HasDefaultSchema`
- Explain the `DOTNETCLOUD_CONNECTION_STRING` environment variable
- Explain the self-migrate pattern in `Program.cs`
- Show the in-memory fallback for development vs real database for production

#### 7.5 Add EF migration for Example module

Since the Example module currently has no migrations (it uses in-memory and `EnsureCreated`-style creation), add an initial migration so the self-migrate pattern works:

```bash
dotnet ef migrations add InitialCreate \
    --project src/Modules/Example/DotNetCloud.Modules.Example.Data \
    --startup-project src/Modules/Example/DotNetCloud.Modules.Example.Host \
    --context ExampleDbContext
```

## Order of Operations

| Step | What | Files |
|------|------|-------|
| 1 | Create `RequiredModules` class | `DotNetCloud.Core/Modules/RequiredModules.cs` (new) |
| 2 | Add `IsRequired` to entity | `InstalledModule.cs` |
| 3 | Add EF configuration | `InstalledModuleConfiguration.cs` |
| 4 | Generate EF migration for CoreDbContext | `dotnet ef migrations add` |
| 5 | Add `IsRequired` to `ModuleDto` | `ModuleDtos.cs` |
| 6 | Update naming strategies (3 files) | `PostgreSqlNamingStrategy.cs`, `SqlServerNamingStrategy.cs`, `MariaDbNamingStrategy.cs` |
| 7 | Update all module DbContexts (11 files) | Each module's `*DbContext.cs` |
| 8 | Create `ModuleSchemaService` | `DotNetCloud.Core.Data/Services/ModuleSchemaService.cs` (new) |
| 9 | Register `ModuleSchemaService` in DI | `DataServiceExtensions.cs` or core `Program.cs` |
| 10 | Gate core server `DbInitializer` on installed modules | Core server `Program.cs` |
| 11 | Trigger schema creation in `SeedKnownModulesAsync` | `ModuleUiRegistrationHostedService.cs` |
| 12 | Trigger schema creation in `SyncEnabledModulesToDatabaseAsync` | `SetupCommand.cs` |
| 13 | Trigger schema creation in CLI `InstallModuleAsync` | `ModuleCommands.cs` |
| 14 | Map `IsRequired` in DTO | `AdminModuleService.cs` |
| 15 | Guard `StopModuleAsync` | `AdminModuleService.cs` |
| 16 | Guard controller endpoint | `AdminController.cs` |
| 17 | Guard CLI stop/uninstall | `ModuleCommands.cs` |
| 18 | Set `IsRequired` in supervisor sync | `ProcessSupervisor.cs` |
| 19 | Replace hardcoded required array | `SetupCommand.cs` |
| 20 | Guard setup sync from disabling required | `SetupCommand.cs` |
| 21 | Verify install.sh needs no changes | `tools/install.sh` |
| 22 | Stop all dotnetcloud processes | `systemctl stop` + `pkill` |
| 23 | Drop and recreate database | `dropdb` + `createdb` + core migration only |
| 24 | Update Example module manifest.json | `manifest.json` — add `schemaProvider: "self"` |
| 25 | Update ExampleDbContext | `ExampleDbContext.cs` — inject `ITableNamingStrategy`, add `HasDefaultSchema` |
| 26 | Update Example Host Program.cs | `Program.cs` — self-migrate pattern, env var connection string |
| 27 | Update Example README.md | `README.md` — document schema management patterns |
| 28 | Add initial EF migration for Example | `dotnet ef migrations add InitialCreate` |

---

## Verification

### Build

```bash
dotnet build DotNetCloud.CI.slnf
```

### Tests

```bash
dotnet test
```

### Manual verification

1. **Fresh DB**: After drop + create + core migration, verify only `core` schema exists.
2. **Run setup**: `dotnetcloud setup --beginner` — enables files + chat. Verify `core` schema now has Files and Chat tables alongside core tables. Verify no other schemas exist.
3. **Enable optional module**: Via admin API or CLI, enable contacts. Verify `contacts` schema is created.
4. **Disable optional module**: Disable contacts. Verify contacts schema remains (data preserved, just disabled).
5. **Try to disable required module**: `POST .../dotnetcloud.files/stop` returns 400 `MODULE_REQUIRED`.
6. **IsRequired in DB**: `SELECT "ModuleId", "IsRequired" FROM core."InstalledModules"` — files/chat/search are true.
7. **SQL Server verification**: Repeat with SQL Server provider — schemas use `[core]`, `[contacts]`, etc. with `[schema].[table]` naming.
