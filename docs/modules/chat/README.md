# DotNetCloud Chat Module

> **Module ID:** `dotnetcloud.chat`
> **Version:** 1.0.0
> **Status:** Implemented (Phase 2)
> **License:** AGPL-3.0

---

## Overview

The Chat module provides real-time messaging for DotNetCloud organizations. It supports public and private channels, direct messages, group DMs, threaded replies, emoji reactions, message pinning, typing indicators, presence tracking, announcements, file attachments (integrated with the Files module), and push notifications via FCM or UnifiedPush.

## Key Features

| Feature | Description |
|---|---|
| **Channels** | Public, private, and archived channels with topic and description |
| **Direct Messages** | One-to-one DMs and multi-user group DMs |
| **Real-Time Messaging** | SignalR-based real-time message delivery, editing, and deletion |
| **Reactions** | Emoji reactions with per-user tracking and grouped counts |
| **Pinned Messages** | Pin important messages for quick reference |
| **Typing Indicators** | In-memory, time-expiring typing status per channel |
| **Presence** | Online/Away/DoNotDisturb/Offline status with custom status messages |
| **@Mentions** | User, channel, and @all mentions with notification dispatch |
| **Announcements** | Organization-wide announcements with priority levels and acknowledgement tracking |
| **File Attachments** | Attach files to messages, integrated with the Files module via `FileNodeId` |
| **Push Notifications** | FCM (Google Play) and UnifiedPush (F-Droid/self-hosted) with retry queue |
| **Unread Counts** | Per-channel unread message and mention counts |
| **Message Search** | Full-text search within channels |
| **Video/Audio Calls** | WebRTC-based 1:1 and group calls from any channel type |
| **Screen Sharing** | Browser-native screen sharing during calls |
| **Call History** | Per-channel call history with pagination |
| **LiveKit SFU** | Optional SFU for group calls with 4+ participants |
| **STUN/TURN** | Built-in STUN server, configurable coturn TURN relay |

## Architecture

The Chat module follows the DotNetCloud module architecture pattern with three projects:

```
src/Modules/Chat/
├── DotNetCloud.Modules.Chat/          # Core domain models, DTOs, interfaces, events
├── DotNetCloud.Modules.Chat.Data/     # EF Core context, entity configs, service implementations
└── DotNetCloud.Modules.Chat.Host/     # ASP.NET Core host: REST controllers, gRPC services
```

### Module Manifest

The `ChatModuleManifest` declares:

- **Required Capabilities:** `INotificationService`, `IUserDirectory`, `ICurrentUserContext`, `IRealtimeBroadcaster`
- **Published Events:** `MessageSentEvent`, `ChannelCreatedEvent`, `ChannelDeletedEvent`, `UserJoinedChannelEvent`, `UserLeftChannelEvent`, `PresenceChangedEvent`, `VideoCallInitiatedEvent`, `VideoCallAnsweredEvent`, `VideoCallEndedEvent`, `VideoCallMissedEvent`, `ParticipantJoinedCallEvent`, `ParticipantLeftCallEvent`, `ScreenShareStartedEvent`, `ScreenShareEndedEvent`
- **Subscribed Events:** `FileUploadedEvent` (from Files module — for file sharing integration)

### Data Flow

```
Client → REST API / gRPC → Service Layer → EF Core → Database
                                         → IChatRealtimeService → SignalR (IRealtimeBroadcaster)
                                         → IPushNotificationService → FCM / UnifiedPush
                                         → IEventBus → Other Modules
```

## Project Structure

### DotNetCloud.Modules.Chat (Core)

| Directory | Contents |
|---|---|
| `Models/` | Entity models (`Channel`, `Message`, `ChannelMember`, `MessageReaction`, `Announcement`, etc.) |
| `DTOs/` | Data transfer objects for API requests/responses |
| `Events/` | Domain events implementing `IEvent` |
| `Services/` | Service interfaces (`IChannelService`, `IMessageService`, etc.) |

### DotNetCloud.Modules.Chat.Data (Data Access)

| Directory | Contents |
|---|---|
| `Configuration/` | EF Core `IEntityTypeConfiguration` for all entities |
| `Services/` | Service implementations (`ChannelService`, `MessageService`, etc.) |
| `Migrations/` | PostgreSQL and SQL Server migrations |

### DotNetCloud.Modules.Chat.Host (Web Host)

| Directory | Contents |
|---|---|
| `Controllers/` | REST API controller (`ChatController` — consolidated) |
| `Services/` | gRPC services (`ChatGrpcService`, `ChatLifecycleService`) and realtime (`ChatRealtimeService`) |
| `Protos/` | Protobuf service definitions |

## Database Support

| Provider | Status |
|---|---|
| PostgreSQL | Supported (schema: `chat.*`) |
| SQL Server | Supported (schema: `chat.*`) |
| MariaDB | Pending (awaiting Pomelo .NET 10 support) |

## Module Lifecycle

The `ChatModule` class implements `IModuleLifecycle`:

| Phase | Action |
|---|---|
| **Initialize** | Resolve `IEventBus`, subscribe `MessageSentEventHandler` and `ChannelCreatedEventHandler` |
| **Start** | Set running state, log startup |
| **Stop** | Unsubscribe event handlers, log shutdown |
| **Dispose** | Release resources |

## Enums

| Enum | Values | Description |
|---|---|---|
| `ChannelType` | `Public`, `Private`, `DirectMessage`, `Group` | Channel visibility and purpose |
| `ChannelMemberRole` | `Member`, `Admin`, `Owner` | Member permission level |
| `NotificationPreference` | `All`, `Mentions`, `None` | Per-channel notification filtering |
| `PresenceStatus` | `Online`, `Away`, `DoNotDisturb`, `Offline` | User availability |
| `MessageType` | `Text`, `System`, `Notification` | Message category |
| `MentionType` | `User`, `Channel`, `All` | @mention scope |
| `AnnouncementPriority` | `Normal`, `Important`, `Urgent` | Announcement urgency |
| `VideoCallState` | `Ringing`, `Connecting`, `Active`, `OnHold`, `Ended`, `Missed`, `Rejected`, `Failed` | Call lifecycle state |
| `VideoCallEndReason` | `Normal`, `Rejected`, `Missed`, `TimedOut`, `Failed`, `Cancelled` | Why a call ended |
| `CallParticipantRole` | `Initiator`, `Participant` | Role in a call |
| `CallMediaType` | `Audio`, `Video`, `ScreenShare` | Media type |

## Related Documentation

- [REST API Reference](API.md)
- [Architecture & Data Model](ARCHITECTURE.md)
- [Real-Time Events (SignalR)](REALTIME.md)
- [Push Notifications](PUSH.md)
- [Chat ↔ Tracks Integration](../CHAT_TRACKS_INTEGRATION.md)
- [Video Calling Admin Guide](../../admin/VIDEO_CALLING.md)

## Test Coverage

| Test Project | Tests | Description |
|---|---|---|
| `DotNetCloud.Modules.Chat.Tests` | 1027 | Unit tests for all services, models, events, controllers, and video calling |
| `DotNetCloud.Integration.Tests` | 47 | REST API integration tests via `ChatHostWebApplicationFactory` |
