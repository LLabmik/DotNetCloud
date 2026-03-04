# Module Development Guide

> **Purpose:** Guide for building DotNetCloud modules  
> **Audience:** First-party and third-party module developers  
> **Status:** Skeleton — will be expanded as the module ecosystem matures  
> **Last Updated:** 2026-03-03

---

## Table of Contents

1. [Overview](#overview)
2. [Module Architecture](#module-architecture)
3. [Creating a Module](#creating-a-module)
4. [Module Manifest](#module-manifest)
5. [Module Lifecycle](#module-lifecycle)
6. [Capability System](#capability-system)
7. [Event System](#event-system)
8. [Data Access](#data-access)
9. [gRPC Communication](#grpc-communication)
10. [Blazor UI Components](#blazor-ui-components)
11. [Testing](#testing)
12. [Reference Implementation](#reference-implementation)

---

## Overview

DotNetCloud modules are process-isolated plugins that extend the platform. Each module:

- Runs in its own process for crash isolation
- Communicates with the core server over gRPC (Unix sockets on Linux, Named Pipes on Windows)
- Declares its capabilities and events in a manifest
- Follows a strict lifecycle (Initialize → Start → Run → Stop → Dispose)
- Has its own database tables (isolated by schema or table prefix)
- Can provide Blazor UI components that load in the web dashboard

### Key Principles

- **Security by default:** Modules have no capabilities until explicitly granted
- **Process isolation:** A crash in one module does not affect others
- **Eat your own dog food:** First-party modules use the same API as third-party modules
- **Loose coupling:** Modules communicate through events, not direct references

---

## Module Architecture

A module typically consists of three projects:

```
src/Modules/MyModule/
├── DotNetCloud.Modules.MyModule/           # Core logic, manifest, models
│   ├── MyModuleManifest.cs
│   ├── MyModule.cs
│   ├── Models/
│   └── Events/
├── DotNetCloud.Modules.MyModule.Data/      # EF Core context and entities
│   ├── MyModuleDbContext.cs
│   └── Entities/
└── DotNetCloud.Modules.MyModule.Host/      # gRPC host, startup
    ├── Program.cs
    ├── Services/
    └── Protos/
```

| Project | Purpose | References |
|---|---|---|
| **Core** | Business logic, manifest, domain models, events | `DotNetCloud.Core` |
| **Data** | EF Core context, entities, migrations | `DotNetCloud.Core.Data` |
| **Host** | gRPC service host, entry point | Core + Data + `DotNetCloud.Core.Grpc` |

---

## Creating a Module

### Step 1: Create the Project Structure

```powershell
# Create module projects
dotnet new classlib -n DotNetCloud.Modules.MyModule -o src\Modules\MyModule\DotNetCloud.Modules.MyModule
dotnet new classlib -n DotNetCloud.Modules.MyModule.Data -o src\Modules\MyModule\DotNetCloud.Modules.MyModule.Data
dotnet new worker -n DotNetCloud.Modules.MyModule.Host -o src\Modules\MyModule\DotNetCloud.Modules.MyModule.Host

# Add to solution
dotnet sln add src\Modules\MyModule\DotNetCloud.Modules.MyModule\DotNetCloud.Modules.MyModule.csproj
dotnet sln add src\Modules\MyModule\DotNetCloud.Modules.MyModule.Data\DotNetCloud.Modules.MyModule.Data.csproj
dotnet sln add src\Modules\MyModule\DotNetCloud.Modules.MyModule.Host\DotNetCloud.Modules.MyModule.Host.csproj
```

### Step 2: Add Project References

```xml
<!-- DotNetCloud.Modules.MyModule.csproj -->
<ItemGroup>
  <ProjectReference Include="..\..\..\..\Core\DotNetCloud.Core\DotNetCloud.Core.csproj" />
</ItemGroup>
```

### Step 3: Create the Manifest

See [Module Manifest](#module-manifest) below.

### Step 4: Implement the Module Class

See [Module Lifecycle](#module-lifecycle) below.

---

## Module Manifest

Every module must provide a manifest that declares its identity, capabilities, and events.

### Interface

```csharp
public interface IModuleManifest
{
    string Id { get; }                                      // Unique ID (e.g., "dotnetcloud.mymodule")
    string Name { get; }                                    // Display name
    string Version { get; }                                 // Semantic version
    IReadOnlyCollection<string> RequiredCapabilities { get; }  // Capabilities needed from core
    IReadOnlyCollection<string> PublishedEvents { get; }       // Events this module emits
    IReadOnlyCollection<string> SubscribedEvents { get; }      // Events this module listens to
}
```

### Example

```csharp
public sealed class MyModuleManifest : IModuleManifest
{
    public string Id => "dotnetcloud.mymodule";
    public string Name => "My Module";
    public string Version => "1.0.0";

    public IReadOnlyCollection<string> RequiredCapabilities => new[]
    {
        nameof(INotificationService),   // Public tier — auto-granted
        nameof(IStorageProvider)         // Restricted tier — requires admin approval
    };

    public IReadOnlyCollection<string> PublishedEvents => new[]
    {
        nameof(ItemCreatedEvent),
        nameof(ItemDeletedEvent)
    };

    public IReadOnlyCollection<string> SubscribedEvents => [];
}
```

### Naming Conventions

- **Module ID:** Reverse domain notation (e.g., `dotnetcloud.files`, `dotnetcloud.chat`)
- **Version:** Semantic versioning (`Major.Minor.Patch`)

---

## Module Lifecycle

Modules implement `IModuleLifecycle` for full lifecycle control:

```
[Discovery] → [Loading] → [Initialize] → [Start] → [Running] → [Stop] → [Dispose]
```

### Interface

```csharp
public interface IModule
{
    IModuleManifest Manifest { get; }
    Task InitializeAsync(ModuleInitializationContext context, CancellationToken cancellationToken);
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}

public interface IModuleLifecycle : IModule, IAsyncDisposable
{
    new Task DisposeAsync();
}
```

### Lifecycle Guarantees

| Phase | Called | Purpose |
|---|---|---|
| `InitializeAsync` | Exactly once, before `StartAsync` | Set up DI, subscribe to events, load config |
| `StartAsync` | Exactly once, after all modules initialized | Begin accepting work |
| `StopAsync` | Exactly once, on shutdown | Drain active work, unsubscribe events |
| `DisposeAsync` | Exactly once, after `StopAsync` | Release resources |

### Initialization Context

`ModuleInitializationContext` provides everything a module needs during initialization:

```csharp
public record ModuleInitializationContext(
    string ModuleId,
    IServiceProvider Services,
    IReadOnlyDictionary<string, object> Configuration,
    CallerContext SystemCaller
);
```

**Usage:**

```csharp
public async Task InitializeAsync(ModuleInitializationContext context, CancellationToken cancellationToken)
{
    // Resolve services from DI
    var eventBus = context.Services.GetRequiredService<IEventBus>();
    var logger = context.Services.GetService<ILogger<MyModule>>();

    // Optional capabilities (null if not granted)
    var storage = context.Services.GetService<IStorageProvider>();
    if (storage is null)
    {
        logger?.LogWarning("IStorageProvider not granted — file features disabled");
    }

    // Load configuration
    if (context.Configuration.TryGetValue("max_items", out var maxObj))
    {
        _maxItems = Convert.ToInt32(maxObj);
    }

    // Subscribe to events
    await eventBus.SubscribeAsync(_myEventHandler, cancellationToken);
}
```

---

## Capability System

Capabilities are the permission model for modules. They control what platform features a module can access.

### Capability Tiers

| Tier | Approval | Examples |
|---|---|---|
| **Public** | Automatic | `IUserDirectory`, `ICurrentUserContext`, `INotificationService`, `IEventBus` |
| **Restricted** | Admin approval | `IStorageProvider`, `IModuleSettings`, `ITeamDirectory` |
| **Privileged** | Admin approval + warning | `IUserManager`, `IBackupProvider` |
| **Forbidden** | Never granted | Direct database access, process management |

### Requesting Capabilities

Declare required capabilities in your manifest:

```csharp
public IReadOnlyCollection<string> RequiredCapabilities => new[]
{
    nameof(INotificationService),  // Public — auto-granted
    nameof(IStorageProvider)       // Restricted — admin must approve
};
```

### Handling Missing Capabilities

If a capability is not granted, it is injected as `null`. Always check:

```csharp
var storage = context.Services.GetService<IStorageProvider>();
if (storage is null)
{
    // Degrade gracefully — disable file-dependent features
    _fileFeatureEnabled = false;
    return;
}
```

### Granting Capabilities

Admins grant capabilities via the admin dashboard or CLI:

```powershell
# Via CLI
dotnetcloud module grant dotnetcloud.mymodule IStorageProvider
```

Or via the API:

```
POST /api/v1/core/admin/modules/dotnetcloud.mymodule/capabilities/IStorageProvider/grant
```

---

## Event System

Modules communicate through a publish/subscribe event bus.

### Defining Events

Events implement the `IEvent` marker interface:

```csharp
public record ItemCreatedEvent(Guid ItemId, string Title, Guid CreatedByUserId) : IEvent;
public record ItemDeletedEvent(Guid ItemId, Guid DeletedByUserId) : IEvent;
```

### Publishing Events

```csharp
var @event = new ItemCreatedEvent(item.Id, item.Title, caller.UserId);
await _eventBus.PublishAsync(@event, caller);
```

### Subscribing to Events

Create an event handler:

```csharp
public sealed class ItemCreatedEventHandler : IEventHandler<ItemCreatedEvent>
{
    private readonly ILogger<ItemCreatedEventHandler> _logger;

    public ItemCreatedEventHandler(ILogger<ItemCreatedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(ItemCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Item created: {ItemId} - {Title}", @event.ItemId, @event.Title);
        return Task.CompletedTask;
    }
}
```

Register during initialization:

```csharp
await eventBus.SubscribeAsync(_itemCreatedHandler, cancellationToken);
```

Unsubscribe during stop:

```csharp
await eventBus.UnsubscribeAsync(_itemCreatedHandler, cancellationToken);
```

---

## Data Access

Each module owns its own `DbContext` with isolated tables.

### Schema Isolation

| Database | Strategy | Example |
|---|---|---|
| PostgreSQL | Separate schema | `mymodule.items` |
| SQL Server | Separate schema | `mymodule.Items` |
| MariaDB | Table prefix | `MyModule_Items` |

### Creating a Module DbContext

```csharp
public class MyModuleDbContext : DbContext
{
    public DbSet<MyItem> Items => Set<MyItem>();

    public MyModuleDbContext(DbContextOptions<MyModuleDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply table naming strategy for multi-database support
        modelBuilder.Entity<MyItem>(entity =>
        {
            entity.ToTable("Items", "mymodule");  // Schema-based
            entity.HasKey(e => e.Id);
        });
    }
}
```

---

## gRPC Communication

Modules communicate with the core server over gRPC.

### Defining a Service

Create a `.proto` file:

```protobuf
syntax = "proto3";

package dotnetcloud.modules.mymodule;

service MyModuleService {
  rpc GetItem (GetItemRequest) returns (GetItemResponse);
  rpc CreateItem (CreateItemRequest) returns (CreateItemResponse);
}

message GetItemRequest {
  string item_id = 1;
}

message GetItemResponse {
  string item_id = 1;
  string title = 2;
  string content = 3;
}
```

### Health Check

Every module must implement the gRPC health check service to be monitored by the core supervisor:

```csharp
public class HealthServiceImpl : Health.HealthBase
{
    public override Task<HealthCheckResponse> Check(
        HealthCheckRequest request,
        ServerCallContext context)
    {
        return Task.FromResult(new HealthCheckResponse
        {
            Status = HealthCheckResponse.Types.ServingStatus.Serving
        });
    }
}
```

---

## Blazor UI Components

Modules can provide Blazor components that are dynamically loaded into the web dashboard.

### Creating a Module Page

```razor
@page "/modules/mymodule"
@using DotNetCloud.Modules.MyModule

<h3>My Module</h3>

<p>This page is provided by the MyModule module.</p>
```

### Registering Navigation

Module navigation items are registered through the module plugin system and appear in the sidebar.

---

## Testing

### Test Project Structure

```
tests/DotNetCloud.Modules.MyModule.Tests/
├── MyModuleManifestTests.cs
├── MyModuleTests.cs
├── Events/
│   └── MyEventHandlerTests.cs
└── Models/
    └── MyItemTests.cs
```

### Key Test Areas

1. **Manifest tests:** Verify ID, Name, Version, capabilities, events
2. **Lifecycle tests:** Initialize → Start → Stop → Dispose
3. **Business logic tests:** CRUD operations, validation
4. **Event tests:** Publishing and handling events
5. **Error state tests:** Invalid input, uninitialized module, missing capabilities

### Example Test

```csharp
[TestClass]
public class MyModuleManifestTests
{
    [TestMethod]
    public void Id_ReturnsExpectedValue()
    {
        var manifest = new MyModuleManifest();
        Assert.AreEqual("dotnetcloud.mymodule", manifest.Id);
    }

    [TestMethod]
    public void RequiredCapabilities_ContainsExpectedCapabilities()
    {
        var manifest = new MyModuleManifest();
        CollectionAssert.Contains(
            manifest.RequiredCapabilities.ToList(),
            nameof(INotificationService));
    }
}
```

---

## Reference Implementation

The **Example Module** (`src/Modules/Example/DotNetCloud.Modules.Example`) is the canonical reference for module development. It demonstrates:

- ✅ `ExampleModuleManifest` — manifest with capabilities and events
- ✅ `ExampleModule` — full lifecycle implementation
- ✅ `ExampleNote` — domain model
- ✅ `NoteCreatedEvent` / `NoteDeletedEvent` — event definitions
- ✅ `NoteCreatedEventHandler` — event handler
- ✅ `ExampleDbContext` — module-scoped database context
- ✅ gRPC health check service
- ✅ Blazor UI components
- ✅ Comprehensive unit tests (51 tests)

Browse the source:

- Core: `src/Modules/Example/DotNetCloud.Modules.Example/`
- Data: `src/Modules/Example/DotNetCloud.Modules.Example.Data/`
- Host: `src/Modules/Example/DotNetCloud.Modules.Example.Host/`
- Tests: `tests/DotNetCloud.Modules.Example.Tests/`

---

**See also:**

- [Core Abstractions](../architecture/core-abstractions.md) — detailed capability and event system docs
- [Architecture Overview](../architecture/ARCHITECTURE.md) — system architecture and module communication
- [API Reference](../api/README.md) — admin endpoints for module management
