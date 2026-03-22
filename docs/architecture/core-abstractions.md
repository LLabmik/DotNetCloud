# Core Abstractions & Interfaces

> **Document Version:** 1.0  
> **Last Updated:** 2026-03-02  
> **Scope:** Capability System, Module System, Event System, and Authorization  
> **Audience:** Module developers, core contributors

---

## Table of Contents

1. [Capability System](#capability-system)
2. [Module System](#module-system)
3. [Event System](#event-system)
4. [Authorization & Caller Context](#authorization--caller-context)
5. [Putting It All Together](#putting-it-all-together)
6. [Best Practices](#best-practices)

---

## Capability System

### Overview

The capability system is DotNetCloud's permission and capability framework. It enables secure, fine-grained access control to sensitive operations, ensuring modules can only access features they've been explicitly granted.

### Design Philosophy

- **Principle of Least Privilege:** Modules start with no capabilities; capabilities must be explicitly granted
- **Tiered Approval:** Different capabilities require different approval levels (automatic, manual, forbidden)
- **Auditability:** All capability grants are tracked and auditable
- **Fail-Safe:** Missing capabilities are gracefully injected as `null`, allowing modules to degrade functionality

### Capability Tiers

Capabilities are organized into four tiers, each representing an approval sensitivity level:

#### Public Tier

**Approval:** Automatic  
**Examples:**
- `IUserDirectory` — Query user information
- `ICurrentUserContext` — Get current caller context
- `INotificationService` — Send notifications to users
- `IEventBus` — Publish/subscribe to events

**Use Case:** Safe operations that don't expose sensitive data or grant elevated privileges. These capabilities are granted to all modules automatically.

```csharp
public interface IUserDirectory : ICapabilityInterface
{
    /// Get basic, non-sensitive user information
    Task<UserDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken);
}
```

#### Restricted Tier

**Approval:** Manual (administrator review required)  
**Examples:**
- `IStorageProvider` — Access file storage system
- `IModuleSettings` — Read/write module configuration
- `ITeamDirectory` — Access team hierarchy

**Use Case:** Moderate-risk operations that access user data or system resources. Granted after an administrator reviews the module's source code and requirements.

```csharp
public interface IStorageProvider : ICapabilityInterface
{
    /// Upload a file to storage
    Task UploadAsync(string path, Stream content, CancellationToken cancellationToken);
    
    /// Download a file from storage
    Task<Stream> DownloadAsync(string path, CancellationToken cancellationToken);
}
```

#### Privileged Tier

**Approval:** Manual (security review + sensitive operations)  
**Examples:**
- `IUserManager` — Create/disable users, manage accounts
- `IBackupProvider` — Initiate backups, backup management

**Use Case:** Highly sensitive operations that affect system integrity or user accounts. Granted only after thorough security review and for trusted modules.

```csharp
public interface IUserManager : ICapabilityInterface
{
    /// Create a new user account
    Task<UserDto> CreateUserAsync(CreateUserDto dto, CancellationToken cancellationToken);
    
    /// Disable a user account
    Task DisableUserAsync(Guid userId, CancellationToken cancellationToken);
}
```

#### Forbidden Tier

**Approval:** Never  
**Examples:**
- Direct database access
- System-level file operations
- Process spawning
- Network bypass operations

**Use Case:** Operations that are fundamentally incompatible with the module security model. Core modules may expose safe subsets through specific capability interfaces.

See [ForbiddenInterfaces.md](../../src/Core/DotNetCloud.Core/Capabilities/ForbiddenInterfaces.md) for the complete forbidden list.

### Capability Enforcement

Capabilities are enforced through dependency injection. When a module requests a capability interface:

```csharp
public class MyModule : IModule
{
    private readonly IStorageProvider? _storage;
    private readonly IUserManager? _userManager;
    
    public MyModule(IStorageProvider? storage, IUserManager? userManager)
    {
        // If capability not granted, injected as null
        _storage = storage;
        _userManager = userManager;
    }
    
    public async Task InitializeAsync(ModuleInitializationContext context, CancellationToken cancellationToken)
    {
        // Gracefully degrade if capability missing
        if (_storage == null)
        {
            throw new InvalidOperationException("Storage capability required");
        }
        
        if (_userManager == null)
        {
            // Feature is optional; continue without it
            Console.WriteLine("User management capability not available");
        }
    }
}
```

### Implementing a Capability Interface

To create a new capability:

1. **Define the interface** — inherit from `ICapabilityInterface`:

```csharp
namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Provides file storage operations for modules.
/// Tier: Restricted
/// </summary>
public interface IStorageProvider : ICapabilityInterface
{
    /// <summary>
    /// Uploads a file to the storage system.
    /// </summary>
    Task UploadAsync(string path, Stream content, CancellationToken cancellationToken);
}
```

2. **Implement the interface** — core implementation:

```csharp
namespace DotNetCloud.Core.Services;

public class StorageProvider : IStorageProvider
{
    public async Task UploadAsync(string path, Stream content, CancellationToken cancellationToken)
    {
        // Implementation
    }
}
```

3. **Register with the appropriate tier** — in core service configuration:

```csharp
// Public tier
services.AddSingleton<INotificationService, NotificationService>();

// Restricted tier (manual approval required)
services.AddScoped<IStorageProvider, StorageProvider>();

// Privileged tier (security review required)
services.AddScoped<IUserManager, UserManager>();
```

4. **Declare in module manifest**:

```csharp
public class MyModuleManifest : IModuleManifest
{
    public IReadOnlyCollection<string> RequiredCapabilities => new[] 
    { 
        nameof(IStorageProvider),  // Restricted
        nameof(IUserDirectory)     // Public (optional, but listed)
    };
}
```

---

## Module System

### Overview

The module system is DotNetCloud's plugin framework. Modules are self-contained, versioned components that extend platform functionality with strict lifecycle management and isolation.

### Design Philosophy

- **Lifecycle-based:** Modules have defined initialization, startup, running, and shutdown phases
- **Manifest-driven:** Module metadata declared upfront for validation and compatibility checks
- **Event-based:** Modules communicate via events, not direct coupling
- **Isolated execution:** Modules run in separate processes with monitored health and resource limits
- **Graceful degradation:** System continues operating even if a module fails

### Module Lifecycle

```
[Discovery] → [Loading] → [Initialization] → [Started] → [Running] → [Stopping] → [Stopped]
                              ↓                                            ↓
                         [Manifest Validation]                      [Cleanup & Disposal]
```

#### Phase 1: Discovery
- File system scanning finds module assemblies
- Module manifest is located and validated
- Capability requirements are checked against system grants

#### Phase 2: Loading
- Module assembly is loaded into memory
- Dependency injection container is configured
- Module instance is created

#### Phase 3: Initialization (InitializeAsync)
- Module validates its environment
- Required capabilities are checked
- Internal state is set up
- Configuration is loaded and validated
- Subscriptions are registered (if using events)

**Best Practice:**
```csharp
public async Task InitializeAsync(ModuleInitializationContext context, CancellationToken cancellationToken)
{
    // 1. Validate required capabilities
    if (_userDirectory == null)
        throw new InvalidOperationException("IUserDirectory capability required");
    
    // 2. Load configuration
    var config = context.Configuration["MyModule"] as string ?? "{}";
    _settings = JsonSerializer.Deserialize<MyModuleSettings>(config);
    
    // 3. Subscribe to events
    await _eventBus.SubscribeAsync<UserCreatedEvent>(new MyEventHandler());
    
    // 4. Initialize state
    _initialized = true;
}
```

#### Phase 4: Started (StartAsync)
- Module begins accepting work
- Background tasks are started
- External connections are established

**Best Practice:**
```csharp
public async Task StartAsync(CancellationToken cancellationToken)
{
    // Start background jobs
    _backgroundProcessor = _ = ProcessEventsAsync(cancellationToken);
    
    // Establish external connections
    await _externalService.ConnectAsync(cancellationToken);
    
    _running = true;
}
```

#### Phase 5: Running
- Module processes requests and events
- Lifecycle methods are not called
- Module should be responsive and healthy

#### Phase 6: Stopping (StopAsync)
- System initiates graceful shutdown
- Module completes in-flight operations
- Resources are released

**Best Practice:**
```csharp
public async Task StopAsync(CancellationToken cancellationToken)
{
    _running = false;
    
    // Stop accepting new work
    // Wait for in-flight operations to complete
    await _backgroundProcessor.ConfigureAwait(false);
    
    // Unsubscribe from events
    await _eventBus.UnsubscribeAsync<UserCreatedEvent>(new MyEventHandler());
    
    // Close external connections gracefully
    await _externalService.CloseAsync(cancellationToken);
}
```

#### Phase 7: Stopped/Disposed
- All resources are released
- Module is removed from memory

### Module Manifest

The manifest declares what a module provides and what it needs:

```csharp
public class DocumentsModuleManifest : IModuleManifest
{
    public string Id => "dotnetcloud.documents";
    public string Name => "Documents";
    public string Version => "1.0.0";
    
    // Capabilities this module requires
    public IReadOnlyCollection<string> RequiredCapabilities => new[]
    {
        nameof(IStorageProvider),      // Restricted: file storage
        nameof(INotificationService)   // Public: send notifications
    };
    
    // Events this module publishes
    public IReadOnlyCollection<string> PublishedEvents => new[]
    {
        nameof(DocumentCreatedEvent),
        nameof(DocumentDeletedEvent),
        nameof(DocumentSharedEvent)
    };
    
    // Events this module subscribes to
    public IReadOnlyCollection<string> SubscribedEvents => new[]
    {
        nameof(UserDeletedEvent),      // Clean up docs when user deleted
        nameof(TeamDeletedEvent)       // Clean up team docs
    };
}
```

### Module Initialization Context

The context passed during initialization provides:

```csharp
public sealed record ModuleInitializationContext
{
    /// Module identifier (e.g., "dotnetcloud.documents")
    public required string ModuleId { get; init; }
    
    /// Full DI service provider with capabilities pre-filtered
    public required IServiceProvider Services { get; init; }
    
    /// Module-specific configuration from settings
    public required IReadOnlyDictionary<string, object> Configuration { get; init; }
    
    /// System caller context (for executing system operations)
    public required CallerContext SystemCaller { get; init; }
}
```

### Creating a Module

1. **Implement IModule**:

```csharp
public class DocumentsModule : IModule
{
    private readonly IStorageProvider _storage;
    private readonly IEventBus _eventBus;
    
    public IModuleManifest Manifest => new DocumentsModuleManifest();
    
    public DocumentsModule(IStorageProvider storage, IEventBus eventBus)
    {
        _storage = storage;
        _eventBus = eventBus;
    }
    
    public async Task InitializeAsync(ModuleInitializationContext context, CancellationToken cancellationToken)
    {
        // Validate and setup
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Begin operations
    }
    
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // Graceful shutdown
    }
}
```

2. **Implement IModuleLifecycle** (adds disposal):

```csharp
public class DocumentsModule : IModule, IModuleLifecycle
{
    public async ValueTask DisposeAsync()
    {
        // Async resource cleanup
    }
}
```

3. **Declare in manifest** — create manifest.json or attribute

4. **Test independently** — each module should have comprehensive unit and integration tests

---

## Event System

### Overview

The event system enables loose coupling between modules through a publish/subscribe pattern. Modules publish domain events, and other modules subscribe to react to those events without direct dependencies.

### Design Philosophy

- **Loose Coupling:** Publishers don't know about subscribers; subscribers don't know about publishers
- **Event-Driven:** Module communication flows through domain events
- **Eventual Consistency:** Events provide eventual consistency across the system
- **Audit Trail:** All events are stored for replay, debugging, and compliance
- **Capability-Aware:** Subscriptions respect capability tiers

### Event Contract

All events must implement `IEvent`:

```csharp
/// <summary>
/// All domain events in DotNetCloud implement this interface.
/// </summary>
public interface IEvent
{
    /// <summary>
    /// Unique identifier for this event instance (correlation ID)
    /// </summary>
    Guid EventId { get; }
    
    /// <summary>
    /// When the event was created (UTC)
    /// </summary>
    DateTime CreatedAt { get; }
}
```

### Defining Domain Events

Domain events capture something significant that happened:

```csharp
namespace DotNetCloud.Core.Events;

/// <summary>
/// Published when a user is created in the system.
/// </summary>
public sealed record UserCreatedEvent : IEvent
{
    public required Guid EventId { get; init; }
    public required DateTime CreatedAt { get; init; }
    
    /// The ID of the newly created user
    public required Guid UserId { get; init; }
    
    /// Email address provided during creation
    public required string Email { get; init; }
    
    /// Display name
    public required string DisplayName { get; init; }
}

/// Published when a file is shared with another user
public sealed record FileSharedEvent : IEvent
{
    public required Guid EventId { get; init; }
    public required DateTime CreatedAt { get; init; }
    
    public required Guid FileId { get; init; }
    public required Guid SharedByUserId { get; init; }
    public required Guid SharedWithUserId { get; init; }
    public required string PermissionLevel { get; init; } // "View", "Edit", "Admin"
}
```

**Best Practices for Domain Events:**

1. **Use Records:** Immutable, well-suited for events
2. **Include EventId & CreatedAt:** Enables correlation and ordering
3. **Use init-only properties:** Prevent accidental mutation
4. **Document the event:** Add XML comments explaining when/why it's raised
5. **Keep events small:** Only include necessary data; reference larger objects by ID
6. **Never include sensitive data:** Events are logged and audited

### Event Handlers

Handlers react to published events:

```csharp
/// <summary>
/// Interface that all event handlers must implement.
/// </summary>
/// <typeparam name="TEvent">The type of event to handle</typeparam>
public interface IEventHandler<TEvent> where TEvent : IEvent
{
    /// <summary>
    /// Handle the event asynchronously.
    /// </summary>
    /// <param name="event">The event being handled</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}
```

### Implementing Event Handlers

```csharp
/// <summary>
/// When a user is created, initialize their settings and send notification.
/// </summary>
public class UserCreatedEventHandler : IEventHandler<UserCreatedEvent>
{
    private readonly IModuleSettings _settings;
    private readonly INotificationService _notifications;
    
    public UserCreatedEventHandler(
        IModuleSettings settings,
        INotificationService notifications)
    {
        _settings = settings;
        _notifications = notifications;
    }
    
    public async Task HandleAsync(UserCreatedEvent @event, CancellationToken cancellationToken)
    {
        try
        {
            // Initialize user settings with defaults
            var userSettings = new UserSettingsDto
            {
                UserId = @event.UserId,
                PreferredLanguage = "en",
                Theme = "light"
            };
            await _settings.SaveAsync(userSettings, cancellationToken);
            
            // Send welcome notification
            await _notifications.SendAsync(
                @event.UserId,
                "Welcome",
                $"Welcome to DotNetCloud, {@event.DisplayName}!",
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Log but don't rethrow — don't cascade failures
            Console.WriteLine($"Error handling UserCreatedEvent: {ex.Message}");
        }
    }
}
```

### Publishing Events

Events are published through the event bus:

```csharp
public class UserService
{
    private readonly IEventBus _eventBus;
    
    public async Task CreateUserAsync(CreateUserDto dto, CallerContext caller, CancellationToken cancellationToken)
    {
        // ... create the user in database ...
        var userId = Guid.NewGuid();
        
        // Publish event
        var @event = new UserCreatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UserId = userId,
            Email = dto.Email,
            DisplayName = dto.DisplayName
        };
        
        await _eventBus.PublishAsync(@event, caller, cancellationToken);
    }
}
```

### Subscribing to Events

Modules subscribe during initialization:

```csharp
public async Task InitializeAsync(ModuleInitializationContext context, CancellationToken cancellationToken)
{
    // Subscribe to user creation events
    var handler = new UserCreatedEventHandler(
        context.Services.GetRequiredService<IModuleSettings>(),
        context.Services.GetRequiredService<INotificationService>());
    
    await _eventBus.SubscribeAsync(handler);
}
```

### Event Bus Interface

```csharp
/// <summary>
/// Core event bus for publish/subscribe communication.
/// </summary>
public interface IEventBus : ICapabilityInterface
{
    /// <summary>
    /// Publish an event to all subscribers.
    /// </summary>
    Task PublishAsync<TEvent>(TEvent @event, CallerContext caller, CancellationToken cancellationToken = default)
        where TEvent : IEvent;
    
    /// <summary>
    /// Subscribe a handler to an event type.
    /// </summary>
    Task SubscribeAsync<TEvent>(IEventHandler<TEvent> handler, CancellationToken cancellationToken = default)
        where TEvent : IEvent;
    
    /// <summary>
    /// Unsubscribe a handler from an event type.
    /// </summary>
    Task UnsubscribeAsync<TEvent>(IEventHandler<TEvent> handler, CancellationToken cancellationToken = default)
        where TEvent : IEvent;
}
```

### Event System Patterns

#### Event Choreography (Saga Pattern)

Multiple modules coordinate through events:

```
User Registration Flow:
1. UserService publishes UserCreatedEvent
2. SettingsModule subscribes, initializes default settings
3. NotificationModule subscribes, sends welcome email
4. TeamModule subscribes, creates default personal team
```

#### Event Sourcing (Future)

Events can be persisted for replay:

```csharp
// Future enhancement
public interface IEventStore : ICapabilityInterface
{
    Task StoreAsync<TEvent>(TEvent @event, CancellationToken cancellationToken)
        where TEvent : IEvent;
    
    Task<IAsyncEnumerable<IEvent>> GetEventsAsync(
        string aggregateId,
        CancellationToken cancellationToken);
}
```

---

## Authorization & Caller Context

### Caller Context

Every operation in DotNetCloud is associated with a `CallerContext` that identifies who/what is performing the operation:

```csharp
/// <summary>
/// Represents the context of a caller making a request.
/// Immutable record used throughout the system for authorization.
/// </summary>
public sealed record CallerContext
{
    /// <summary>
    /// The unique identifier of the caller.
    /// </summary>
    public Guid UserId { get; }
    
    /// <summary>
    /// Roles assigned to the caller.
    /// </summary>
    public IReadOnlyList<string> Roles { get; }
    
    /// <summary>
    /// Type of caller: User, System, or Module.
    /// </summary>
    public CallerType Type { get; }
}
```

### Caller Types

```csharp
/// <summary>
/// Identifies the type of entity making a request.
/// </summary>
public enum CallerType
{
    /// A human user authenticated via login
    User = 0,
    
    /// System performing background operations (admin tasks, cleanup)
    System = 1,
    
    /// A module performing operations within its scope
    Module = 2
}
```

### Using Caller Context in Services

```csharp
public class DocumentService
{
    private readonly IEventBus _eventBus;
    
    /// <summary>
    /// Create a document on behalf of the caller.
    /// </summary>
    public async Task<DocumentDto> CreateAsync(
        CreateDocumentDto dto,
        CallerContext caller,
        CancellationToken cancellationToken)
    {
        // Verify caller has permission
        if (!caller.Roles.Contains("user"))
        {
            throw new UnauthorizedException("Only users can create documents");
        }
        
        // Create document associated with caller
        var doc = new Document
        {
            Id = Guid.NewGuid(),
            OwnerId = caller.UserId,
            Title = dto.Title,
            CreatedAt = DateTime.UtcNow
        };
        
        // Save...
        
        // Publish event with caller context
        await _eventBus.PublishAsync(
            new DocumentCreatedEvent
            {
                EventId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                DocumentId = doc.Id,
                OwnerId = caller.UserId
            },
            caller,
            cancellationToken);
        
        return new DocumentDto { /* ... */ };
    }
}
```

### System Caller for Background Operations

```csharp
// System caller for cleanup operations
var systemCaller = new CallerContext(
    Guid.Empty,                    // Special system ID
    new[] { "system" },            // System role
    CallerType.System);

// Schedule a background cleanup task
await _documentService.CleanupOrphanedAsync(systemCaller, cancellationToken);
```

---

## Putting It All Together

### Example: Building a Chat Module

Here's how all the abstractions work together:

```csharp
// 1. Define domain events
public sealed record MessageSentEvent : IEvent
{
    public required Guid EventId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required Guid ChatRoomId { get; init; }
    public required Guid SenderId { get; init; }
    public required string Content { get; init; }
}

// 2. Implement event handler
public class MessageSentEventHandler : IEventHandler<MessageSentEvent>
{
    private readonly INotificationService _notifications;
    
    public async Task HandleAsync(MessageSentEvent @event, CancellationToken cancellationToken)
    {
        // Notify room members that a message was sent
        await _notifications.BroadcastAsync($"room:{@event.ChatRoomId}", 
            new { @event.SenderId, @event.Content }, cancellationToken);
    }
}

// 3. Create module manifest
public class ChatModuleManifest : IModuleManifest
{
    public string Id => "dotnetcloud.chat";
    public string Name => "Chat";
    public string Version => "1.0.0";
    
    public IReadOnlyCollection<string> RequiredCapabilities => new[]
    {
        nameof(ICurrentUserContext),      // Public
        nameof(INotificationService),     // Public
        nameof(IEventBus),                // Public
        nameof(IStorageProvider)          // Restricted: for file attachments
    };
    
    public IReadOnlyCollection<string> PublishedEvents => new[]
    {
        nameof(MessageSentEvent),
        nameof(RoomCreatedEvent)
    };
    
    public IReadOnlyCollection<string> SubscribedEvents => new[]
    {
        nameof(UserDeletedEvent)  // Clean up messages from deleted user
    };
}

// 4. Implement module
public class ChatModule : IModule
{
    private readonly IEventBus _eventBus;
    private readonly INotificationService _notifications;
    private readonly IStorageProvider? _storage;
    
    public IModuleManifest Manifest => new ChatModuleManifest();
    
    public ChatModule(
        IEventBus eventBus,
        INotificationService notifications,
        IStorageProvider? storage)
    {
        _eventBus = eventBus;
        _notifications = notifications;
        _storage = storage;  // Optional capability
    }
    
    public async Task InitializeAsync(ModuleInitializationContext context, CancellationToken cancellationToken)
    {
        // Setup
        var handler = new MessageSentEventHandler(_notifications);
        await _eventBus.SubscribeAsync(handler);
        
        if (_storage == null)
        {
            Console.WriteLine("Warning: File attachments disabled (IStorageProvider not granted)");
        }
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Start accepting messages
    }
    
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // Graceful shutdown
    }
}

// 5. Use in service
public class ChatService
{
    private readonly IEventBus _eventBus;
    
    public async Task SendMessageAsync(
        Guid chatRoomId,
        string content,
        CallerContext caller,
        CancellationToken cancellationToken)
    {
        // Validate permission
        // Save message to database
        
        // Publish event
        await _eventBus.PublishAsync(
            new MessageSentEvent
            {
                EventId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                ChatRoomId = chatRoomId,
                SenderId = caller.UserId,
                Content = content
            },
            caller,
            cancellationToken);
    }
}
```

---

## Best Practices

### Capability System

✅ **DO:**
- Check for null when injecting optional capabilities
- Declare all required capabilities in the manifest
- Fail fast if a required capability is missing
- Use specific capability interfaces, not generic "admin" roles

❌ **DON'T:**
- Assume a capability is available without checking
- Request capabilities you don't use
- Bypass capability checks with reflection
- Store capability instances outside the DI container

### Module System

✅ **DO:**
- Keep InitializeAsync fast (don't do long operations)
- Implement idempotency in StartAsync/StopAsync
- Log what you're doing at each lifecycle stage
- Handle cancellation tokens properly

❌ **DON'T:**
- Throw from StopAsync (attempt graceful shutdown)
- Store state in static fields (use DI instead)
- Call other modules' services directly (use events)
- Block on async operations (async all the way down)

### Event System

✅ **DO:**
- Use domain events for cross-module communication
- Include enough context in events for handlers to act
- Handle handler exceptions gracefully (don't cascade)
- Subscribe during initialization, unsubscribe during shutdown

❌ **DON'T:**
- Publish events for trivial internal state changes
- Include sensitive data in events
- Throw from event handlers
- Create circular event subscriptions

### Authorization

✅ **DO:**
- Pass CallerContext through all service calls
- Validate caller has required roles/permissions
- Log authorization decisions for auditing
- Use CallerType to distinguish users from system operations

❌ **DON'T:**
- Assume who the caller is without checking
- Hardcode permissions checks
- Use empty/default CallerContext
- Mix authentication with authorization concerns

---

## Related Documentation

- [Architecture Overview](./ARCHITECTURE.md)
- [Module Development Guide](./MODULE_DEVELOPMENT.md)
- [API Design Standards](./API_DESIGN.md)
- [Event System Reference](./EVENT_SYSTEM.md)

---

**Last Updated:** 2026-03-02  
**Author:** DotNetCloud Team  
**License:** AGPL-3.0
