# DotNetCloud Example Module

> Reference implementation of a DotNetCloud module

## Overview

This module serves as a **complete reference implementation** for building DotNetCloud modules. It demonstrates every major integration point: module lifecycle, capability usage, event publishing/subscription, gRPC services, EF Core data access, and a **web UI served from the module's own process** and framed by the core.

Use this module as a starting point when building your own modules.

## Project Structure

```
src/Modules/Example/
├── manifest.json                              # Module manifest (filesystem discovery)
├── DotNetCloud.Modules.Example/               # Core logic (plain class library)
│   ├── ExampleModule.cs                       # IModuleLifecycle implementation
│   ├── ExampleModuleManifest.cs               # IModuleManifest implementation
│   ├── Models/
│   │   └── ExampleNote.cs                     # Domain model
│   └── Events/
│       ├── NoteCreatedEvent.cs                # Domain event
│       ├── NoteDeletedEvent.cs                # Domain event
│       └── NoteCreatedEventHandler.cs         # Event handler
├── DotNetCloud.Modules.Example.Data/          # Data access layer
│   ├── ExampleDbContext.cs                    # Module-specific DbContext
│   └── Configuration/
│       └── ExampleNoteConfiguration.cs        # EF Core entity configuration
└── DotNetCloud.Modules.Example.Host/          # Host process (gRPC + its own web UI)
    ├── Program.cs                             # Entry point
    ├── wwwroot/                               # The module's own UI (served by this process)
    │   ├── index.html                         # Notes page (also the help panel)
    │   ├── dncModuleBridge.js                 # Copied from the SDK at build time
    │   └── assets/
    │       ├── example-ui.js                  # Calls this module's own JSON API
    │       └── example-ui.css
    ├── Protos/
    │   └── example_service.proto              # gRPC service definition
    ├── Ui/
    │   └── ExampleNotesApi.cs                 # JSON API behind the UI (/api/notes)
    └── Services/
        ├── ExampleGrpcService.cs              # gRPC CRUD service
        ├── ExampleLifecycleService.cs         # Lifecycle gRPC service
        ├── ExampleNotesService.cs             # Notes logic (owner-scoped)
        ├── ExampleUserErasureHandler.cs       # Account erasure (F12)
        └── ExampleHealthCheck.cs              # ASP.NET health check
```

## Key Concepts Demonstrated

### 1. Module Manifest (`ExampleModuleManifest.cs`)

Declares module identity, required capabilities, and event contracts:

```csharp
public sealed class ExampleModuleManifest : IModuleManifest
{
    public string Id => "dotnetcloud.example";
    public string Name => "Example";
    public string Version => "1.1.0";
    public IReadOnlyCollection<string> RequiredCapabilities => new[]
    {
        nameof(INotificationService),
        nameof(IAuditLogger)
    };
    public IReadOnlyCollection<string> PublishedEvents => new[]
    {
        nameof(NoteCreatedEvent),
        nameof(NoteDeletedEvent)
    };
    public IReadOnlyCollection<string> SubscribedEvents => [];
}
```

### 2. Module Lifecycle (`ExampleModule.cs`)

Implements `IModuleLifecycle` for full lifecycle control:

- **InitializeAsync**: Resolves capabilities, subscribes to events, loads configuration
- **StartAsync**: Begins accepting work
- **StopAsync**: Unsubscribes from events, drains in-flight work
- **DisposeAsync**: Releases resources

### 3. Domain Events (`Events/`)

Events follow these conventions:

- Implement `IEvent` with `EventId` and `CreatedAt`
- Use `sealed record` for immutability
- Past-tense naming (e.g., `NoteCreatedEvent`)

### 4. gRPC Services (`Host/Services/`)

- **ExampleGrpcService**: Module-specific CRUD operations
- **ExampleLifecycleService**: Core supervisor integration (Initialize/Start/Stop/HealthCheck/GetManifest)

### 5. Data Access (`Data/`)

- Module-owned `DbContext` (separate from `CoreDbContext`)
- Injects `ITableNamingStrategy` for provider-appropriate schema naming
- Self-managed schema with `HasDefaultSchema` for table isolation
- Standard EF Core entity configuration with fluent API

### 6. Module-hosted web UI (`Host/wwwroot` + `Host/Ui`)

- The module serves its **own** web UI from its own process (`UseDefaultFiles` + `UseStaticFiles`); the core reverse-proxies it at `/apps/dotnetcloud.example/` and frames it with `sandbox="allow-scripts allow-forms allow-popups allow-modals"` (no `allow-same-origin`).
- `manifest.json` declares `ui.type = "iframe"`, `ui.entryPath = "/"` and a Material-icon nav entry, so the shell registers the module without a line of core code.
- Every asset and API reference in `index.html` / `example-ui.js` is **relative**. The proxy strips `/apps/<moduleId>` before forwarding, so an absolute `/assets/...` would leave the module and hit a core route.
- `dncModuleBridge.js` comes from the SDK (`src/SDK/DotNetCloud.Modules.Sdk/wwwroot`) and is copied into `wwwroot` at build time — no npm, no bundler. It provides the `dnc:init`/`theme`/`ready`/`title`/`notify` handshake.
- The frame has an **opaque origin**: no cookies, no `localStorage`, no `window.top`. State lives in the module; identity arrives as the core-signed `dnc-user-assertion` that the proxy mints for the signed-in user.
- The UI calls this module's own JSON API (`GET/POST /api/notes`, `DELETE /api/notes/{id}`). The owner is always the verified assertion user — never a payload field — and another user's note answers `404`.

See `docs/modules/MODULE_UI_HOSTING.md` for the full contract.

## Database Schema Management

The Example module uses a **self-managed schema** pattern, which is the correct approach for all third-party modules. The module owns its database schema and migrations, independent of the DotNetCloud core.

### No `schemaProvider` field

The old `schemaProvider: "self"` field was removed from the manifest format (decision D5): schema mode is now derived from whether the module is required — required modules are migrated by the core into the shared `core` schema, every non-required module owns its own provisioned schema and self-migrates. There is nothing for the module to declare.

Optional (third-party) modules should always use `"schemaProvider": "self"`. Required modules (files, chat, search) share the `core` schema and are migrated by the core server.

### `ITableNamingStrategy`

The `ExampleDbContext` injects `ITableNamingStrategy` for provider-appropriate **table** naming and for the sequential-Guid column defaults:

```csharp
public ExampleDbContext(DbContextOptions<ExampleDbContext> options, ITableNamingStrategy namingStrategy)
    : base(options)
{
    _namingStrategy = namingStrategy;
}

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ApplyConfiguration(new ExampleNoteConfiguration());
    SequentialGuidConfigurationExtensions.ApplySequentialGuidDefaults(modelBuilder, _namingStrategy.Provider);
}
```

Do **not** call `HasDefaultSchema(...)` here — see the sandbox rule below.

### Connection String & Database Provider

The host loads the shared `config.json` from the `DOTNETCLOUD_CONFIG_DIR` environment variable (set by the core server's `ProcessSupervisor` when launching module processes). The database settings are read from the `connectionString` and `databaseProvider` keys (with `database:provider` as a fallback). A connection string and provider are **required** — the host throws `InvalidOperationException` at startup if either is missing. There is no in-memory fallback.

For local development, the connection string can also be provided via `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=dotnetcloud;Username=dotnetcloud;Password=..."
  },
  "databaseProvider": "PostgreSql"
}
```

### Self-Migration on Startup

Because a connection string is now mandatory, the module calls `MigrateAsync()` unconditionally at startup to ensure its schema is created and up-to-date:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ExampleDbContext>();
    await db.Database.MigrateAsync();
}
```

The migration history table is **schema-neutral** too: the provisioned connection string pins `search_path` to the module's own schema, so `__EFMigrationsHistory` is created there along with the module's tables.

### Dual-Provider Migrations

The module follows the canonical dual-provider pattern (PostgreSQL + SQL Server), mirroring the real modules:

- `DotNetCloud.Modules.Example.Data` — PostgreSQL migrations (`Migrations/`, namespace `DotNetCloud.Modules.Example.Data.Migrations`)
- `DotNetCloud.Modules.Example.Data.SqlServer` — SQL Server migrations (separate project, `Migrations/`)

Each provider's migrations live in their own assembly, so the runtime applies only the active
provider's migration set — no provider filtering is required.

Two design-time factories exist for the EF CLI (one per project):

- `ExampleDbContextFactory` — PostgreSQL (Npgsql)
- `ExampleDbContextSqlServerDesignTimeFactory` — SQL Server

## How to Create Your Own Module

1. **Copy this module** as a template
2. **Rename** all `Example` references to your module name
3. **Update the manifest** with your module's ID, capabilities, and events
4. **Add your domain models** in `Models/`
5. **Define your events** in `Events/`
6. **Create your DbContext** in `Data/`
7. **Implement your gRPC service** in `Host/`
8. **Build your UI** in `Host/wwwroot` and declare it in the manifest (`ui.type = "iframe"`, entry path, Material-icon nav items) — see `docs/modules/MODULE_UI_HOSTING.md`
9. **Package it** with `dotnetcloud-pack pack` (`.dncpkg`) and install it with `dotnetcloud module install --file`

## Capabilities Used

| Capability             | Tier   | Purpose                                                                                                       |
| ---------------------- | ------ | ------------------------------------------------------------------------------------------------------------- |
| `INotificationService` | Public | Notifies the acting user when a note is created through the module UI (gated by `notifications:send` consent) |
| `IAuditLogger`         | Public | Writes an audit entry for each note created or deleted                                                        |

Least privilege matters: an earlier revision also requested `IStorageProvider` while never touching file storage. A store submission is rejected for a capability it cannot justify, so the reference module asks only for what it uses. The matching consent scope (`notifications:send`) is declared in `userConsentScopes`; the core's F18 rule would reject a manifest that requested the capability without it.

## Events Published

| Event              | Description                      |
| ------------------ | -------------------------------- |
| `NoteCreatedEvent` | Fired when a new note is created |
| `NoteDeletedEvent` | Fired when a note is deleted     |

## Running Locally

The host project can be run standalone for development, but now requires database configuration (a connection string + provider). Provide it either via a `config.json` in `DOTNETCLOUD_CONFIG_DIR` or via `appsettings.json`:

```bash
# With a config.json available at DOTNETCLOUD_CONFIG_DIR/config.json:
DOTNETCLOUD_CONFIG_DIR=/path/to/config dotnet run --project src/Modules/Example/DotNetCloud.Modules.Example.Host

# Or via appsettings.json ConnectionStrings:DefaultConnection + databaseProvider:
dotnet run --project src/Modules/Example/DotNetCloud.Modules.Example.Host
```

If the database configuration is missing, the host throws `InvalidOperationException` at startup instead of booting with an empty in-memory database. The module self-migrates its schema on startup.

When it is running, the host serves:

| Path                  | What it is                                                                                      |
| --------------------- | ----------------------------------------------------------------------------------------------- |
| `/`                   | The module's own UI (`wwwroot/index.html`) — the document the core frames                       |
| `/assets/*`           | The UI's script and stylesheet                                                                  |
| `/dncModuleBridge.js` | The SDK's UI bridge (copied at build time)                                                      |
| `/api/notes`          | The UI's JSON API — **401** unless the request carries a valid core-signed `dnc-user-assertion` |
| `/api/info`           | Liveness/version probe (the old `/` JSON probe moved here so it cannot shadow the UI document)  |
| `/health`             | Health check                                                                                    |

### Sandbox rule: keep the model and migrations schema-neutral

A sandboxed module does **not** own the database: the core provisions a schema and a principal that owns only that schema. The connection string it passes pins `search_path` to that schema, so:

- Do **not** call `HasDefaultSchema(...)` in `OnModelCreating` and do **not** name a schema in a migration or in `MigrationsHistoryTable(...)`. Unqualified DDL lands in the module's own schema; naming one requires `CREATE` on the database and fails with PostgreSQL `42501 permission denied for database`.
- Naming the schema in the model also makes EF report pending model changes against the snapshot, so `MigrateAsync` refuses to run.
- Keep `ExampleDbContextModelTests` green: it asserts both that the model matches the snapshot and that the generated script contains no schema-qualified DDL.
