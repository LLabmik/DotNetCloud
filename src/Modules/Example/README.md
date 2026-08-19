# DotNetCloud Example Module

> Reference implementation of a DotNetCloud module

## Overview

This module serves as a **complete reference implementation** for building DotNetCloud modules. It demonstrates every major integration point: module lifecycle, capability usage, event publishing/subscription, gRPC services, EF Core data access, and Blazor UI components.

Use this module as a starting point when building your own modules.

## Project Structure

```
src/Modules/Example/
├── manifest.json                              # Module manifest (filesystem discovery)
├── DotNetCloud.Modules.Example/               # Core logic
│   ├── ExampleModule.cs                       # IModuleLifecycle implementation
│   ├── ExampleModuleManifest.cs               # IModuleManifest implementation
│   ├── Models/
│   │   └── ExampleNote.cs                     # Domain model
│   ├── Events/
│   │   ├── NoteCreatedEvent.cs                # Domain event
│   │   ├── NoteDeletedEvent.cs                # Domain event
│   │   └── NoteCreatedEventHandler.cs         # Event handler
│   └── UI/
│       ├── ExampleNotesPage.razor             # Main page component
│       ├── ExampleNoteForm.razor              # Create form component
│       └── ExampleNoteDisplay.razor           # Note card component
├── DotNetCloud.Modules.Example.Data/          # Data access layer
│   ├── ExampleDbContext.cs                    # Module-specific DbContext
│   └── Configuration/
│       └── ExampleNoteConfiguration.cs        # EF Core entity configuration
└── DotNetCloud.Modules.Example.Host/          # gRPC host process
    ├── Program.cs                             # Entry point
    ├── Protos/
    │   └── example_service.proto              # gRPC service definition
    └── Services/
        ├── ExampleGrpcService.cs              # gRPC CRUD service
        ├── ExampleLifecycleService.cs         # Lifecycle gRPC service
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
    public string Version => "1.0.0";
    public IReadOnlyCollection<string> RequiredCapabilities => new[]
    {
        nameof(INotificationService),
        nameof(IStorageProvider)
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

### 6. Blazor UI (`UI/`)

- Self-contained Razor components in the module assembly
- Loaded dynamically by the core web shell's module plugin system

## Database Schema Management

The Example module uses a **self-managed schema** pattern, which is the correct approach for all third-party modules. The module owns its database schema and migrations, independent of the DotNetCloud core.

### `schemaProvider: "self"`

The `manifest.json` declares `"schemaProvider": "self"`, which tells the core server: **"this module manages its own database schema — do not try to migrate it."** The core will skip this module during its schema management and will not attempt to create or migrate tables for it.

Optional (third-party) modules should always use `"schemaProvider": "self"`. Required modules (files, chat, search) share the `core` schema and are migrated by the core server.

### `ITableNamingStrategy` and Schema Naming

The `ExampleDbContext` injects `ITableNamingStrategy` and calls `HasDefaultSchema`:

```csharp
public ExampleDbContext(DbContextOptions<ExampleDbContext> options, ITableNamingStrategy namingStrategy)
    : base(options)
{
    _namingStrategy = namingStrategy;
}

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.HasDefaultSchema(_namingStrategy.GetSchemaForModule("example"));
    // ...
}
```

`GetSchemaForModule("example")` returns `"example"` — the Example module is not architecturally required, so it gets its own dedicated schema. This keeps the module's tables isolated from the core and other modules.

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

The migration history table is scoped to the `example` schema (`__EFMigrationsHistory` in the `example` schema) to avoid collisions with the core or other modules.

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
8. **Build your UI** in `UI/`
9. **Place the compiled output** in the `modules/` directory

## Capabilities Used

| Capability             | Tier       | Purpose                                        |
| ---------------------- | ---------- | ---------------------------------------------- |
| `INotificationService` | Public     | Send user notifications when notes are created |
| `IStorageProvider`     | Restricted | Store note attachments (future)                |

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
