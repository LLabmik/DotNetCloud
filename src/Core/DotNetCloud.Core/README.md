# DotNetCloud.Core

> **Purpose:** Core abstractions, interfaces, and data transfer objects for the DotNetCloud platform  
> **Status:** Production  
> **Audience:** Module developers, core contributors

---

## Overview

`DotNetCloud.Core` provides the foundational abstractions and interfaces that all DotNetCloud modules build upon. It defines the contracts for:

- **Capabilities System** — Permission framework for secure module access
- **Module System** — Plugin lifecycle and manifest contracts
- **Event System** — Pub/sub communication between modules
- **Authorization** — Caller context and identity

## Quick Start for Module Developers

### 1. Implement IModule

```csharp
using DotNetCloud.Core.Modules;

public class MyModule : IModule
{
    private readonly IEventBus _eventBus;
    
    public IModuleManifest Manifest => new MyModuleManifest();
    
    public MyModule(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }
    
    public async Task InitializeAsync(ModuleInitializationContext context, CancellationToken cancellationToken)
    {
        // Validate capabilities, load config, subscribe to events
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Begin accepting requests
    }
    
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // Graceful shutdown
    }
}
```

### 2. Create Your Module Manifest

```csharp
using DotNetCloud.Core.Modules;

public class MyModuleManifest : IModuleManifest
{
    public string Id => "myorg.mymodule";
    public string Name => "My Module";
    public string Version => "1.0.0";
    
    public IReadOnlyCollection<string> RequiredCapabilities => new[]
    {
        nameof(IEventBus),
        nameof(IUserDirectory)
    };
    
    public IReadOnlyCollection<string> PublishedEvents => new[]
    {
        nameof(MyEvent)
    };
    
    public IReadOnlyCollection<string> SubscribedEvents => new[]
    {
        nameof(UserDeletedEvent)
    };
}
```

### 3. Define Domain Events

```csharp
using DotNetCloud.Core.Events;

/// Published when something important happens in your module
public sealed record MyEvent : IEvent
{
    public required Guid EventId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required Guid SomethingId { get; init; }
}
```

### 4. Handle Events

```csharp
using DotNetCloud.Core.Events;

public class UserDeletedEventHandler : IEventHandler<UserDeletedEvent>
{
    public async Task HandleAsync(UserDeletedEvent @event, CancellationToken cancellationToken)
    {
        // React to user deletion
        // Clean up user data in your module
    }
}
```

### 5. Use Capabilities

```csharp
public class MyService
{
    private readonly IUserDirectory _userDir;
    private readonly IEventBus _eventBus;
    
    public MyService(IUserDirectory userDir, IEventBus eventBus)
    {
        _userDir = userDir;
        _eventBus = eventBus;
    }
    
    public async Task DoSomethingAsync(CallerContext caller, CancellationToken ct)
    {
        // Verify caller permission
        if (!caller.HasRole("user"))
            throw new UnauthorizedException("User role required");
        
        // Use capabilities
        var user = await _userDir.GetUserAsync(caller.UserId, ct);
        
        // Publish event
        await _eventBus.PublishAsync(
            new MyEvent { /* ... */ },
            caller,
            ct);
    }
}
```

---

## Core Abstractions

### Capability System

Capabilities define what operations a module can perform. They're organized into tiers:

- **Public** — Automatically granted (e.g., `IEventBus`, `INotificationService`)
- **Restricted** — Admin approval required (e.g., `IStorageProvider`)
- **Privileged** — Security review required (e.g., `IUserManager`)
- **Forbidden** — Never granted (e.g., direct database access)

**Key Types:**
- `ICapabilityInterface` — Marker interface for capabilities
- `CapabilityTier` — Enum for approval sensitivity

**Example:**
```csharp
public interface IStorageProvider : ICapabilityInterface
{
    Task UploadAsync(string path, Stream content, CancellationToken cancellationToken);
    Task<Stream> DownloadAsync(string path, CancellationToken cancellationToken);
}
```

### Module System

Modules are plugin-style components with a managed lifecycle.

**Lifecycle:** Initialize → Start → Running → Stop → Stopped

**Key Types:**
- `IModule` — Core module interface
- `IModuleManifest` — Module metadata
- `ModuleInitializationContext` — Context passed during initialization

**Key Methods:**
- `InitializeAsync()` — Set up module (called once at startup)
- `StartAsync()` — Begin accepting work (called after all modules initialized)
- `StopAsync()` — Graceful shutdown (called during system shutdown)

### Event System

Modules communicate through domain events, enabling loose coupling.

**Key Types:**
- `IEvent` — Base event interface
- `IEventHandler<TEvent>` — Event handler interface
- `IEventBus` — Publish/subscribe implementation

**Pattern:**
1. Module publishes event: `await _eventBus.PublishAsync(event, caller, cancellationToken)`
2. Other modules subscribe: `await _eventBus.SubscribeAsync(handler, cancellationToken)`
3. Handlers react: `public Task HandleAsync(MyEvent @event, CancellationToken cancellationToken)`

### Authorization

Every operation requires a `CallerContext` to identify who's making the request.

**Key Types:**
- `CallerContext` — Caller identity, type, and roles
- `CallerType` — User, System, or Module

**Usage:**
```csharp
var userCaller = new CallerContext(
    userId: userId,
    roles: new[] { "admin", "user" },
    type: CallerType.User);

await _service.DoSomethingAsync(userCaller, cancellationToken);
```

---

## Key Interfaces Reference

### Capability Interfaces

| Interface | Tier | Purpose |
|-----------|------|---------|
| `IUserDirectory` | Public | Query user information |
| `ICurrentUserContext` | Public | Get current caller context |
| `INotificationService` | Public | Send notifications |
| `IEventBus` | Public | Publish/subscribe events |
| `IStorageProvider` | Restricted | File storage operations |
| `IModuleSettings` | Restricted | Module configuration |
| `ITeamDirectory` | Restricted | Team hierarchy access |
| `IUserManager` | Privileged | User account management |
| `IBackupProvider` | Privileged | Backup/restore operations |

See [ForbiddenInterfaces.md](./Capabilities/ForbiddenInterfaces.md) for capabilities that are never granted.

### DTOs (Data Transfer Objects)

Organized by domain:
- `UserDtos.cs` — User-related DTOs
- `OrganizationDtos.cs` — Organization DTOs
- `TeamDtos.cs` — Team DTOs
- `PermissionDtos.cs` — Permission DTOs
- `ModuleDtos.cs` — Module DTOs
- `DeviceDtos.cs` — Device DTOs
- `SettingsDtos.cs` — Settings DTOs

### Error Types

- `ApiErrorResponse` — Standard API error format
- `ErrorCodes` — Standard error code constants
- `DotNetCloudExceptions.cs` — Custom exception types:
  - `CapabilityNotGrantedException`
  - `ModuleNotFoundException`
  - `UnauthorizedException`
  - `ValidationException`

---

## Project Structure

```
src/Core/DotNetCloud.Core/
├── Authorization/              # Caller context and authorization
│   ├── CallerContext.cs
│   └── CallerType.cs
├── Capabilities/               # Capability interfaces (public/restricted/privileged)
│   ├── ICapabilityInterface.cs
│   ├── CapabilityTier.cs
│   ├── IEventBus.cs           # Publish/subscribe capability
│   ├── IUserDirectory.cs
│   ├── ICurrentUserContext.cs
│   ├── IStorageProvider.cs
│   ├── IUserManager.cs
│   └── ... (other capabilities)
├── Events/                     # Event system contracts
│   ├── IEvent.cs
│   ├── IEventHandler.cs
│   ├── IEventBus.cs
│   └── EventSubscription.cs
├── Modules/                    # Module lifecycle and manifest
│   ├── IModule.cs
│   ├── IModuleManifest.cs
│   ├── IModuleLifecycle.cs
│   └── ModuleInitializationContext.cs
├── DTOs/                       # Data transfer objects
│   ├── UserDtos.cs
│   ├── OrganizationDtos.cs
│   ├── TeamDtos.cs
│   └── ...
├── Errors/                     # Error handling
│   ├── ApiErrorResponse.cs
│   ├── ErrorCodes.cs
│   └── DotNetCloudExceptions.cs
└── README.md                   # This file
```

---

## Development Guidelines

### Creating New Capability Interfaces

1. **Define the interface:**
   ```csharp
   /// <summary>
   /// Your capability description.
   /// Tier: [Public|Restricted|Privileged]
   /// </summary>
   public interface IMyCapability : ICapabilityInterface
   {
       Task DoSomethingAsync(CancellationToken cancellationToken);
   }
   ```

2. **Add XML documentation** — Explain what the capability does, when to use it, examples

3. **Register in DI container** — Core will inject based on capability tier

4. **Declare in manifest** — Modules list capabilities they require

### Creating Domain Events

1. **Use records for immutability:**
   ```csharp
   public sealed record MyEvent : IEvent
   {
       public required Guid EventId { get; init; }
       public required DateTime CreatedAt { get; init; }
       // ... event-specific properties
   }
   ```

2. **Include EventId & CreatedAt** — Required by IEvent

3. **Use past-tense naming** — `UserCreatedEvent`, not `CreateUserEvent`

4. **Include only necessary data** — Reference large objects by ID

5. **Never include sensitive data** — Events are logged

### Best Practices

✅ **DO:**
- Use `nameof()` for type safety when declaring capabilities/events
- Include comprehensive XML documentation (///)
- Keep interfaces focused (single responsibility)
- Make implementations immutable (records, init-only properties)
- Provide clear examples in XML docs

❌ **DON'T:**
- Add implementation code to interfaces
- Use auto-generated code comments
- Include mutable state in DTOs/events
- Bypass capability checks
- Create circular capability dependencies

---

## Related Documentation

- **[Core Abstractions Guide](../../docs/architecture/core-abstractions.md)** — Detailed design patterns and examples
- **[Architecture Overview](../../docs/architecture/ARCHITECTURE.md)** — System-wide architecture
- **[Module Development Guide](../../docs/guides/MODULE_DEVELOPMENT.md)** — Step-by-step module creation

---

## Contributing

When adding new abstractions to DotNetCloud.Core:

1. **Add comprehensive XML documentation** — Use `///` comments with `<summary>`, `<remarks>`, `<example>`
2. **Include code examples** — Show how to use the new type
3. **Update this README** — Add new types to the reference section
4. **Add unit tests** — Test interfaces with mock implementations
5. **Follow naming conventions** — Interfaces start with `I`, events end with `Event`

See [CONTRIBUTING.md](../../CONTRIBUTING.md) for full guidelines.

---

## License

Part of DotNetCloud, licensed under AGPL-3.0. See [LICENSE](../../../LICENSE) for details.

---

**Last Updated:** 2026-03-02  
**Maintainers:** DotNetCloud Team
