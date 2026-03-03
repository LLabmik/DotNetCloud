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
- Standard EF Core entity configuration with fluent API
- Works with PostgreSQL, SQL Server, and MariaDB

### 6. Blazor UI (`UI/`)

- Self-contained Razor components in the module assembly
- Loaded dynamically by the core web shell's module plugin system

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

| Capability | Tier | Purpose |
|---|---|---|
| `INotificationService` | Public | Send user notifications when notes are created |
| `IStorageProvider` | Restricted | Store note attachments (future) |

## Events Published

| Event | Description |
|---|---|
| `NoteCreatedEvent` | Fired when a new note is created |
| `NoteDeletedEvent` | Fired when a note is deleted |

## Running Locally

The host project can be run standalone for development:

```bash
dotnet run --project src/Modules/Example/DotNetCloud.Modules.Example.Host
```

The module uses an in-memory database by default. In production, the core server configures the real database connection.
