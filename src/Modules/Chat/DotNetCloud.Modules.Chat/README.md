# DotNetCloud.Modules.Chat

Core library for the DotNetCloud Chat module. Contains domain models, service interfaces, DTOs, and domain events. This project has **no infrastructure dependencies** — it references only `DotNetCloud.Core`.

## Project Structure

```
DotNetCloud.Modules.Chat/
├── ChatModule.cs               # IModule lifecycle implementation
├── ChatModuleManifest.cs       # Module manifest (capabilities, events)
├── Models/                     # Domain entities
│   ├── Channel.cs
│   ├── ChannelMember.cs
│   ├── Message.cs
│   ├── MessageAttachment.cs
│   ├── MessageReaction.cs
│   ├── MessageMention.cs
│   ├── PinnedMessage.cs
│   ├── Announcement.cs
│   ├── AnnouncementAcknowledgement.cs
│   ├── ChannelType.cs          # Enum: Public, Private, Direct
│   ├── ChannelMemberRole.cs    # Enum: Owner, Admin, Member
│   ├── NotificationPreference.cs
│   └── PresenceStatus.cs       # Enum: Online, Away, DND, Offline
├── DTOs/
│   └── ChatDtos.cs             # Record types for API transport
├── Events/                     # Domain events (pub/sub via IEventBus)
│   ├── MessageSentEvent.cs
│   ├── ChannelCreatedEvent.cs
│   ├── ChannelDeletedEvent.cs
│   ├── UserJoinedChannelEvent.cs
│   ├── UserLeftChannelEvent.cs
│   ├── PresenceChangedEvent.cs
│   └── Handlers/
├── Services/                   # Service interfaces + push providers
│   ├── IChannelService.cs
│   ├── IMessageService.cs
│   ├── IChannelMemberService.cs
│   ├── IReactionService.cs
│   ├── IPinService.cs
│   ├── ITypingIndicatorService.cs
│   ├── IAnnouncementService.cs
│   ├── IPushNotificationService.cs
│   └── ...
└── UI/                         # Blazor UI components (future)
```

## Module Identity

- **Module ID:** `dotnetcloud.chat`
- **Required Capabilities:** `INotificationService`, `IRealtimeBroadcaster`
- **Published Events:** `MessageSentEvent`, `ChannelCreatedEvent`, `ChannelDeletedEvent`, `UserJoinedChannelEvent`, `UserLeftChannelEvent`, `PresenceChangedEvent`

## Key Concepts

### Domain Models

All entities inherit soft-delete and timestamp semantics from the core framework. Key relationships:

- **Channel** → has many `ChannelMember`, `Message`, `Announcement`
- **Message** → has many `MessageAttachment`, `MessageReaction`, `MessageMention`
- **Message** → can be a `PinnedMessage`

### Service Interfaces

Each interface covers a focused domain area:

| Interface | Responsibility |
|-----------|---------------|
| `IChannelService` | Channel CRUD, search |
| `IMessageService` | Message send/edit/delete, history |
| `IChannelMemberService` | Join/leave, role management |
| `IReactionService` | Add/remove reactions |
| `IPinService` | Pin/unpin messages |
| `ITypingIndicatorService` | Start/stop typing indicators |
| `IAnnouncementService` | Channel announcements |

### DTOs

All DTOs are C# `record` types defined in `ChatDtos.cs`. They mirror the domain models but are transport-safe (no navigation properties, no EF dependencies).

## Dependencies

- `DotNetCloud.Core` — core interfaces (`IModule`, `IEventBus`, `CallerContext`, capability interfaces)

## Related Projects

| Project | Purpose |
|---------|---------|
| `DotNetCloud.Modules.Chat.Data` | EF Core implementations of service interfaces |
| `DotNetCloud.Modules.Chat.Host` | REST API, gRPC, and SignalR host |

## Documentation

See `docs/modules/chat/` for full documentation:
- [README](../../../docs/modules/chat/README.md) — Module overview
- [API Reference](../../../docs/modules/chat/API.md) — REST endpoint reference
- [Architecture](../../../docs/modules/chat/ARCHITECTURE.md) — Data model and design
- [Real-time Events](../../../docs/modules/chat/REALTIME.md) — SignalR event reference
- [Push Notifications](../../../docs/modules/chat/PUSH.md) — FCM/UnifiedPush setup
