# Chat Module Architecture

## System Overview

The Chat module is a **process-isolated module** that runs as a separate process from the DotNetCloud core server. It communicates with the core via gRPC over Unix sockets (Linux) or Named Pipes (Windows), and exposes its own REST API for client access.

```
┌─────────────────────────────────────────────────────────┐
│ Core Server (Supervisor)                                │
│                                                         │
│  CoreHub (SignalR) ← IRealtimeBroadcaster              │
│  EventBus (pub/sub)                                     │
│  User Directory                                         │
│                                                         │
│         │ gRPC (Unix socket / Named Pipe)               │
│         ▼                                               │
│  ┌────────────────────────────────────────────────────┐ │
│  │ Chat Module Process (DotNetCloud.Modules.Chat.Host)│ │
│  │                                                    │ │
│  │  ChatController (REST API)                         │ │
│  │  ChatGrpcService (gRPC interface)                  │ │
│  │  ChatRealtimeService → IRealtimeBroadcaster        │ │
│  │  Service Layer (business logic)                    │ │
│  │  ChatDbContext (EF Core)                           │ │
│  │  Push Notification Router                          │ │
│  └────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────┘
```

## Project Structure

### DotNetCloud.Modules.Chat (Core Library)

The SDK/interface layer containing domain models, DTOs, events, and service contracts. This project has no implementation — it defines the module's public API.

```
DotNetCloud.Modules.Chat/
├── Models/
│   ├── Channel.cs              # Channel entity (Id, Name, Type, Topic, etc.)
│   ├── Message.cs              # Message entity (Content, SenderUserId, ReplyTo, etc.)
│   ├── ChannelMember.cs        # Member-channel relationship (Role, Notifications, LastRead)
│   ├── MessageReaction.cs      # Reaction entity (Emoji, UserId, MessageId)
│   ├── MessageMention.cs       # Mention entity (Type, UserId, Position)
│   ├── MessageAttachment.cs    # Attachment entity (FileName, MimeType, FileNodeId)
│   ├── Announcement.cs         # Announcement entity (Title, Content, Priority, Expiry)
│   └── AnnouncementAcknowledgement.cs
├── DTOs/
│   └── ChatDtos.cs             # All request/response DTOs
├── Events/
│   └── ChatEvents.cs           # IEvent records and IEventHandler implementations
├── Services/
│   ├── IChannelService.cs
│   ├── IMessageService.cs
│   ├── IChannelMemberService.cs
│   ├── IReactionService.cs
│   ├── IPinService.cs
│   ├── ITypingIndicatorService.cs
│   ├── IAnnouncementService.cs
│   ├── IChatRealtimeService.cs
│   ├── IMentionNotificationService.cs
│   ├── IPushNotificationService.cs
│   └── INotificationPreferenceStore.cs
├── ChatModule.cs               # IModuleLifecycle implementation
└── ChatModuleManifest.cs       # IModuleManifest declaration
```

### DotNetCloud.Modules.Chat.Data (Data Access)

EF Core implementations of all service interfaces, entity configurations, and migrations.

```
DotNetCloud.Modules.Chat.Data/
├── ChatDbContext.cs             # EF Core DbContext for all chat entities
├── Configuration/
│   ├── ChannelConfiguration.cs
│   ├── MessageConfiguration.cs
│   ├── ChannelMemberConfiguration.cs
│   ├── MessageReactionConfiguration.cs
│   ├── MessageMentionConfiguration.cs
│   ├── MessageAttachmentConfiguration.cs
│   ├── AnnouncementConfiguration.cs
│   └── AnnouncementAcknowledgementConfiguration.cs
├── Services/
│   ├── ChannelService.cs
│   ├── MessageService.cs
│   ├── ChannelMemberService.cs
│   ├── ReactionService.cs
│   ├── PinService.cs
│   ├── TypingIndicatorService.cs
│   ├── AnnouncementService.cs
│   ├── MentionNotificationService.cs
│   └── Push/
│       ├── NotificationRouter.cs
│       ├── FcmPushProvider.cs
│       ├── UnifiedPushProvider.cs
│       ├── InMemoryNotificationDeliveryQueue.cs
│       └── NotificationDeliveryBackgroundService.cs
├── ChatDbInitializer.cs         # Seeds default public channels
└── Migrations/
    ├── PostgreSQL/              # Default provider migrations
    └── SqlServer/               # SQL Server-specific migrations
```

### DotNetCloud.Modules.Chat.Host (Web Host)

ASP.NET Core host process with REST controllers, gRPC services, and health checks.

```
DotNetCloud.Modules.Chat.Host/
├── Controllers/
│   └── ChatController.cs       # Consolidated REST API (~30 routes)
├── Services/
│   ├── ChatGrpcService.cs      # gRPC service implementation (10 RPCs)
│   ├── ChatLifecycleService.cs # Module lifecycle gRPC (init/start/stop/health)
│   ├── ChatRealtimeService.cs  # Real-time broadcast via IRealtimeBroadcaster
│   └── ChatHealthCheck.cs      # ASP.NET Core IHealthCheck
├── Protos/
│   └── chat_service.proto      # gRPC service definitions
└── Program.cs                  # Host configuration and DI
```

## Data Model

### Entity Relationships

```
Channel ──1:N── ChannelMember ──N:1── [User]
Channel ──1:N── Message ──1:N── MessageReaction
                Message ──1:N── MessageMention
                Message ──1:N── MessageAttachment
                Message ──0:1── Message (ReplyTo)

Announcement ──1:N── AnnouncementAcknowledgement
```

### Channel

| Column | Type | Description |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `Name` | `string` | Channel name (unique per type) |
| `Description` | `string?` | Channel description |
| `Type` | `ChannelType` | Public, Private, DirectMessage, Group |
| `Topic` | `string?` | Current topic |
| `AvatarUrl` | `string?` | Channel avatar |
| `IsArchived` | `bool` | Archived state |
| `OrganizationId` | `Guid?` | Organization scope |
| `CreatedByUserId` | `Guid` | Creator |
| `CreatedAt` | `DateTime` | Creation timestamp |
| `LastActivityAt` | `DateTime?` | Last message timestamp |

### Message

| Column | Type | Description |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `ChannelId` | `Guid` | Foreign key to Channel |
| `SenderUserId` | `Guid` | Author user ID |
| `Content` | `string` | Markdown content |
| `Type` | `MessageType` | Text, System, Notification |
| `SentAt` | `DateTime` | Send timestamp |
| `EditedAt` | `DateTime?` | Last edit timestamp |
| `IsEdited` | `bool` | Edit flag |
| `IsPinned` | `bool` | Pin flag |
| `PinnedAt` | `DateTime?` | Pin timestamp |
| `IsDeleted` | `bool` | Soft delete flag |
| `ReplyToMessageId` | `Guid?` | Thread parent |

### ChannelMember

| Column | Type | Description |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `ChannelId` | `Guid` | Foreign key to Channel |
| `UserId` | `Guid` | User ID |
| `Role` | `ChannelMemberRole` | Member, Admin, Owner |
| `JoinedAt` | `DateTime` | Join timestamp |
| `IsMuted` | `bool` | Muted flag |
| `NotificationPreference` | `NotificationPreference` | All, Mentions, None |
| `LastReadMessageId` | `Guid?` | Last read message (for unread counts) |

## Service Layer

### Authorization Model

All service methods accept a `CallerContext` that identifies the caller (User, System, or Module). Authorization is enforced at the service layer:

| Operation | Required Role |
|---|---|
| Create channel | Any authenticated user |
| Update channel | Admin or Owner |
| Delete channel | Owner only |
| Archive channel | Admin or Owner |
| Add member | Admin or Owner |
| Remove member | Admin or Owner (cannot remove last owner) |
| Update member role | Owner only |
| Send message | Channel member |
| Edit message | Message author only |
| Delete message | Message author, Admin, or Owner |
| Add/remove reaction | Channel member |
| Pin/unpin message | Admin or Owner |

### Mention Processing

When a message is sent, the `MessageService` parses @mentions from the content and creates `MessageMention` records. Types of mentions:

| Type | Syntax | Behavior |
|---|---|---|
| User | `@username` | Notifies specific user |
| Channel | `@channel` | Notifies all channel members |
| All | `@all` | Notifies all channel members |

The `MentionNotificationService` dispatches notifications via both SignalR (real-time) and push notifications, excluding the sender. Unread mention counts include `@all` and `@channel` mentions.

### Typing Indicators

Typing indicators are **in-memory only** (not persisted). They auto-expire after a configurable timeout. The `TypingIndicatorService` maintains a concurrent dictionary of active typists per channel.

## gRPC Interface

The Chat module's gRPC interface is used by the core supervisor for inter-process communication:

### ChatService (10 RPCs)

| RPC | Description |
|---|---|
| `CreateChannel` | Create a new channel |
| `GetChannel` | Get channel by ID |
| `ListChannels` | List channels for a user |
| `SendMessage` | Send a message to a channel |
| `GetMessages` | Get paginated messages |
| `EditMessage` | Edit an existing message |
| `DeleteMessage` | Delete a message |
| `AddReaction` | Add emoji reaction |
| `RemoveReaction` | Remove emoji reaction |
| `NotifyTyping` | Signal typing activity |

### ChatLifecycleService

| RPC | Description |
|---|---|
| `Initialize` | Initialize module with context |
| `Start` | Start module processing |
| `Stop` | Gracefully stop module |
| `GetHealth` | Health check status |
| `GetManifest` | Module manifest information |

## Real-Time Broadcasting

The `ChatRealtimeService` uses the core's `IRealtimeBroadcaster` capability to push events to connected SignalR clients. It uses channel-based groups (`chat-channel-{channelId}`) for targeted delivery.

See [Real-Time Events](REALTIME.md) for the full event catalog and SignalR method reference.

## Push Notification Pipeline

```
Message Sent → MentionNotificationService
                    │
                    ├── SignalR (real-time, online users)
                    │
                    └── NotificationRouter
                            │
                            ├── Check preferences (DND, muted channels)
                            ├── Check online status (skip if real-time delivered)
                            │
                            ├── FcmPushProvider (FCM devices)
                            │     └── Firebase HTTP v1 API
                            │
                            └── UnifiedPushProvider (UP devices)
                                  └── HTTP POST to distributor endpoint
                            │
                            └── On failure → INotificationDeliveryQueue
                                              └── NotificationDeliveryBackgroundService
                                                    └── Retry with exponential backoff
```

See [Push Notifications](PUSH.md) for provider configuration and retry semantics.

## Database Migrations

```bash
# PostgreSQL (default)
dotnet ef migrations add <Name> \
  --project src/Modules/Chat/DotNetCloud.Modules.Chat.Data \
  --context ChatDbContext

# SQL Server
dotnet ef migrations add <Name>_SqlServer \
  --project src/Modules/Chat/DotNetCloud.Modules.Chat.Data \
  --context ChatDbContext \
  --output-dir Migrations/SqlServer
```

The `ChatDbInitializer` seeds three default public channels (`#general`, `#random`, `#announcements`) on first startup with idempotent checks.
